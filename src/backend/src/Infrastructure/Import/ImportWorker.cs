using Application.Recipes.Sources;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Import;

/// <summary>
/// Takes queued runs and hands them to the runner, on the host's lifetime. Runs go on concurrently; each
/// already bounds its own fetches.
/// </summary>
/// <param name="runs">The queue of accepted imports.</param>
/// <param name="runner">Does one import.</param>
internal sealed class ImportWorker(ImportRuns runs, SourceImportRunner runner) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        List<Task> running = [];

        try
        {
            await foreach (var run in runs.QueuedAsync(stoppingToken).ConfigureAwait(false))
            {
                running.RemoveAll(one => one.IsCompleted);
                running.Add(runner.RunAsync(run, stoppingToken));
            }
        }
        catch (OperationCanceledException)
        {
            // The host is stopping.
        }

        // Never throws: a run reports failures as outcomes.
        await Task.WhenAll(running).ConfigureAwait(false);
    }
}
