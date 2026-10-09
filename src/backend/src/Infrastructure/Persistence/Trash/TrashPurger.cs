using Application.Abstractions.Messaging;
using Application.Telemetry;
using Application.Trash.Purge;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence.Trash;

/// <summary>Removes, once a day, what has been in a bin for longer than the retention.</summary>
/// <remarks>
/// Daily is precise enough for "at least thirty days". A first pass runs shortly after start, so an
/// instance that was down for a week does not carry overdue rows until tomorrow.
/// </remarks>
internal sealed class TrashPurger(
    IServiceScopeFactory scopeFactory,
    TimeProvider time,
    ILogger<TrashPurger> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Housekeeping must not stop the host: a failed purge is logged and the next tick tries again.")]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, time);

        do
        {
            try
            {
                await PurgeAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                TrashLogs.PurgeThrew(logger, exception);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private async Task PurgeAsync(CancellationToken cancellationToken)
    {
        // No request to hang off, so the purge is a trace of its own.
        using var activity = CulinaTelemetry.ActivitySource.StartActivity("Trash.Sweep");

        var scope = scopeFactory.CreateAsyncScope();

        await using (scope.ConfigureAwait(false))
        {
            var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<PurgeTrashCommand, int>>();

            var result = await handler
                .Handle(new PurgeTrashCommand(time.GetUtcNow()), cancellationToken)
                .ConfigureAwait(false);

            result.Match(
                removed =>
                {
                    if (removed > 0)
                    {
                        TrashLogs.Purged(logger, removed);
                    }
                },
                error => TrashLogs.PurgeFailed(logger, error.Code));
        }
    }
}
