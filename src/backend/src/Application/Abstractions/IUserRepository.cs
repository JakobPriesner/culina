using Domain.Shared;
using Domain.Users;

namespace Application.Abstractions;

/// <summary>Reads and writes user accounts.</summary>
public interface IUserRepository
{
    /// <summary>Finds a user by id.</summary>
    /// <param name="userId">Who to look for.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<Result<User>> FindAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Finds a user by the address they sign in with.</summary>
    /// <param name="email">The normalised address.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<Result<User>> FindByEmailAsync(Email email, CancellationToken cancellationToken);

    /// <summary>How many accounts exist, for the instance user limit.</summary>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<int> CountAsync(CancellationToken cancellationToken);

    /// <summary>Stores a new account.</summary>
    /// <param name="user">The account to store.</param>
    /// <param name="isAdmin">Whether this account administers the instance.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns><c>users.email_already_used</c> when the address is taken.</returns>
    Task<Result> AddAsync(User user, bool isAdmin, CancellationToken cancellationToken);

    /// <summary>Saves changes to an account.</summary>
    /// <param name="user">The changed account.</param>
    /// <param name="expectedVersion">The version the caller last saw.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The new version, or a precondition failure when it moved on.</returns>
    Task<Result<long>> UpdateAsync(User user, long expectedVersion, CancellationToken cancellationToken);

    /// <summary>Whether this account administers the instance.</summary>
    /// <param name="userId">Who to check.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<bool> IsAdminAsync(Guid userId, CancellationToken cancellationToken);
}
