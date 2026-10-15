using Application.Abstractions;

namespace Infrastructure.Persistence.Import;

/// <summary>
/// Keeps the intake tables from growing for ever. A reviewed or failed job is kept a month, enough to look back at;
/// a notification delivered or given up on (twelve attempts, as the delivery loop counts) is kept a week.
/// </summary>
internal sealed class IntakePurge(DbExecutor db) : IIntakePurge
{
    internal static readonly TimeSpan JobRetention = TimeSpan.FromDays(30);

    internal static readonly TimeSpan NotificationRetention = TimeSpan.FromDays(7);

    public async Task<IntakePurged> PurgeAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var spentBefore = now - NotificationRetention;

        // Outbox rows first: they name jobs that may stay, and a job's own delete takes the rest with it (cascade).
        var web = await db.ExecuteAsync(
                """
                delete from recipe_intake_notifications n using recipe_intake_jobs j
                where j.id = n.job_id and j.updated_at < @SpentBefore and (n.delivered_at is not null or n.attempts >= 12)
                """,
                new { SpentBefore = spentBefore },
                cancellationToken)
            .ConfigureAwait(false);

        var devices = await db.ExecuteAsync(
                """
                delete from recipe_intake_device_notifications n using recipe_intake_jobs j
                where j.id = n.job_id and j.updated_at < @SpentBefore and (n.delivered_at is not null or n.attempts >= 12)
                """,
                new { SpentBefore = spentBefore },
                cancellationToken)
            .ConfigureAwait(false);

        var jobs = await db.ExecuteAsync(
                "delete from recipe_intake_jobs where stage in ('reviewed', 'failed') and updated_at < @JobsBefore",
                new { JobsBefore = now - JobRetention },
                cancellationToken)
            .ConfigureAwait(false);

        return new IntakePurged(jobs, web + devices);
    }
}
