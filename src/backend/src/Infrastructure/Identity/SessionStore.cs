using Application.Abstractions;
using Domain.Sessions;
using Domain.Shared;
using Infrastructure.Persistence;

namespace Infrastructure.Identity;

/// <summary>Stores sessions.</summary>
/// <param name="executor">Runs the SQL.</param>
/// <param name="tokens">Hashes the cookie value for lookup.</param>
internal sealed class SessionStore(DbExecutor executor, ISessionTokens tokens) : ISessionStore
{
    private const string Columns =
        "id, user_id, token_hash, csrf_token_hash, created_at, last_seen_at, expires_at, "
        + "host(ip_address) as ip_address, user_agent, revoked_at";

    public async Task<Result<Session>> FindActiveByTokenAsync(
        string token,
        CancellationToken cancellationToken)
    {
        // Lookup is by digest, so the raw token exists only in the request and
        // never in an index, a log or a query plan.
        var row = await executor.QuerySingleOrDefaultAsync<SessionRow>(
            $"select {Columns} from sessions where token_hash = @tokenHash;",
            new { tokenHash = tokens.Digest(token).ToArray() },
            cancellationToken).ConfigureAwait(false);

        return row is null ? SessionErrors.NotAuthenticated : row.ToDomain();
    }

    public async Task<IReadOnlyList<Session>> ForUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var rows = await executor.QueryAsync<SessionRow>(
            $"""
             select {Columns} from sessions
             where user_id = @userId and revoked_at is null
             order by last_seen_at desc;
             """,
            new { userId },
            cancellationToken).ConfigureAwait(false);

        return [.. rows.Select(row => row.ToDomain())];
    }

    public async Task<Result> AddAsync(Session session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        await executor.ExecuteAsync(
            """
            insert into sessions (id, user_id, token_hash, csrf_token_hash, created_at,
                                  last_seen_at, expires_at, ip_address, user_agent)
            values (@id, @userId, @tokenHash, @csrfTokenHash, @createdAt,
                    @lastSeenAt, @expiresAt, cast(@ipAddress as inet), @userAgent);
            """,
            new
            {
                id = session.Id,
                userId = session.UserId,
                tokenHash = session.TokenHash.ToArray(),
                csrfTokenHash = session.CsrfTokenHash.ToArray(),
                createdAt = session.CreatedAt,
                lastSeenAt = session.LastSeenAt,
                expiresAt = session.ExpiresAt,
                ipAddress = session.IpAddress,
                userAgent = session.UserAgent
            },
            cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result> UpdateAsync(Session session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        await executor.ExecuteAsync(
            """
            update sessions
            set last_seen_at = @lastSeenAt,
                expires_at = @expiresAt,
                csrf_token_hash = @csrfTokenHash,
                revoked_at = @revokedAt
            where id = @id;
            """,
            new
            {
                id = session.Id,
                lastSeenAt = session.LastSeenAt,
                expiresAt = session.ExpiresAt,
                csrfTokenHash = session.CsrfTokenHash.ToArray(),
                revokedAt = session.RevokedAt
            },
            cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result> RevokeAsync(
        Guid sessionId,
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // The user id is part of the WHERE, so revoking is scoped to the
        // caller's own sessions in SQL rather than by a check that could be
        // forgotten at a second call site.
        var affected = await executor.ExecuteAsync(
            """
            update sessions set revoked_at = @now
            where id = @sessionId and user_id = @userId and revoked_at is null;
            """,
            new { sessionId, userId, now },
            cancellationToken).ConfigureAwait(false);

        return affected == 0 ? SessionErrors.SessionNotFound : Result.Success();
    }

    public Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        executor.ExecuteAsync(
            "delete from sessions where expires_at < @now or revoked_at is not null;",
            new { now },
            cancellationToken);
}
