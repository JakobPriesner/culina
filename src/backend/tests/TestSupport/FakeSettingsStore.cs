using Application.Abstractions.Settings;
using Domain.Shared;

namespace TestSupport;

/// <summary>Keeps one settings group in memory and can fail the write, to assert "persist first, then mutate".</summary>
public sealed class FakeSettingsStore<TSettings> : ISettingsStore<TSettings>
    where TSettings : class, IInstanceSettings<TSettings>
{
    public TSettings? Saved { get; private set; }

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
