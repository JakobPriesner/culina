using Application.Abstractions.Settings;
using Domain.Shared;

namespace TestSupport;

/// <summary>
/// Keeps one settings group in memory.
/// </summary>
/// <remarks>
/// Records what was written, because "persist first, then mutate the singleton"
/// is the property worth asserting about a settings handler — and the way to
/// check the order is to be able to make the write fail.
/// </remarks>
/// <typeparam name="TSettings">The group.</typeparam>
public sealed class FakeSettingsStore<TSettings> : ISettingsStore<TSettings>
    where TSettings : class, IInstanceSettings<TSettings>
{
    /// <summary>What was last saved, or null if nothing was.</summary>
    public TSettings? Saved { get; private set; }

    /// <summary>When set, saving fails with this instead of storing anything.</summary>
    public Error? FailWith { get; set; }

    public Task<TSettings?> LoadAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Saved);

    public Task<Result> SaveAsync(TSettings settings, CancellationToken cancellationToken)
    {
        if (FailWith is { } failure)
        {
            return Task.FromResult(Result.Failure(failure));
        }

        Saved = settings;

        return Task.FromResult(Result.Success());
    }
}
