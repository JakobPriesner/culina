using Domain.Shared;
using Infrastructure.Persistence;
using IntegrationTests.Fixtures;
using Npgsql;
using TestSupport;

namespace IntegrationTests.Persistence;

[Collection(RequiresDatabase.Name)]
public class DbSessionTests(PostgresFixture postgres)
{
    private const long LockKey = 0x74657374; // "test"

    private static readonly Error ScratchFailure = new("tests.scratch", "The work declined.", ErrorType.Conflict);

    [Fact]
    public async Task Executor_ShouldRunAStatement_WhenTheConnectionIsOpened()
    {
        await using var session = NewSession();
        var executor = new DbExecutor(session);

        var answer = await executor.ExecuteScalarAsync<int>(
            "select 1;",
            parameters: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(1, answer);
    }

    [Fact]
    public async Task UnitOfWork_ShouldKeepTheWrites_WhenTheWorkCompletes()
    {
        await using var session = NewSession();
        var executor = new DbExecutor(session);
        var unitOfWork = new UnitOfWork(session);
        var table = await CreateScratchTableAsync(executor);

        await unitOfWork.InTransactionAsync(
            async token =>
            {
                await executor.ExecuteAsync($"insert into {table}(id) values (1);", null, token);
                return true;
            },
            TestContext.Current.CancellationToken);

        var rows = await CountAsync(executor, table);
        Assert.Equal(1, rows);
    }

    [Fact]
    public async Task UnitOfWork_ShouldDiscardEveryWrite_WhenTheWorkThrows()
    {
        await using var session = NewSession();
        var executor = new DbExecutor(session);
        var unitOfWork = new UnitOfWork(session);
        var table = await CreateScratchTableAsync(executor);

        async Task Act() => await unitOfWork.InTransactionAsync<bool>(
            async token =>
            {
                await executor.ExecuteAsync($"insert into {table}(id) values (1);", null, token);
                await executor.ExecuteAsync($"insert into {table}(id) values (2);", null, token);

                throw new InvalidOperationException("the second half of the work failed");
            },
            TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(Act);
        // Disposing an uncommitted transaction rolls it back: partial writes are what the unit of
        // work prevents.
        var rows = await CountAsync(executor, table);
        Assert.Equal(0, rows);
    }

    [Fact]
    public async Task UnitOfWork_ShouldDiscardEveryWrite_WhenTheWorkReturnsAFailure()
    {
        await using var session = NewSession();
        var executor = new DbExecutor(session);
        var unitOfWork = new UnitOfWork(session);
        var table = await CreateScratchTableAsync(executor);

        var result = await unitOfWork.InTransactionAsync(
            async token =>
            {
                await executor.ExecuteAsync($"insert into {table}(id) values (1);", null, token);

                return Result.Failure(ScratchFailure);
            },
            TestContext.Current.CancellationToken);

        // An expected failure is returned, not thrown, but still a reason to discard the work so
        // far.
        result.ShouldBeFailure(ScratchFailure);
        var rows = await CountAsync(executor, table);
        Assert.Equal(0, rows);
    }

    [Fact]
    public async Task UnitOfWork_ShouldDiscardEveryWrite_WhenTheWorkReturnsAFailedValue()
    {
        await using var session = NewSession();
        var executor = new DbExecutor(session);
        var unitOfWork = new UnitOfWork(session);
        var table = await CreateScratchTableAsync(executor);

        var result = await unitOfWork.InTransactionAsync(
            async token =>
            {
                await executor.ExecuteAsync($"insert into {table}(id) values (1);", null, token);

                return Result<int>.Failure(ScratchFailure);
            },
            TestContext.Current.CancellationToken);

        result.ShouldBeFailure(ScratchFailure);
        var rows = await CountAsync(executor, table);
        Assert.Equal(0, rows);
    }

    [Fact]
    public async Task UnitOfWork_ShouldRefuseToNest_WhenATransactionIsAlreadyOpen()
    {
        await using var session = NewSession();
        var unitOfWork = new UnitOfWork(session);

        async Task Act() => await unitOfWork.InTransactionAsync(
            async _ => await unitOfWork.InTransactionAsync(_ => Task.FromResult(true), CancellationToken.None),
            TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(Act);
    }

    [Fact]
    public async Task UnitOfWork_ShouldReleaseTheTransaction_WhenTheWorkCompletes()
    {
        await using var session = NewSession();
        var unitOfWork = new UnitOfWork(session);

        await unitOfWork.InTransactionAsync(_ => Task.FromResult(true), TestContext.Current.CancellationToken);
        await unitOfWork.InTransactionAsync(_ => Task.FromResult(true), TestContext.Current.CancellationToken);

        // A second unit of work per request must be possible; only nesting is refused.
        Assert.Null(session.Transaction);
    }

    [Fact]
    public async Task Executor_ShouldGiveTheConnectionBack_AfterEachStatement()
    {
        // A pool of one: the second session can only run once the first, still alive as during
        // streaming, lets go.
        await using var pool = OneConnectionPool();
        await using var first = PostgresFixture.SessionOn(pool);
        await using var second = PostgresFixture.SessionOn(pool);
        await new DbExecutor(first).ExecuteScalarAsync<int>("select 1;", null, Token);

        var answer = await new DbExecutor(second).ExecuteScalarAsync<int>("select 1;", null, Token);

        Assert.Equal(1, answer);
    }

    [Fact]
    public async Task UnitOfWork_ShouldGiveTheConnectionBack_WhenTheTransactionEnds()
    {
        await using var pool = OneConnectionPool();
        await using var first = PostgresFixture.SessionOn(pool);
        await using var second = PostgresFixture.SessionOn(pool);
        var executor = new DbExecutor(first);
        await new UnitOfWork(first).InTransactionAsync(
            async token => await executor.ExecuteScalarAsync<int>("select 1;", null, token),
            Token);

        var answer = await new DbExecutor(second).ExecuteScalarAsync<int>("select 1;", null, Token);

        Assert.Equal(1, answer);
    }

    [Fact]
    public async Task Executor_ShouldLoseStateKeptOnTheConnection_OnceItIsGivenBack()
    {
        // Why a pin exists: a pooled connection is reset, and a session-level lock goes with the
        // reset.
        await using var pool = OneConnectionPool();
        await using var session = PostgresFixture.SessionOn(pool);
        var executor = new DbExecutor(session);
        await executor.ExecuteAsync("select pg_advisory_lock(@key);", new { key = LockKey }, Token);

        var unlocked = await executor.ExecuteScalarAsync<bool>("select pg_advisory_unlock(@key);", new { key = LockKey }, Token);

        Assert.False(unlocked);
    }

    [Fact]
    public async Task Pin_ShouldKeepStateKeptOnTheConnection_UntilItEnds()
    {
        await using var pool = OneConnectionPool();
        await using var session = PostgresFixture.SessionOn(pool);
        var executor = new DbExecutor(session);
        bool unlocked;

        await using (await session.PinAsync(Token))
        {
            await executor.ExecuteAsync("select pg_advisory_lock(@key);", new { key = LockKey }, Token);
            unlocked = await executor.ExecuteScalarAsync<bool>("select pg_advisory_unlock(@key);", new { key = LockKey }, Token);
        }

        // The migration runner's lock is held this way, across its separate transactions.
        Assert.True(unlocked);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private NpgsqlDataSource OneConnectionPool() =>
        CulinaDataSource.Build(postgres.Settings with { MaxPoolSize = 1 });

    private DbSession NewSession() => PostgresFixture.SessionOn(CulinaDataSource.Build(postgres.Settings));

    private static async Task<string> CreateScratchTableAsync(DbExecutor executor)
    {
        var table = $"scratch_{Guid.CreateVersion7():n}";

        await executor.ExecuteAsync(
            $"create table {table}(id int primary key);",
            parameters: null,
            TestContext.Current.CancellationToken);

        return table;
    }

    private static async Task<int> CountAsync(DbExecutor executor, string table) =>
        await executor.ExecuteScalarAsync<int>(
            $"select count(*) from {table};",
            parameters: null,
            TestContext.Current.CancellationToken);
}
