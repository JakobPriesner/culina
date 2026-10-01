namespace Domain.Trash;

/// <summary>
/// How long something deleted waits in its household's bin.
/// </summary>
/// <remarks>
/// <para>
/// A household, a recipe or a cookbook that is deleted is hidden, not
/// removed, and can be restored by the people it belonged to until the
/// retention ends; then it is purged with its photographs. Thirty days is long
/// enough to notice a recipe that a housemate deleted — the kind of thing you
/// find out about the next time you want to cook it — and short enough that
/// "deleted" still means deleted.
/// </para>
/// <para>
/// A constant rather than a setting: an operator who wanted a longer bin would
/// be asking for a backup, which is a different promise and already exists.
/// </para>
/// </remarks>
public static class TrashPolicy
{
    /// <summary>How long something deleted can still be restored.</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    /// <summary>When something deleted at <paramref name="deletedAt"/> will be purged.</summary>
    /// <param name="deletedAt">When it was deleted.</param>
    public static DateTimeOffset PurgeAfter(DateTimeOffset deletedAt) => deletedAt.Add(Retention);

    /// <summary>Everything deleted before this moment is due to be purged.</summary>
    /// <param name="now">The injected current time.</param>
    public static DateTimeOffset PurgeCutoff(DateTimeOffset now) => now.Subtract(Retention);
}
