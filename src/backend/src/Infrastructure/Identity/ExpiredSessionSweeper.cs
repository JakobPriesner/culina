using Application.Abstractions;
using Application.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Identity;

/// <summary>Deletes sessions that have lapsed or been revoked, household invitations that expired unused, and intake jobs and push outbox rows long finished. Housekeeping, not security: neither works once expired.</summary>
internal sealed class ExpiredSessionSweeper(
    IServiceScopeFactory scopeFactory,
    TimeProvider time,
    ILogger<ExpiredSessionSweeper> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Housekeeping must not stop the host: a failed sweep is logged and the next tick tries again.")]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, time);

        // A first pass shortly after start, then daily, so a restarted instance does not carry a day's backlog.
        do
        {
            try
            {
                await SweepAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                IdentityLogs.SweepFailed(logger, exception);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        // No request to hang off, so the sweep is a trace of its own.
        using var activity = CulinaTelemetry.ActivitySource.StartActivity("Sessions.Sweep");

        var scope = scopeFactory.CreateAsyncScope();

        await using (scope.ConfigureAwait(false))
        {
            var now = time.GetUtcNow();
            var sessions = scope.ServiceProvider.GetRequiredService<ISessionStore>();
            var invitations = scope.ServiceProvider.GetRequiredService<IInvitationRepository>();

            var removed = await sessions
                .DeleteExpiredAsync(now, cancellationToken)
                .ConfigureAwait(false);

            if (removed > 0)
            {
                IdentityLogs.SweptSessions(logger, removed);
            }

            var lapsed = await invitations
                .DeleteExpiredAsync(now, cancellationToken)
                .ConfigureAwait(false);

            if (lapsed > 0)
            {
                IdentityLogs.SweptInvitations(logger, lapsed);
            }

            var purged = await scope.ServiceProvider.GetRequiredService<IIntakePurge>()
                .PurgeAsync(now, cancellationToken)
                .ConfigureAwait(false);

            if (purged.Jobs + purged.Notifications > 0)
            {
                IdentityLogs.SweptIntake(logger, purged.Jobs, purged.Notifications);
            }
        }
    }
}
