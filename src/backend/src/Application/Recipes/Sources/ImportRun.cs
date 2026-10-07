using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Contracts.Recipes.Sources;
using Domain.Shared;

namespace Application.Recipes.Sources;

/// <summary>
/// One import, owned by the server rather than the watching tab. Outcomes are kept in order, so
/// <see cref="WatchAsync"/> replays from where a caller left off: a reconnect loses and repeats nothing.
/// </summary>
public sealed class ImportRun
{
    // A silent connection gets closed by proxies; a tick keeps it open.
    private static readonly TimeSpan Heartbeat = TimeSpan.FromSeconds(15);

    private readonly Lock gate = new();
    private readonly List<ImportedRecipe> outcomes = [];

    // Replaced on every wake. Handed out under the same lock as the outcomes snapshot, so a watcher cannot miss a wake.
    private TaskCompletionSource changed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private bool finished;

    /// <summary>Starts a run. It does no work until the worker picks it up.</summary>
    /// <param name="sourceId">The connection the recipes come from.</param>
    /// <param name="userId">Who asked for them.</param>
    /// <param name="cookbookId">The shelf they are landing on.</param>
    /// <param name="cookbookName">What that shelf is called.</param>
    /// <param name="externalIds">Which of their recipes, in the order asked for.</param>
    /// <param name="startedAt">When it was asked for.</param>
    public ImportRun(
        Guid sourceId,
        Guid userId,
        Guid cookbookId,
        string cookbookName,
        IReadOnlyList<string> externalIds,
        DateTimeOffset startedAt)
    {
        SourceId = sourceId;
        UserId = userId;
        CookbookId = cookbookId;
        CookbookName = cookbookName;
        ExternalIds = externalIds;
        StartedAt = startedAt;
    }

    /// <summary>Which import.</summary>
    public Guid Id { get; } = Guid.CreateVersion7();

    /// <summary>The connection being read.</summary>
    public Guid SourceId { get; }

    /// <summary>Who asked, and so who may watch.</summary>
    public Guid UserId { get; }

    /// <summary>The cookbook everything lands on.</summary>
    public Guid CookbookId { get; }

    /// <summary>What that cookbook is called.</summary>
    public string CookbookName { get; }

    /// <summary>Which of their recipes were asked for.</summary>
    public IReadOnlyList<string> ExternalIds { get; }

    /// <summary>When it was asked for. Used to forget it later.</summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>Whether a recipe that looks like one already here is written anyway.</summary>
    public bool AllowLookalikes { get; init; }

    /// <summary>The asking device's language, used when the person's own reads "as the device".</summary>
    public Language DeviceLanguage { get; init; }

    /// <summary>How many recipes were asked for.</summary>
    public int Total => ExternalIds.Count;

    /// <summary>True once every recipe has been tried.</summary>
    public bool Finished
    {
        get
        {
            lock (gate)
            {
                return finished;
            }
        }
    }

    /// <summary>Notes what happened to one recipe, and wakes everyone watching.</summary>
    /// <param name="outcome">Imported, already here, or failed.</param>
    public void Record(ImportedRecipe outcome)
    {
        lock (gate)
        {
            outcomes.Add(outcome);
            Wake();
        }
    }

    /// <summary>How many recipes have ended with <paramref name="outcome"/> so far.</summary>
    /// <param name="outcome">An outcome as recorded, such as <c>imported</c>.</param>
    public int Count(string outcome)
    {
        lock (gate)
        {
            return outcomes.Count(one => one.Outcome == outcome);
        }
    }

    /// <summary>Says the run is over, however it went.</summary>
    public void Finish()
    {
        lock (gate)
        {
            finished = true;
            Wake();
        }
    }

    /// <summary>Everything that has happened since <paramref name="from"/>, then everything that happens next.</summary>
    /// <param name="from">How many outcomes the caller already has.</param>
    /// <param name="cancellationToken">Ends the watch. Never the run.</param>
    /// <returns>One event per recipe, then one final event with no recipe on it.</returns>
    public async IAsyncEnumerable<ImportEvent> WatchAsync(
        int from,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var sent = Math.Clamp(from, 0, Total);

        while (true)
        {
            List<ImportedRecipe> fresh;
            Task waiting;
            bool over;

            lock (gate)
            {
                fresh = outcomes.Skip(sent).ToList();
                waiting = changed.Task;
                over = finished;
            }

            foreach (var outcome in fresh)
            {
                sent += 1;

                yield return new ImportEvent
                {
                    Recipe = outcome,
                    Done = sent,
                    Total = Total,
                    Finished = false
                };
            }

            if (over)
            {
                yield return new ImportEvent { Done = sent, Total = Total, Finished = true };

                yield break;
            }

            var silent = false;

            try
            {
                await waiting.WaitAsync(Heartbeat, cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                silent = true;
            }

            if (silent)
            {
                // Heartbeat: keeps the connection alive.
                yield return new ImportEvent { Done = sent, Total = Total, Finished = false };
            }
        }
    }

    private void Wake()
    {
        var waiting = changed;

        changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        waiting.TrySetResult();
    }
}

/// <summary>
/// The imports this instance is running, and the queue the worker reads.
/// In memory by design: a lost run is recovered by re-requesting, since arrived recipes have an origin row.
/// </summary>
/// <param name="time">The injected clock, for forgetting old runs.</param>
public sealed class ImportRuns(TimeProvider time)
{
    // Long enough to reconnect and see the result, short enough not to hoard.
    private static readonly TimeSpan Remembered = TimeSpan.FromMinutes(30);

    private readonly Dictionary<Guid, ImportRun> runs = [];
    private readonly Lock gate = new();

    private readonly Channel<ImportRun> waiting =
        Channel.CreateUnbounded<ImportRun>(new UnboundedChannelOptions { SingleReader = true });

    /// <summary>Registers a run and queues it for the worker.</summary>
    /// <param name="run">The import to start.</param>
    public void Start(ImportRun run)
    {
        ArgumentNullException.ThrowIfNull(run);

        lock (gate)
        {
            Forget();

            runs[run.Id] = run;
        }

        waiting.Writer.TryWrite(run);
    }

    /// <summary>Finds a run to watch, or null when there is no such import.</summary>
    /// <param name="importId">Which import.</param>
    public ImportRun? Find(Guid importId)
    {
        lock (gate)
        {
            return runs.GetValueOrDefault(importId);
        }
    }

    /// <summary>The runs waiting to be done, in the order they were asked for.</summary>
    /// <param name="cancellationToken">Ends the wait when the host stops.</param>
    public IAsyncEnumerable<ImportRun> QueuedAsync(CancellationToken cancellationToken) =>
        waiting.Reader.ReadAllAsync(cancellationToken);

    private void Forget()
    {
        var cutoff = time.GetUtcNow() - Remembered;

        var over = runs.Values
            .Where(run => run.Finished && run.StartedAt < cutoff)
            .Select(run => run.Id)
            .ToList();

        foreach (var id in over)
        {
            runs.Remove(id);
        }
    }
}
