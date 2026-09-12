using Domain.Shared;
using Domain.Users;

namespace Application.Abstractions;

/// <summary>Reads and writes one person's preferences.</summary>
public interface IUserPreferencesRepository
{
    /// <summary>
    /// The stored preferences, or the defaults when the row has never been
    /// written.
    /// </summary>
    /// <param name="userId">Whose preferences.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <remarks>
    /// Never a failure for an absent row: preferences are optional by nature,
    /// and an account that has never opened the settings screen still has
    /// perfectly good defaults.
    /// </remarks>
    Task<UserPreferences> GetAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Writes the preferences, creating the row if needed.</summary>
    /// <param name="preferences">What to store.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task<Result<long>> SaveAsync(UserPreferences preferences, CancellationToken cancellationToken);
}
