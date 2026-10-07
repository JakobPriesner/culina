using Domain.Shared;

namespace Domain.Sessions;

/// <summary>One signed-in browser.</summary>
/// <remarks>
/// The row is the truth, not the cookie: the cookie is an opaque reference, so sign-out, revocation
/// and privilege changes take effect immediately.
/// </remarks>
public sealed class Session
{
    private Session(
        Guid id,
        Guid userId,
        ReadOnlyMemory<byte> tokenHash,
        ReadOnlyMemory<byte> csrfTokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset lastSeenAt,
        DateTimeOffset expiresAt,
        string? ipAddress,
        string? userAgent,
        DateTimeOffset? revokedAt)
    {
        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        CsrfTokenHash = csrfTokenHash;
        CreatedAt = createdAt;
        LastSeenAt = lastSeenAt;
        ExpiresAt = expiresAt;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        RevokedAt = revokedAt;
    }

    /// <summary>The session's identifier, used in URLs and never as a credential.</summary>
    public Guid Id { get; }

    /// <summary>Whose session it is.</summary>
    public Guid UserId { get; }

    /// <summary>
    /// The digest of the cookie value; read-only so a caller cannot alter it in place.
    /// </summary>
    public ReadOnlyMemory<byte> TokenHash { get; }

    /// <summary>The digest of the CSRF token this session's requests must echo.</summary>
    public ReadOnlyMemory<byte> CsrfTokenHash { get; private set; }

    /// <summary>When the session began.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>When a request last used it, for the "your devices" screen.</summary>
    public DateTimeOffset LastSeenAt { get; private set; }

    /// <summary>When it stops being valid regardless of activity.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Where it was created from.</summary>
    public string? IpAddress { get; }

    /// <summary>What browser created it.</summary>
    public string? UserAgent { get; }

    /// <summary>When it was revoked, if it was.</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>Starts a new session.</summary>
    public static Session Start(
        Guid userId,
        ReadOnlyMemory<byte> tokenHash,
        ReadOnlyMemory<byte> csrfTokenHash,
        DateTimeOffset now,
        TimeSpan lifetime,
        string? ipAddress,
        string? userAgent) =>
        new(
            CulinaId.New(),
            userId,
            tokenHash,
            csrfTokenHash,
            now,
            now,
            now.Add(lifetime),
            ipAddress,
            Truncate(userAgent),
            revokedAt: null);

    /// <summary>Rebuilds a session from storage.</summary>
    public static Session Restore(
        Guid id,
        Guid userId,
        ReadOnlyMemory<byte> tokenHash,
        ReadOnlyMemory<byte> csrfTokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset lastSeenAt,
        DateTimeOffset expiresAt,
        string? ipAddress,
        string? userAgent,
        DateTimeOffset? revokedAt) =>
        new(id, userId, tokenHash, csrfTokenHash, createdAt, lastSeenAt, expiresAt, ipAddress, userAgent, revokedAt);

    /// <summary>Whether this session may still authenticate a request.</summary>
    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    /// <summary>Whether the session has sat unused long enough to be worth extending.</summary>
    /// <remarks>
    /// Sliding the expiry on every request would write the row once per page view; asking first
    /// keeps a request at one indexed read.
    /// </remarks>
    public bool IsDueForRenewal(DateTimeOffset now, TimeSpan idleFor) =>
        now - LastSeenAt >= idleFor;

    /// <summary>Extends the session because it was used, but never past its ceiling.</summary>
    /// <remarks>Without the ceiling a stolen cookie used now and then never expires.</remarks>
    public void Touch(DateTimeOffset now, TimeSpan lifetime, TimeSpan maxLifetime)
    {
        var idleExpiry = now.Add(lifetime);
        var ceiling = CreatedAt.Add(maxLifetime);

        LastSeenAt = now;
        ExpiresAt = idleExpiry < ceiling ? idleExpiry : ceiling;
    }

    /// <summary>Ends the session immediately.</summary>
    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;

    /// <summary>Replaces the CSRF token, on sign-in and whenever privileges change.</summary>
    public void RotateCsrfToken(ReadOnlyMemory<byte> csrfTokenHash) => CsrfTokenHash = csrfTokenHash;

    /// <summary>
    /// User agents are attacker-controlled and unbounded; the devices screen needs only enough to
    /// recognise a browser.
    /// </summary>
    private static string? Truncate(string? userAgent) =>
        userAgent is null ? null : userAgent[..Math.Min(userAgent.Length, 400)];
}
