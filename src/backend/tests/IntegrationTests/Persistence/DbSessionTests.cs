using Infrastructure.Persistence;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Persistence;

[Collection(RequiresDatabase.Name)]
public class DbSessionTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Executor_ShouldRunAStatement_WhenTheConnectionIsOpened()
    {
        // Arrange
        await using var session = NewSession();
        var executor = new DbExecutor(session);

        // Act
        var answer = await executor.ExecuteScalarAsync<int>(
            "select 1;",
            parameters: null,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, answer);
    }

    [Fact]
    public async Task UnitOfWork_ShouldKeepTheWrites_WhenTheWorkCompletes()
    {
        // Arrange
        await using var session = NewSession();
        var executor = new DbExecutor(session);
        var unitOfWork = new UnitOfWork(session);
        var table = await CreateScratchTableAsync(executor);

        // Act
        await unitOfWork.InTransactionAsync(
            async token =>
            {
                await executor.ExecuteAsync($"insert into {table}(id) values (1);", null, token);
                return true;
            },
            TestContext.Current.CancellationToken);

        // Assert
        var rows = await CountAsync(executor, table);
        Assert.Equal(1, rows);
    }

    [Fact]
    public async Task UnitOfWork_ShouldDiscardEveryWrite_WhenTheWorkThrows()
    {
        // Arrange
        await using var session = NewSession();
        var executor = new DbExecutor(session);
        var unitOfWork = new UnitOfWork(session);
        var table = await CreateScratchTableAsync(executor);

        // Act
        async Task Act() => await unitOfWork.InTransactionAsync<bool>(
            async token =>
            {
                await executor.ExecuteAsync($"insert into {table}(id) values (1);", null, token);
                await executor.ExecuteAsync($"insert into {table}(id) values (2);", null, token);

                throw new InvalidOperationException("the second half of the work failed");
            },
            TestContext.Current.CancellationToken);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(Act);
        // Disposing an uncommitted transaction rolls it back, so neither insert
        // survives — partial writes are what the unit of work exists to prevent.
        var rows = await CountAsync(executor, table);
        Assert.Equal(0, rows);
    }

    [Fact]
    public async Task UnitOfWork_ShouldRefuseToNest_WhenATransactionIsAlreadyOpen()
    {
        // Arrange
        await using var session = NewSession();
        var unitOfWork = new UnitOfWork(session);

        // Act
        async Task Act() => await unitOfWork.InTransactionAsync(
            async _ => await unitOfWork.InTransactionAsync(_ => Task.FromResult(true), CancellationToken.None),
            TestContext.Current.CancellationToken);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(Act);
    }

    [Fact]
    public async Task UnitOfWork_ShouldReleaseTheTransaction_WhenTheWorkCompletes()
    {
        // Arrange
        await using var session = NewSession();
        var unitOfWork = new UnitOfWork(session);

        // Act
        await unitOfWork.InTransactionAsync(_ => Task.FromResult(true), TestContext.Current.CancellationToken);
        await unitOfWork.InTransactionAsync(_ => Task.FromResult(true), TestContext.Current.CancellationToken);

        // Assert
        // A second unit of work on the same request must be possible; only
        // nesting is refused.
        Assert.Null(session.Transaction);
    }

    private DbSession NewSession() => new(CulinaDataSource.Build(postgres.Settings));

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
