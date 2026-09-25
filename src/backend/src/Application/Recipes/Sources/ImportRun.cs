using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Contracts.Recipes.Sources;

namespace Application.Recipes.Sources;

/// <summary>
/// One import, while it runs and for a while after it has stopped.
/// </summary>
/// <remarks>
/// <para>
/// The import belongs to the server, not to the tab that asked for it. That is
/// the whole point of this type: the person who started four hundred recipes
/// can close the laptop, come back, and the recipes are there — because nothing
/// about the work depends on somebody watching it.
/// </para>
/// <para>
/// So a watcher is a reader of what has already happened, not a subscriber that
/// has to be present when it does. Every outcome is kept in order, and
/// <see cref="WatchAsync"/> replays from wherever the caller left off before it
/// waits for anything new. A reconnect loses nothing and repeats nothing.
/// </para>
/// </remarks>
public sealed class ImportRun
{
    /// <summary>
    /// How long a stream may say nothing before it says nothing out loud.
    /// </summary>
    /// <remarks>
    /// A recipe can take a while — somebody else's server, then a photo — and a
    /// connection that is silent for minutes is a connection a proxy will close
    /// out from under both ends. A tick costs one line and keeps it open.
    /// </remarks>
    private static readonly TimeSpan Heartbeat = TimeSpan.FromSeconds(15);

    private readonly Lock gate = new();
    private readonly List<ImportedRecipe> outcomes = [];

    /// <summary>
    /// Completed whenever something is recorded, and replaced immediately.
    /// </summary>
    /// <remarks>
    /// Handed out under the same lock that takes the snapshot of what has
    /// happened so far, which is what makes a watcher impossible to wake up
    /// late: anything recorded after the snapshot completes the very task the
    /// watcher is about to await.
    /// </remarks>
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

    /// <summary>
    /// Whether a recipe that looks like one already here is written anyway.
    /// </summary>
    /// <remarks>
    /// Only ever true for a run somebody started after being shown what each
    /// of its recipes looks like.
    /// </remarks>
    public bool AllowLookalikes { get; init; }

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

    /// <summary>Says the run is over, however it went.</summary>
    public void Finish()
    {
        lock (gate)
        {
            finished = true;
            Wake();
        }
    }

    /// <summary>
    /// Everything that has happened since <paramref name="from"/>, and then
    /// everything that happens next.
    /// </summary>
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
                // Nothing to report, said out loud so the connection survives to
                // report something later.
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
/// </summary>
/// <remarks>
/// <para>
/// One type rather than two, because a registry of runs and a queue of work are
/// the same fact here: a run exists from the moment it is accepted, and the
/// worker's job is to pick up the ones nobody has started yet.
/// </para>
/// <para>
/// In memory, deliberately. An import is minutes of work against somebody
/// else's server, not days, and the property that makes it safe to lose is the
/// one the database already gives: a recipe that arrived has an origin row, so
/// asking for the same selection again brings over exactly what is missing. A
/// job table would buy resumption across a restart and cost a schema, a poller
/// and a new way for the system to be half-finished.
/// </para>
/// </remarks>
/// <param name="time">The injected clock, for forgetting old runs.</param>
public sealed class ImportRuns(TimeProvider time)
{
    /// <summary>
    /// How long a finished run stays readable.
    /// </summary>
    /// <remarks>
    /// Long enough that somebody who lost their connection at the end can
    /// reconnect and see how it went; short enough that a busy instance is not
    /// remembering last week.
    /// </remarks>
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
