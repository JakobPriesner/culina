using Domain.Sessions;
using Domain.Shared;

namespace Application.Abstractions;

/// <summary>Reads and writes sessions.</summary>
public interface ISessionStore
{
    /// <summary>Finds the session a cookie value refers to, if it is still active.</summary>
    /// <param name="token">The raw cookie value.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<Result<Session>> FindActiveByTokenAsync(string token, CancellationToken cancellationToken);

    /// <summary>The user's sessions, newest first, for the devices screen.</summary>
    /// <param name="userId">Whose sessions.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<IReadOnlyList<Session>> ForUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Stores a newly started session.</summary>
    /// <param name="session">The session to store.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task<Result> AddAsync(Session session, CancellationToken cancellationToken);

    /// <summary>Saves activity, expiry and the CSRF digest.</summary>
    /// <param name="session">The changed session.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task<Result> UpdateAsync(Session session, CancellationToken cancellationToken);

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

    /// <summary>Deletes sessions that lapsed, so the table does not grow forever.</summary>
    /// <param name="now">The injected current time.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
