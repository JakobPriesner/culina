using Application.Abstractions;
using Application.Recipes.Intake;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Import;

internal sealed partial class RecipeIntakeWorker(IServiceScopeFactory scopes, ILogger<RecipeIntakeWorker> logger) : BackgroundService
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A durable worker records failures and keeps serving the next job; exceptions are logged.")]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
                        await scope.ServiceProvider.GetRequiredService<RecipeIntake>().ProcessAsync(work, deadline.Token).ConfigureAwait(false);
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
                await scope.ServiceProvider.GetRequiredService<IIntakeNotifications>().DeliverAsync(stoppingToken).ConfigureAwait(false);
                if (work is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken).ConfigureAwait(false);
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
    [LoggerMessage(Level = LogLevel.Error, Message = "Recipe intake {JobId} failed.")]
    private static partial void ImportFailed(ILogger logger, Exception exception, Guid jobId);
    [LoggerMessage(Level = LogLevel.Error, Message = "Recipe intake worker failed; retrying.")]
    private static partial void WorkerFailed(ILogger logger, Exception exception);
}
