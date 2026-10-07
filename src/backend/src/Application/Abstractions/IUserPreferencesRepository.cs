using Domain.Shared;
using Domain.Users;

namespace Application.Abstractions;

/// <summary>Reads and writes one person's preferences.</summary>
public interface IUserPreferencesRepository
{
    /// <summary>
    /// The stored preferences, or good defaults when the row has never been written; an absent row
    /// is never a failure.
    /// </summary>
    Task<UserPreferences> GetAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Writes the preferences, creating the row if needed.</summary>
    Task<Result<long>> SaveAsync(UserPreferences preferences, CancellationToken cancellationToken);
}
