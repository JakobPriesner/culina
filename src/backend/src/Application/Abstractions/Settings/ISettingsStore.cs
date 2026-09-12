using Domain.Shared;

namespace Application.Abstractions.Settings;

/// <summary>Persists one instance-settings group.</summary>
/// <typeparam name="TSettings">The group.</typeparam>
public interface ISettingsStore<TSettings>
    where TSettings : class, IInstanceSettings<TSettings>
{
    /// <summary>
    /// Reads the stored values, or null when this instance has never changed
    /// them.
    /// </summary>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <remarks>
    /// Null rather than a failure: a fresh instance has no row, which is the
    /// normal first-boot state and not an error. The caller keeps the
    /// compiled-in defaults.
    /// </remarks>
    Task<TSettings?> LoadAsync(CancellationToken cancellationToken);

    /// <summary>Writes the values.</summary>
    /// <param name="settings">The values to store.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task<Result> SaveAsync(TSettings settings, CancellationToken cancellationToken);
}
