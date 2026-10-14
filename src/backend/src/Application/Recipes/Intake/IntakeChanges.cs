using System.Threading.Channels;

namespace Application.Recipes.Intake;

/// <summary>
/// Tells whoever is watching a person's imports that one of them changed. In memory by design: the
/// database stays the truth, a watcher re-reads, and a restart is covered by the snapshot a reconnect gets.
/// </summary>
public sealed class IntakeChanges
{
    private readonly Lock gate = new();
    private readonly Dictionary<Guid, List<IntakeSubscription>> watchers = [];

    // One pending wake is all the worker needs; it claims until the queue is empty.
    private readonly Channel<bool> queued = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>Says work was queued, so an idle worker claims it now rather than at its next poll.</summary>
    public void Queued() => queued.Writer.TryWrite(true);

    /// <summary>Waits until work is queued or <paramref name="poll"/> passes; the poll still finds work queued elsewhere.</summary>
    /// <param name="poll">The longest wait.</param>
    /// <param name="token">Stops the worker.</param>
    public async Task WaitForWorkAsync(TimeSpan poll, CancellationToken token)
    {
        using var tick = CancellationTokenSource.CreateLinkedTokenSource(token);
        tick.CancelAfter(poll);

        try
        {
            await queued.Reader.ReadAsync(tick.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            // The poll passed.
        }
    }

    /// <summary>Whether an open stream is attending this draft: a page showing it, so nobody needs telling it is ready.</summary>
    /// <param name="jobId">Which draft.</param>
    public bool Attended(Guid jobId)
    {
        lock (gate)
        {
            return watchers.Values.Any(list => list.Any(subscription => subscription.Attending == jobId));
        }
    }

    /// <summary>Says an import changed, to every stream open for its owner. Never waits and never fails.</summary>
    /// <param name="userId">Whose import.</param>
    /// <param name="jobId">Which import.</param>
    public void Changed(Guid userId, Guid jobId)
    {
        IntakeSubscription[] open;

        lock (gate)
        {
            open = watchers.TryGetValue(userId, out var found) ? [.. found] : [];
        }

        foreach (var subscription in open)
        {
            subscription.Add(jobId);
        }
    }

    /// <summary>Starts listening for one person's imports; dispose it when the stream ends.</summary>
    /// <param name="userId">Whose imports.</param>
    /// <param name="attending">The draft the page shows, if any; see <see cref="Attended"/>.</param>
    public IntakeSubscription Subscribe(Guid userId, Guid? attending = null)
    {
        var subscription = new IntakeSubscription(userId, attending, Remove);

        lock (gate)
        {
            if (!watchers.TryGetValue(userId, out var list))
            {
                watchers[userId] = list = [];
            }

            list.Add(subscription);
        }

        return subscription;
    }

    private void Remove(IntakeSubscription subscription)
    {
        lock (gate)
        {
            if (watchers.TryGetValue(subscription.UserId, out var list)
                && list.Remove(subscription)
                && list.Count == 0)
            {
                watchers.Remove(subscription.UserId);
            }
        }
    }
}

/// <summary>The imports that changed since a stream last asked; a slow stream gets one batch, never a backlog.</summary>
public sealed class IntakeSubscription : IDisposable
{
    private readonly Lock gate = new();
    private readonly HashSet<Guid> changed = [];
    private readonly Action<IntakeSubscription> leave;

    // One pending wake is all a reader needs; further ones are dropped.
    private readonly Channel<bool> signal = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    internal IntakeSubscription(Guid userId, Guid? attending, Action<IntakeSubscription> leave)
    {
        UserId = userId;
        Attending = attending;
        this.leave = leave;
    }

    internal Guid UserId { get; }

    internal Guid? Attending { get; }

    internal void Add(Guid jobId)
    {
        lock (gate)
        {
            changed.Add(jobId);
        }

        signal.Writer.TryWrite(true);
    }

    /// <summary>Waits for changes; returns none once <paramref name="heartbeat"/> passes without any.</summary>
    /// <param name="heartbeat">How long a silent wait lasts.</param>
    /// <param name="token">Ends the wait when the stream does.</param>
    public async Task<IReadOnlyList<Guid>> NextAsync(TimeSpan heartbeat, CancellationToken token)
    {
        using var tick = CancellationTokenSource.CreateLinkedTokenSource(token);
        tick.CancelAfter(heartbeat);

        try
        {
            await signal.Reader.ReadAsync(tick.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            return [];
        }

        lock (gate)
        {
            var ids = changed.ToArray();
            changed.Clear();

            return ids;
        }
    }

    /// <summary>Stops listening.</summary>
    public void Dispose() => leave(this);
}
