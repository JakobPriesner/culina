using Dapper;
using Npgsql;

namespace IntegrationTests.Fixtures;

/// <summary>Returns the database to an empty-but-migrated state between tests.</summary>
/// <remarks>
/// A truncate rather than a rollback: a wrapping transaction would hide everything that happens at commit
/// (deferred constraints, racing unique violations, cascade behaviour).
/// </remarks>
internal static class DatabaseReset
{
    internal static async Task TruncateAllAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var tables = await connection.QueryAsync<string>(
            new CommandDefinition(
                """
                select table_name
                from information_schema.tables
                where table_schema = 'public'
                  and table_type = 'BASE TABLE'
                  -- Keeping the migration history means the schema is built
                  -- once per run rather than once per test.
                  and table_name <> 'schema_migrations';
                """,
                cancellationToken: cancellationToken));

        var names = tables.ToList();

        if (names.Count == 0)
        {
            return;
        }

        var list = string.Join(", ", names.Select(name => $"public.{name}"));

        // A hosted notification reader can lock these tables in another order; PostgreSQL rolls the whole
        // TRUNCATE back on deadlock, so retry that statement only.
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    $"truncate table {list} restart identity cascade;",
                    cancellationToken: cancellationToken));

                return;
            }
            catch (PostgresException failure) when (
                failure.SqlState == PostgresErrorCodes.DeadlockDetected && attempt < 2)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
            }
        }
    }
}
