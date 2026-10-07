using Application.Abstractions;

namespace Infrastructure.Settings;

/// <summary>
/// Setup for a host with a database: finished once somebody has an account, as the first account
/// administers the instance.
/// </summary>
internal sealed class AccountSetupProgress(IUserRepository users) : ISetupProgress
{
    public async Task<SetupStage> CurrentAsync(CancellationToken cancellationToken) =>
        await users.CountAsync(cancellationToken).ConfigureAwait(false) == 0
            ? SetupStage.Account
            : SetupStage.Complete;
}
