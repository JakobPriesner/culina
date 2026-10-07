using Application.Abstractions;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Migrations;
using IntegrationTests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntegrationTests.Persistence;

[Collection(RequiresDatabase.Name)]
public class MigrationRunnerTests(PostgresFixture postgres)
{
    [Fact]
    public async Task ApplyAsync_ShouldRunEveryMigration_WhenTheDatabaseIsEmpty()
    {
        await using var session = NewSession();
        var (runner, executor) = Build(session);
        var table = Unique("widgets");
        var migrations = new[] { new SqlMigration("0001_first", $"create table {table}(id int);") };

        await runner.ApplyAsync(migrations, TestContext.Current.CancellationToken);

        Assert.True(await TableExistsAsync(executor, table));
        Assert.Equal(1, await AppliedCountAsync(executor, "0001_first"));
    }

    [Fact]
    public async Task ApplyAsync_ShouldChangeNothing_WhenEveryMigrationHasAlreadyRun()
    {
        await using var session = NewSession();
        var (runner, executor) = Build(session);
        var table = Unique("gadgets");
        var migrations = new[] { new SqlMigration(Unique("0001"), $"create table {table}(id int);") };
        await runner.ApplyAsync(migrations, TestContext.Current.CancellationToken);

        await runner.ApplyAsync(migrations, TestContext.Current.CancellationToken);

        // Running twice must be a no-op: a `create table` without `if not exists` would fail on
        // re-apply.
        Assert.Equal(1, await AppliedCountAsync(executor, migrations[0].Version));
    }

    [Fact]
    public async Task ApplyAsync_ShouldRefuseToStart_WhenAnAppliedMigrationWasEdited()
    {
        await using var session = NewSession();
        var (runner, _) = Build(session);
        var version = Unique("0001");
        var original = new[] { new SqlMigration(version, $"create table {Unique("t")}(id int);") };
        await runner.ApplyAsync(original, TestContext.Current.CancellationToken);
        var edited = new[] { new SqlMigration(version, $"create table {Unique("t")}(id int, name text);") };

        async Task Act() => await runner.ApplyAsync(edited, TestContext.Current.CancellationToken);

        // Migrations are forward-only: silently editing history would leave existing databases
        // different from a fresh one.
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(Act);
        Assert.Contains(version, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplyAsync_ShouldKeepEarlierMigrations_WhenALaterOneFails()
    {
        await using var session = NewSession();
        var (runner, executor) = Build(session);
        var good = Unique("0001");
        var table = Unique("kept");
        var migrations = new[]
        {
            new SqlMigration(good, $"create table {table}(id int);"),
            new SqlMigration(Unique("0002"), "this is not valid sql;")
        };

        async Task Act() => await runner.ApplyAsync(migrations, TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<Exception>(Act);
        // Each migration commits on its own, so the next start resumes instead of redoing it.
        Assert.True(await TableExistsAsync(executor, table));
        Assert.Equal(1, await AppliedCountAsync(executor, good));
    }

    [Fact]
    public async Task ApplyAsync_ShouldApplyTheRealBaseline_WhenLoadedFromTheAssembly()
    {
        await using var session = NewSession();
        var (runner, executor) = Build(session);
        var migrations = EmbeddedMigrations.Load();

        await runner.ApplyAsync(migrations, TestContext.Current.CancellationToken);

        Assert.NotEmpty(migrations);
        Assert.True(await TableExistsAsync(executor, "settings"));
    }

    private DbSession NewSession() => PostgresFixture.SessionOn(CulinaDataSource.Build(postgres.Settings));

    private static (MigrationRunner Runner, DbExecutor Executor) Build(DbSession session)
    {
        var executor = new DbExecutor(session);
        IUnitOfWork unitOfWork = new UnitOfWork(session);

        return (new MigrationRunner(session, executor, unitOfWork, NullLogger<MigrationRunner>.Instance), executor);
    }

    private static string Unique(string prefix) => $"{prefix}_{Guid.CreateVersion7():n}";

    private static async Task<bool> TableExistsAsync(DbExecutor executor, string table) =>
        await executor.ExecuteScalarAsync<bool>(
            "select exists (select 1 from information_schema.tables where table_name = @table);",
            new { table },
            TestContext.Current.CancellationToken);

    private static async Task<int> AppliedCountAsync(DbExecutor executor, string version) =>
        await executor.ExecuteScalarAsync<int>(
            "select count(*) from schema_migrations where version = @version;",
            new { version },
            TestContext.Current.CancellationToken);
}
