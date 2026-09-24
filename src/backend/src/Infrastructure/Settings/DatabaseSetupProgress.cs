using Application.Abstractions;

namespace Infrastructure.Settings;

/// <summary>
/// Setup, for the host that runs because there is no database yet: always at
/// the first step, and nothing to ask to know it.
/// </summary>
internal sealed class DatabaseSetupProgress : ISetupProgress
{
    public Task<SetupStage> CurrentAsync(CancellationToken cancellationToken) =>
        Task.FromResult(SetupStage.Database);
}
