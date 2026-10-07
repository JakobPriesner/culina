using Domain.Shared;
using Domain.Users;

namespace Application.Abstractions;

/// <summary>Reads and writes user accounts.</summary>
public interface IUserRepository
{
    /// <summary>Finds a user by id.</summary>
    Task<Result<User>> FindAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Finds a user by the address they sign in with.</summary>
    Task<Result<User>> FindByEmailAsync(Email email, CancellationToken cancellationToken);

    /// <summary>How many accounts exist, for the instance user limit.</summary>
    Task<int> CountAsync(CancellationToken cancellationToken);

    /// <summary>How many accounts exist, read under a lock that holds other registrations back until the transaction ends; only meaningful inside a unit of work.</summary>
    /// <param name="cancellationToken">Cancels the wait and the query.</param>
    Task<int> CountForRegistrationAsync(CancellationToken cancellationToken);

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
    Task<bool> IsAdminAsync(Guid userId, CancellationToken cancellationToken);
}
