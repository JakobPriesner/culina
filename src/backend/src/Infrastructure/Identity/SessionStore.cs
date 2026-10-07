using Application.Abstractions;
using Domain.Sessions;
using Domain.Shared;
using Infrastructure.Persistence;

namespace Infrastructure.Identity;

/// <summary>Stores sessions.</summary>
internal sealed class SessionStore(DbExecutor executor, ISecretTokens tokens) : ISessionStore
{
    private const string Columns =
        "id, user_id, token_hash, csrf_token_hash, created_at, last_seen_at, expires_at, "
        + "host(ip_address) as ip_address, user_agent, revoked_at";

    public async Task<Result<Session>> FindActiveByTokenAsync(
        string token,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Lookup is by digest, so the raw token never reaches an index, log or plan. "Active" is in the WHERE,
        // so a revoked or lapsed session's CSRF token is as dead as its cookie.
        var row = await executor.QuerySingleOrDefaultAsync<SessionRow>(
            $"""
             select {Columns} from sessions
             where token_hash = @tokenHash and revoked_at is null and expires_at > @now;
             """,
            new { tokenHash = tokens.Digest(token).ToArray(), now },
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

    public async Task<Result> RenewAsync(Session session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        // Writes only what renewing changes, to a still-active session: writing back the stale copy could undo a concurrent revocation.
        var affected = await executor.ExecuteAsync(
            """
            update sessions
            set last_seen_at = @lastSeenAt,
                expires_at = @expiresAt
            where id = @id and revoked_at is null and expires_at > @lastSeenAt;
            """,
            new
            {
                id = session.Id,
                lastSeenAt = session.LastSeenAt,
                expiresAt = session.ExpiresAt
            },
            cancellationToken).ConfigureAwait(false);

        return affected == 0 ? SessionErrors.NotAuthenticated : Result.Success();
    }

    public async Task<Result> RevokeAsync(
        Guid sessionId,
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // The user id is in the WHERE, so revoking is scoped to the caller's own sessions in SQL.
        var affected = await executor.ExecuteAsync(
            """
            update sessions set revoked_at = @now
            where id = @sessionId and user_id = @userId and revoked_at is null;
            """,
            new { sessionId, userId, now },
            cancellationToken).ConfigureAwait(false);

        return affected == 0 ? SessionErrors.SessionNotFound : Result.Success();
    }

    public Task RevokeAllAsync(
        Guid userId,
        Guid? keepSessionId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        executor.ExecuteAsync(
            """
            update sessions set revoked_at = @now
            where user_id = @userId
              and revoked_at is null
              and (@keepSessionId::uuid is null or id <> @keepSessionId);
            """,
            new { userId, keepSessionId, now },
            cancellationToken);

    public Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        executor.ExecuteAsync(
            "delete from sessions where expires_at < @now or revoked_at is not null;",
            new { now },
            cancellationToken);
}
