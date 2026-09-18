using Application.Recipes.Sources;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Import;

/// <summary>
/// The background process that does the imports people ask for.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately almost empty. All it does is take runs off the queue and hand
/// them to the runner — the deciding, the fetching and the writing are use-case
/// work and live in <c>Application</c>. What it contributes is the one thing
/// only a hosted service can: a lifetime that is the host's rather than a
/// request's, and a token that says when to stop.
/// </para>
/// <para>
/// Runs go on together rather than one after another. Each already limits how
/// many recipes it fetches at a time, so the load of a second import is bounded
/// — and somebody who starts one while a colleague's eight hundred are still
/// arriving should not watch a bar that says nothing for ten minutes.
/// </para>
/// </remarks>
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
            // The host is stopping, which is the ordinary way this ends.
        }

        // Never throws: a run reports its own failures as outcomes and always
        // finishes, so this is a wait rather than a risk.
        await Task.WhenAll(running).ConfigureAwait(false);
    }
}
