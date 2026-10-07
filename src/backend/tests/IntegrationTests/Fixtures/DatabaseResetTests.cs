using Infrastructure.Persistence;

namespace IntegrationTests.Fixtures;

[Collection(RequiresDatabase.Name)]
public class DatabaseResetTests(PostgresFixture postgres)
{
    [Fact]
    public async Task ResetAsync_ShouldRetry_WhenPostgresRollsBackTheFirstTruncate()
    {
        _ = postgres.Api.Services;
        await using var session = postgres.NewSession();
        var executor = new DbExecutor(session);
        var cancellationToken = TestContext.Current.CancellationToken;

        // A sequence survives a statement rollback, making a transient
        // deadlock deterministic without racing a real background reader.
        await executor.ExecuteAsync(
            """
            create sequence public.reset_deadlock_count;
            create function public.refuse_first_reset() returns trigger language plpgsql as $$
            begin
                if nextval('public.reset_deadlock_count') = 1 then
                    raise exception 'reset was rolled back' using errcode = '40P01';
                end if;
                return null;
            end;
            $$;
            create trigger refuse_first_reset before truncate on settings
                for each statement execute function public.refuse_first_reset();
            insert into settings (group_name, payload) values ('reset_probe', '{}'::jsonb);
            """,
            null,
            cancellationToken);

        try
        {
            await postgres.ResetAsync(cancellationToken);

            Assert.Equal(0, await executor.ExecuteScalarAsync<int>(
                "select count(*) from settings;", null, cancellationToken));
            Assert.Equal(2L, await executor.ExecuteScalarAsync<long>(
                "select last_value from public.reset_deadlock_count;", null, cancellationToken));
        }
        finally
        {
            await executor.ExecuteAsync(
                """
                drop function public.refuse_first_reset() cascade;
                drop sequence public.reset_deadlock_count;
                """,
                null,
                cancellationToken);
        }
    }

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
