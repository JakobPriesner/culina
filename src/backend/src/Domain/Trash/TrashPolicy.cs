namespace Domain.Trash;

/// <summary>How long something deleted waits in its household's bin.</summary>
/// <remarks>
/// Deleted is hidden, not removed, and can be restored until retention ends, then purged with its
/// photographs: thirty days is long enough to notice, short enough that deleted still means
/// deleted. A constant, not a setting: a longer bin is a backup, a different promise.
/// </remarks>
public static class TrashPolicy
{
    /// <summary>How long something deleted can still be restored.</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    /// <summary>When something deleted at <paramref name="deletedAt"/> will be purged.</summary>
    public static DateTimeOffset PurgeAfter(DateTimeOffset deletedAt) => deletedAt.Add(Retention);

    /// <summary>Everything deleted before this moment is due to be purged.</summary>
    public static DateTimeOffset PurgeCutoff(DateTimeOffset now) => now.Subtract(Retention);
}
