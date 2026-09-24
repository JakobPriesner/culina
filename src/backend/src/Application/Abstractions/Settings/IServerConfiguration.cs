using Domain.Shared;

namespace Application.Abstractions.Settings;

/// <summary>
/// The bootstrap settings an administrator saves from the app, and what the
/// deployment itself has fixed.
/// </summary>
/// <remarks>
/// <para>
/// Bootstrap settings are still read once, at startup, into immutable records
/// that validate themselves. What this adds is a second place for them to come
/// from: a file on the data volume that the app writes, layered above
/// <c>appsettings.json</c> and below the environment. A change is saved there
/// and applied by starting the host again, so every value goes through exactly
/// the validation it always did.
/// </para>
/// <para>
/// Below the environment on purpose. A value the deployment sets is a value the
/// file cannot change — it is reported as pinned, and the screen shows it
/// read-only rather than accepting an edit that would do nothing. It is also
/// the way back from a setting that locked everybody out: set the variable,
/// and it wins.
/// </para>
/// <para>
/// Keys use the configuration's own form, <c>Section:Key</c>, so they are the
/// same strings the settings extensions read.
/// </para>
/// </remarks>
public interface IServerConfiguration
{
    /// <summary>The value this process started with, from wherever it came.</summary>
    /// <param name="key">The configuration key.</param>
    string? Read(string key);

    /// <summary>
    /// Whether the environment or the command line sets this key, so saving it
    /// would have no effect.
    /// </summary>
    /// <param name="key">The configuration key.</param>
    bool IsPinned(string key);

    /// <summary>Whether the settings file can be written at all.</summary>
    bool CanSave();

    /// <summary>
    /// Writes values into the settings file, leaving every other key in it as
    /// it was. Pinned keys are skipped.
    /// </summary>
    /// <remarks>
    /// There is no way to remove a key, on purpose: "no value" is written as
    /// empty, which every settings reader takes as absent. A removed key would
    /// fall through to whatever a lower source says instead, and an
    /// administrator who cleared a field did not ask for that.
    /// </remarks>
    /// <param name="values">The values, by configuration key.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task<Result> SaveAsync(IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken);
}
