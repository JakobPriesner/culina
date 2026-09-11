using Dapper;
using Npgsql;

namespace IntegrationTests.Fixtures;

/// <summary>
/// Returns the database to an empty-but-migrated state between tests.
/// </summary>
/// <remarks>
/// A truncate rather than a transaction rollback. Wrapping each test in a
/// transaction is faster, but it hides everything that only happens at commit —
/// deferred constraints, unique violations that race, and the trigger-free
/// behaviour of <c>on delete cascade</c>. Those are exactly the things an
/// integration test is for.
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

        await connection.ExecuteAsync(new CommandDefinition(
            $"truncate table {list} restart identity cascade;",
            cancellationToken: cancellationToken));
    }
}
