using Domain.Shared;

namespace Application.Abstractions.Settings;

/// <summary>
/// The bootstrap settings an administrator saves from the app, and what the deployment itself has
/// fixed.
/// </summary>
/// <remarks>
/// A file on the data volume layered above <c>appsettings.json</c> and below the environment,
/// applied by restarting so every value passes its usual validation. An environment value is pinned
/// (read-only), which is also the way back from a setting that locked everybody out.
/// </remarks>
public interface IServerConfiguration
{
    /// <summary>The value this process started with, from wherever it came.</summary>
    string? Read(string key);

    /// <summary>
    /// Whether the environment or the command line sets this key, so saving it would have no
    /// effect.
    /// </summary>
    bool IsPinned(string key);

    /// <summary>Whether the settings file can be written at all.</summary>
    bool CanSave();

    /// <summary>
    /// Writes values into the settings file, leaving every other key as it was; pinned keys are
    /// skipped.
    /// </summary>
    /// <remarks>
    /// No way to remove a key: "no value" is written as empty, which every reader takes as absent,
    /// where a removed key would fall through to a lower source.
    /// </remarks>
    Task<Result> SaveAsync(IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken);
}
