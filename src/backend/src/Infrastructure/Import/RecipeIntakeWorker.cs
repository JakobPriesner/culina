using Application.Abstractions;
using Application.Recipes.Intake;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Import;

internal sealed partial class RecipeIntakeWorker(IServiceScopeFactory scopes, IntakeChanges changes, ILogger<RecipeIntakeWorker> logger) : BackgroundService
{
    /// <summary>
    /// Jobs written at once. A composer draft is somebody watching a recipe appear, so it must not queue behind another
    /// person's import; the claim skips locked rows, so the loops never take the same job.
    /// </summary>
    internal const int Loops = 3;

    /// <summary>How long an idle loop waits before looking again, when nothing told it work was queued.</summary>
    private static readonly TimeSpan Poll = TimeSpan.FromSeconds(2);

    // Delivery has a loop of its own, so a ten-minute import never holds up another's "ready".
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(Enumerable.Range(0, Loops).Select(_ => WorkAsync(stoppingToken)).Append(DeliverAsync(stoppingToken)));

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A durable worker records failures and keeps serving the next job; exceptions are logged.")]
    private async Task WorkAsync(CancellationToken stoppingToken)
    {
        // Off the host's start-up path: ExecuteAsync must return before the first job runs.
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var scope = scopes.CreateAsyncScope();
                await using var lifetime = scope.ConfigureAwait(false);
                var jobs = scope.ServiceProvider.GetRequiredService<IRecipeIntakeJobs>();
                var work = await jobs.ClaimAsync(stoppingToken).ConfigureAwait(false);
                if (work is not null)
                {
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    deadline.CancelAfter(TimeSpan.FromMinutes(10));
                    try
                    {
                        await scope.ServiceProvider.GetRequiredService<IntakeProcessor>().ProcessAsync(work, deadline.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                    {
                        await jobs.FailAsync(work.Id, "RecipeIntake.TimedOut", stoppingToken).ConfigureAwait(false);
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        ImportFailed(logger, exception, work.Id);
                        await jobs.FailAsync(work.Id, "RecipeIntake.Failed", stoppingToken).ConfigureAwait(false);
                    }
                }

                if (work is null)
                {
                    await changes.WaitForWorkAsync(Poll, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                WorkerFailed(logger, exception);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
            }
        }
    }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Delivery is retried by its outbox; exceptions are logged.")]
    private async Task DeliverAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var scope = scopes.CreateAsyncScope();
                await using (scope.ConfigureAwait(false))
                {
                    await scope.ServiceProvider.GetRequiredService<IIntakeNotifications>().DeliverAsync(stoppingToken).ConfigureAwait(false);
                }

                await Task.Delay(Poll, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                WorkerFailed(logger, exception);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Recipe intake {JobId} failed.")]
    private static partial void ImportFailed(ILogger logger, Exception exception, Guid jobId);
    [LoggerMessage(Level = LogLevel.Error, Message = "Recipe intake worker failed; retrying.")]
    private static partial void WorkerFailed(ILogger logger, Exception exception);
}
