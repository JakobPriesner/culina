using System.Diagnostics;
using Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence.Migrations;

/// <summary>
/// Applies pending migrations, forward only.
/// </summary>
/// <remarks>
/// The migrations to apply arrive as a parameter rather than being read here,
/// which is what lets a test drive the runner with its own set without an
/// interface existing solely for the test.
/// </remarks>
/// <param name="executor">Runs the SQL.</param>
/// <param name="unitOfWork">Makes each migration atomic.</param>
/// <param name="logger">Records what was applied.</param>
internal sealed class MigrationRunner(
    DbExecutor executor,
    IUnitOfWork unitOfWork,
    ILogger<MigrationRunner> logger)
{
    /// <summary>
    /// An arbitrary but fixed key, so every Culina process competes for the
    /// same lock. Rolling restarts and replicas must not migrate concurrently.
    /// </summary>
    private const long AdvisoryLockKey = 0x63756C69; // "culi"

    private const string HistoryTable = """
        create table if not exists schema_migrations (
            version    text        not null primary key,
            checksum   text        not null,
            applied_at timestamptz not null default now()
        );
        """;

    internal async Task ApplyAsync(
        IReadOnlyList<SqlMigration> migrations,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(migrations);

        // Session-scoped, so it survives the per-migration transactions below
        // and is released when the connection closes.
        await executor.ExecuteAsync(
            "select pg_advisory_lock(@key);",
            new { key = AdvisoryLockKey },
            cancellationToken).ConfigureAwait(false);

        try
        {
            await executor.ExecuteAsync(HistoryTable, null, cancellationToken).ConfigureAwait(false);

            var applied = await AppliedAsync(cancellationToken).ConfigureAwait(false);
            var pending = new List<SqlMigration>();

            foreach (var migration in migrations)
            {
                if (!applied.TryGetValue(migration.Version, out var checksum))
                {
                    pending.Add(migration);
                    continue;
                }

                if (checksum != migration.Checksum)
                {
                    logger.ChecksumMismatch(migration.Version);

                    throw new InvalidOperationException(
                        $"Migration {migration.Version} has already been applied but its contents "
                        + "have changed. Restore the file and add a new migration instead.");
                }
            }

            foreach (var migration in pending)
            {
                await ApplyOneAsync(migration, cancellationToken).ConfigureAwait(false);
            }

            if (pending.Count == 0 && migrations.Count > 0)
            {
                logger.UpToDate(migrations[^1].Version);
            }
        }
        finally
        {
            await executor.ExecuteAsync(
                "select pg_advisory_unlock(@key);",
                new { key = AdvisoryLockKey },
                CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task<Dictionary<string, string>> AppliedAsync(CancellationToken cancellationToken)
    {
        var rows = await executor.QueryAsync<(string Version, string Checksum)>(
            "select version, checksum from schema_migrations;",
            null,
            cancellationToken).ConfigureAwait(false);

        return rows.ToDictionary(row => row.Version, row => row.Checksum, StringComparer.Ordinal);
    }

    private async Task ApplyOneAsync(SqlMigration migration, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Each migration commits on its own, so a failure halfway through a
            // set leaves the earlier ones applied and recorded rather than
            // silently rolling them back on the next start.
            await unitOfWork.InTransactionAsync(
                async token =>
                {
                    await executor.ExecuteAsync(migration.Sql, null, token).ConfigureAwait(false);

                    await executor.ExecuteAsync(
                        """
                        insert into schema_migrations (version, checksum)
                        values (@version, @checksum);
                        """,
                        new { version = migration.Version, checksum = migration.Checksum },
                        token).ConfigureAwait(false);

                    return true;
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.Failed(migration.Version, exception);

            throw;
        }

        logger.Applied(migration.Version, stopwatch.ElapsedMilliseconds);
    }
}
