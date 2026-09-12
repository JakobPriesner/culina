using Infrastructure.Persistence;

namespace IntegrationTests.Fixtures;

[Collection(RequiresDatabase.Name)]
public class DatabaseResetTests(PostgresFixture postgres)
{
    [Fact]
    public async Task ResetAsync_ShouldEmptyEveryTable_ButKeepTheMigratedSchema()
    {
        // Arrange
        _ = postgres.Api.Services; // starting the host applies the migrations
        await using var session = postgres.NewSession();
        var executor = new DbExecutor(session);

        await executor.ExecuteAsync(
            "insert into settings (group_name, payload) values ('reset_probe', '{}'::jsonb);",
            null,
            TestContext.Current.CancellationToken);

        // Act
        await postgres.ResetAsync(TestContext.Current.CancellationToken);

        // Assert
        var rows = await executor.ExecuteScalarAsync<int>(
            "select count(*) from settings;",
            null,
            TestContext.Current.CancellationToken);
        var migrations = await executor.ExecuteScalarAsync<int>(
            "select count(*) from schema_migrations;",
            null,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, rows);
        // The migration history survives, so the schema is built once per run
        // rather than once per test.
        Assert.True(migrations > 0);
    }
}
