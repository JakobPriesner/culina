using Domain.Cooking;
using Domain.Shared;

namespace Application.Abstractions;

/// <summary>Where cooking sessions are kept.</summary>
public interface ICookSessionRepository
{
    /// <summary>The one session this person has going, if any.</summary>
    /// <param name="userId">Whose session.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<CookSession?> ActiveForUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>One session, by id, if it belongs to this person.</summary>
    /// <param name="sessionId">Which session.</param>
    /// <param name="userId">Whose it must be.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<Result<CookSession>> FindAsync(
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Starts a session, ending whatever else this person had going.
    /// </summary>
    /// <param name="session">The session to start.</param>
    /// <param name="now">The injected clock's reading, for the abandonment.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <remarks>
    /// One statement pair in one transaction: the database's partial unique
    /// index will refuse a second active session, so abandoning the previous one
    /// and inserting the new one cannot be two separate decisions.
    /// </remarks>
    Task<Result> StartAsync(
        CookSession session,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Saves a step advance.
    /// </summary>
    /// <param name="session">The session that moved.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <remarks>
    /// Called on every step, so it touches two columns and does not check a
    /// version: the last tap genuinely is the truth about where the cook is.
    /// </remarks>
    Task<Result> TouchAsync(CookSession session, CancellationToken cancellationToken);

    /// <summary>Saves a change that is worth a concurrency check.</summary>
    /// <param name="session">The session, already changed.</param>
    /// <param name="expectedVersion">The version the caller had.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task<Result> UpdateAsync(
        CookSession session,
        long expectedVersion,
        CancellationToken cancellationToken);
}
