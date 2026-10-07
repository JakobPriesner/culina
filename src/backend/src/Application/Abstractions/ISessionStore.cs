using Domain.Sessions;
using Domain.Shared;

namespace Application.Abstractions;

/// <summary>Reads and writes sessions.</summary>
public interface ISessionStore
{
    /// <summary>Finds the session a cookie value refers to, if it is still active.</summary>
    /// <param name="token">The raw cookie value.</param>
    /// <param name="now">The injected current time, against which expiry is judged.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns><c>auth.not_authenticated</c> for an unknown, revoked or expired session alike.</returns>
    Task<Result<Session>> FindActiveByTokenAsync(
        string token,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>The user's sessions, newest first, for the devices screen.</summary>
    /// <param name="userId">Whose sessions.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<IReadOnlyList<Session>> ForUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Stores a newly started session.</summary>
    /// <param name="session">The session to store.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task<Result> AddAsync(Session session, CancellationToken cancellationToken);

    /// <summary>Saves that a session was used, and how far its expiry moved.</summary>
    /// <param name="session">The touched session.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns><c>auth.not_authenticated</c> when the session was revoked or lapsed after it was read; it is then left unchanged.</returns>
    Task<Result> RenewAsync(Session session, CancellationToken cancellationToken);

    /// <summary>Ends one session.</summary>
    /// <param name="sessionId">Which session.</param>
    /// <param name="userId">Whose it must be, so nobody can revoke another user's.</param>
    /// <param name="now">The injected current time.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task<Result> RevokeAsync(
        Guid sessionId,
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>Ends every session of one account, except perhaps the caller's own.</summary>
    /// <param name="userId">Whose sessions.</param>
    /// <param name="keepSessionId">The session to leave running, or null to end them all.</param>
    /// <param name="now">The injected current time.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task RevokeAllAsync(
        Guid userId,
        Guid? keepSessionId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>Deletes sessions that lapsed, so the table does not grow forever.</summary>
    /// <param name="now">The injected current time.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
