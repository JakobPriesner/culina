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
    public IntakeSubscription Subscribe(Guid userId)
    {
        var subscription = new IntakeSubscription(userId, Remove);

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

    internal IntakeSubscription(Guid userId, Action<IntakeSubscription> leave)
    {
        UserId = userId;
        this.leave = leave;
    }

    internal Guid UserId { get; }

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
