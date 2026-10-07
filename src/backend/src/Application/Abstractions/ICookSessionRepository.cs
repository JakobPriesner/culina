using Domain.Cooking;
using Domain.Shared;

namespace Application.Abstractions;

/// <summary>Where cooking sessions are kept.</summary>
public interface ICookSessionRepository
{
    /// <summary>The one session this person has going, if any.</summary>
    Task<CookSession?> ActiveForUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>One session, by id, if it belongs to this person.</summary>
    Task<Result<CookSession>> FindAsync(
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken);

    /// <summary>Starts a session, ending whatever else this person had going, in one transaction (the partial unique index refuses a second active session).</summary>
    /// <param name="session">The session to start.</param>
    /// <param name="now">The injected clock's reading, for the abandonment.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task<Result> StartAsync(
        CookSession session,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>Saves a step advance. Touches two columns and checks no version: the last tap is the truth about where the cook is.</summary>
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
