using Application.Abstractions;
using Contracts.Recipes.Intake;

namespace Application.Recipes.Intake;

/// <summary>Follows one person's imports: what they are now, then each one as it changes.</summary>
public sealed class IntakeWatch(IRecipeIntakeJobs jobs, IntakeChanges changes)
{
    /// <summary>A silent connection gets closed by proxies; a tick keeps it open.</summary>
    public static readonly TimeSpan Heartbeat = TimeSpan.FromSeconds(15);

    /// <summary>A snapshot of every import, then the ones that change; an event with none is a heartbeat.</summary>
    /// <param name="userId">Whose imports; nobody else's are ever read.</param>
    /// <param name="heartbeat">How long a silent stream waits before it says something.</param>
    /// <param name="token">Ends the watch, never an import.</param>
    public async IAsyncEnumerable<IntakeEvent> WatchAsync(
        Guid userId,
        TimeSpan heartbeat,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
    {
        // Subscribed first, so a change while the snapshot is read is not missed.
        using var subscription = changes.Subscribe(userId);

        yield return new IntakeEvent
        {
            Snapshot = true,
            Jobs = await jobs.ListAsync(userId, token).ConfigureAwait(false)
        };

        while (true)
        {
            var ids = await subscription.NextAsync(heartbeat, token).ConfigureAwait(false);
            List<IntakeJob> fresh = [];

            foreach (var id in ids)
            {
                if (await jobs.GetAsync(id, userId, token).ConfigureAwait(false) is { } job)
                {
                    fresh.Add(job);
                }
            }

            yield return new IntakeEvent { Snapshot = false, Jobs = fresh };
        }
    }
}
