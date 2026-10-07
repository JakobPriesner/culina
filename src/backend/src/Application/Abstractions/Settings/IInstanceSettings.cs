namespace Application.Abstractions.Settings;

/// <summary>
/// A settings group an admin edits from the app's own screens, stored in the database rather than a
/// file.
/// </summary>
/// <remarks>
/// They change while the process runs, so the record is a mutable singleton every consumer holds
/// and an update mutates it in place: no cache, no re-resolution, no restart.
/// </remarks>
public interface IInstanceSettings<TSelf>
    where TSelf : class, IInstanceSettings<TSelf>
{
    /// <summary>The key this group is stored under.</summary>
    static abstract string GroupName { get; }

    /// <summary>Copies another instance's values onto this one.</summary>
    /// <remarks>
    /// By hand rather than reflection, used by both the loader and the update handler, so the copy
    /// exists once per group.
    /// </remarks>
    void CopyFrom(TSelf other);
}
