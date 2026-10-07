using Domain.Shared;

namespace Application.Abstractions.Settings;

/// <summary>Persists one instance-settings group.</summary>
public interface ISettingsStore<TSettings>
    where TSettings : class, IInstanceSettings<TSettings>
{
    /// <summary>Reads the stored values, or null when this instance never changed them (a fresh instance has no row).</summary>
    Task<TSettings?> LoadAsync(CancellationToken cancellationToken);

    /// <summary>Writes the values.</summary>
    Task<Result> SaveAsync(TSettings settings, CancellationToken cancellationToken);
}
