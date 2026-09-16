using Domain.Shared;

namespace Domain.Sessions;

/// <summary>
/// One signed-in browser.
/// </summary>
/// <remarks>
/// The row is the truth, not the cookie. Because the cookie carries nothing but
/// an opaque reference, signing out, revoking a device and a change of
/// privileges all take effect immediately — none of which is possible with a
/// self-contained token that the server cannot withdraw.
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
    /// The digest of the cookie value. Read-only memory rather than an array,
    /// so a caller cannot alter a stored digest in place.
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
    /// <param name="userId">Who signed in.</param>
    /// <param name="tokenHash">The digest of the cookie value.</param>
    /// <param name="csrfTokenHash">The digest of the CSRF token.</param>
    /// <param name="now">The injected current time.</param>
    /// <param name="lifetime">How long it may live without activity.</param>
    /// <param name="ipAddress">The client address, for the devices screen.</param>
    /// <param name="userAgent">The browser, for the devices screen.</param>
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
    /// <param name="id">Its identifier.</param>
    /// <param name="userId">Whose it is.</param>
    /// <param name="tokenHash">The digest of the cookie value.</param>
    /// <param name="csrfTokenHash">The digest of the CSRF token.</param>
    /// <param name="createdAt">When it began.</param>
    /// <param name="lastSeenAt">When it was last used.</param>
    /// <param name="expiresAt">When it lapses.</param>
    /// <param name="ipAddress">Where it came from.</param>
    /// <param name="userAgent">What browser created it.</param>
    /// <param name="revokedAt">When it was revoked, if it was.</param>
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
    /// <param name="now">The injected current time.</param>
    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    /// <summary>
    /// Whether the session has sat unused long enough to be worth extending.
    /// </summary>
    /// <remarks>
    /// The expiry slides, but not on every request: the row would then be
    /// written once per page view for no gain, because a session used twice in
    /// a minute is no more alive than one used once. Asking this first is what
    /// keeps an authenticated request at a single indexed read.
    /// </remarks>
    /// <param name="now">The injected current time.</param>
    /// <param name="idleFor">How long unused is long enough.</param>
    public bool IsDueForRenewal(DateTimeOffset now, TimeSpan idleFor) =>
        now - LastSeenAt >= idleFor;

    /// <summary>Extends the session because it was used.</summary>
    /// <param name="now">The injected current time.</param>
    /// <param name="lifetime">How long it may live without further activity.</param>
    public void Touch(DateTimeOffset now, TimeSpan lifetime)
    {
        LastSeenAt = now;
        ExpiresAt = now.Add(lifetime);
    }

    /// <summary>Ends the session immediately.</summary>
    /// <param name="now">The injected current time.</param>
    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;

    /// <summary>
    /// Replaces the CSRF token, which happens on sign-in and whenever
    /// privileges change.
    /// </summary>
    /// <param name="csrfTokenHash">The digest of the new token.</param>
    public void RotateCsrfToken(ReadOnlyMemory<byte> csrfTokenHash) => CsrfTokenHash = csrfTokenHash;

    /// <summary>
    /// User agents are attacker-controlled and unbounded; the devices screen
    /// only needs enough to recognise a browser.
    /// </summary>
    private static string? Truncate(string? userAgent) =>
        userAgent is null ? null : userAgent[..Math.Min(userAgent.Length, 400)];
}
