using Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Identity;

/// <summary>
/// Deletes sessions that have lapsed or been revoked.
/// </summary>
/// <remarks>
/// Housekeeping, not security: an expired session already fails to
/// authenticate. Without it the table grows forever on a long-lived instance,
/// and the "your devices" query slows down for no reason.
/// </remarks>
/// <param name="scopeFactory">Creates the scope the store lives in.</param>
/// <param name="time">The injected clock.</param>
/// <param name="logger">Records how many rows were removed.</param>
internal sealed class ExpiredSessionSweeper(
    IServiceScopeFactory scopeFactory,
    TimeProvider time,
    ILogger<ExpiredSessionSweeper> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, time);

        // A first pass shortly after start, then daily. Waiting a full day
        // before the first sweep would leave a restarted instance carrying
        // whatever accumulated while it was down.
        do
        {
            await SweepAsync(stoppingToken).ConfigureAwait(false);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        var scope = scopeFactory.CreateAsyncScope();

        await using (scope.ConfigureAwait(false))
        {
            var sessions = scope.ServiceProvider.GetRequiredService<ISessionStore>();

            var removed = await sessions
                .DeleteExpiredAsync(time.GetUtcNow(), cancellationToken)
                .ConfigureAwait(false);

            if (removed > 0)
            {
                IdentityLogs.SweptSessions(logger, removed);
            }
        }
    }
}
