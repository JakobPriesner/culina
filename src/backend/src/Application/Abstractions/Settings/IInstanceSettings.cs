namespace Application.Abstractions.Settings;

/// <summary>
/// A settings group an admin edits from the app's own screens, stored in the
/// database rather than in a file.
/// </summary>
/// <typeparam name="TSelf">The implementing record.</typeparam>
/// <remarks>
/// Instance settings differ from bootstrap settings in one way that drives
/// everything else: they change while the process runs. So the record is a
/// mutable singleton every consumer already holds, and an update mutates it in
/// place — no cache to invalidate, no re-resolution, no restart.
/// </remarks>
public interface IInstanceSettings<TSelf>
    where TSelf : class, IInstanceSettings<TSelf>
{
    /// <summary>The key this group is stored under.</summary>
    static abstract string GroupName { get; }

    /// <summary>
    /// Copies another instance's values onto this one.
    /// </summary>
    /// <param name="other">The values to adopt.</param>
    /// <remarks>
    /// Written by hand rather than by reflection, and used by both the loader
    /// and the update handler, so the copy exists in exactly one place per
    /// group.
    /// </remarks>
    void CopyFrom(TSelf other);
}
