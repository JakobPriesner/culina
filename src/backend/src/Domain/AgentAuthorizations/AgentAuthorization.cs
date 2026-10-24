using Domain.Shared;

namespace Domain.AgentAuthorizations;

/// <summary>An app asking, through OAuth, to connect an AI agent to someone's account: pending, then answered, then redeemed once.</summary>
/// <remarks>
/// The authorization endpoint starts one before anybody is known; whoever approves it on the consent screen is whose
/// account the agent gets. Approving mints the authorization code, of which only the digest is kept; redeeming it once
/// starts the agent's session, and the session's id is kept so a second redemption can end it (OAuth 2.1, 4.1.3).
/// </remarks>
public sealed class AgentAuthorization
{
    /// <summary>How long the consent screen may take, signing in included.</summary>
    public static readonly TimeSpan PendingLifetime = TimeSpan.FromMinutes(10);

    /// <summary>How long a code lives: the app redeems it the moment the browser comes back.</summary>
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(2);

    private AgentAuthorization(
        Guid id,
        AgentClient client,
        string redirectUri,
        string codeChallenge,
        string? state,
        string resource,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        Id = id;
        Client = client;
        RedirectUri = redirectUri;
        CodeChallenge = codeChallenge;
        State = state;
        Resource = resource;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    /// <summary>The request's identifier, in the consent screen's address; never a credential.</summary>
    public Guid Id { get; }

    /// <summary>The app asking.</summary>
    public AgentClient Client { get; }

    /// <summary>Where the browser goes back to: one of the app's registered addresses.</summary>
    public string RedirectUri { get; }

    /// <summary>The PKCE <c>S256</c> challenge the redeeming app must answer.</summary>
    public string CodeChallenge { get; }

    /// <summary>The app's own value, handed back unchanged on the way out.</summary>
    public string? State { get; }

    /// <summary>The MCP server the token is for (RFC 8707): always this Culina's.</summary>
    public string Resource { get; }

    /// <summary>When the app asked.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>Until when the person may answer, and once approved, until when the code may be redeemed.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Who approved or denied it.</summary>
    public Guid? UserId { get; private set; }

    /// <summary>When it was answered.</summary>
    public DateTimeOffset? DecidedAt { get; private set; }

    /// <summary>Whether the answer was yes.</summary>
    public bool Approved { get; private set; }

    /// <summary>The digest of the authorization code, once approved.</summary>
    public ReadOnlyMemory<byte>? CodeHash { get; private set; }

    /// <summary>When the code was redeemed.</summary>
    public DateTimeOffset? RedeemedAt { get; private set; }

    /// <summary>The agent session the code became.</summary>
    public Guid? SessionId { get; private set; }

    /// <summary>The browser's address when approving, which the agent's session is listed with.</summary>
    public string? ApproverIpAddress { get; private set; }

    /// <summary>The browser that approved, which the agent's session is listed with.</summary>
    public string? ApproverUserAgent { get; private set; }

    /// <summary>A request nobody has answered yet.</summary>
    public static AgentAuthorization Start(
        AgentClient client,
        string redirectUri,
        string codeChallenge,
        string? state,
        string resource,
        DateTimeOffset now) =>
        new(CulinaId.New(), client, redirectUri, codeChallenge, state, resource, now, now.Add(PendingLifetime));

    /// <summary>Rebuilds a request from storage.</summary>
    public static AgentAuthorization Restore(
        Guid id,
        AgentClient client,
        string redirectUri,
        string codeChallenge,
        string? state,
        string resource,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        Guid? userId,
        DateTimeOffset? decidedAt,
        bool approved,
        ReadOnlyMemory<byte>? codeHash,
        DateTimeOffset? redeemedAt,
        Guid? sessionId,
        string? approverIpAddress,
        string? approverUserAgent) =>
        new(id, client, redirectUri, codeChallenge, state, resource, createdAt, expiresAt)
        {
            UserId = userId,
            DecidedAt = decidedAt,
            Approved = approved,
            CodeHash = codeHash,
            RedeemedAt = redeemedAt,
            SessionId = sessionId,
            ApproverIpAddress = approverIpAddress,
            ApproverUserAgent = approverUserAgent
        };

    /// <summary>Whether the person can still answer it.</summary>
    public bool IsPending(DateTimeOffset now) => DecidedAt is null && ExpiresAt > now;

    /// <summary>Says yes: the code with this digest now opens the person's account to the app, briefly.</summary>
    public Result Approve(
        Guid userId,
        ReadOnlyMemory<byte> codeHash,
        string? ipAddress,
        string? userAgent,
        DateTimeOffset now)
    {
        if (!IsPending(now))
        {
            return AgentAuthorizationErrors.NotFound;
        }

        UserId = userId;
        DecidedAt = now;
        Approved = true;
        CodeHash = codeHash;
        ExpiresAt = now.Add(CodeLifetime);
        ApproverIpAddress = ipAddress;
        ApproverUserAgent = userAgent;

        return Result.Success();
    }

    /// <summary>Says no; the app is told <c>access_denied</c>.</summary>
    public Result Deny(Guid userId, DateTimeOffset now)
    {
        if (!IsPending(now))
        {
            return AgentAuthorizationErrors.NotFound;
        }

        UserId = userId;
        DecidedAt = now;

        return Result.Success();
    }

    /// <summary>
    /// Whether an app may redeem the code: the same app, the same return address, the PKCE verifier, before it
    /// expires, the first time. Any miss is <c>invalid_grant</c>, except naming another resource.
    /// </summary>
    public Result Redeem(string clientId, string redirectUri, string? codeVerifier, string? resource, DateTimeOffset now)
    {
        if (resource is not null && !Names(resource, Resource))
        {
            return AgentAuthorizationErrors.InvalidTarget;
        }

        if (!Approved
            || RedeemedAt is not null
            || ExpiresAt <= now
            || !string.Equals(clientId, Client.ClientId, StringComparison.Ordinal)
            || !string.Equals(redirectUri, RedirectUri, StringComparison.Ordinal)
            || !AgentAuthorizations.CodeChallenge.Verifies(codeVerifier, CodeChallenge))
        {
            return AgentAuthorizationErrors.InvalidGrant;
        }

        RedeemedAt = now;

        return Result.Success();
    }

    /// <summary>
    /// Whether a resource indicator (RFC 8707) names this MCP server: scheme and host compared without regard to case,
    /// as the MCP authorization spec asks, and a trailing slash ignored; no query or fragment.
    /// </summary>
    public static bool Names(string presented, string resource)
    {
        ArgumentNullException.ThrowIfNull(presented);
        ArgumentNullException.ThrowIfNull(resource);

        return Uri.TryCreate(presented, UriKind.Absolute, out var actual)
            && Uri.TryCreate(resource, UriKind.Absolute, out var expected)
            && actual.Query.Length == 0
            && actual.Fragment.Length == 0
            && !presented.Contains('#', StringComparison.Ordinal)
            && actual.UserInfo.Length == 0
            && string.Equals(actual.Scheme, expected.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(actual.Authority, expected.Authority, StringComparison.OrdinalIgnoreCase)
            && string.Equals(actual.AbsolutePath.TrimEnd('/'), expected.AbsolutePath.TrimEnd('/'), StringComparison.Ordinal);
    }

    /// <summary>Records the agent session the code became.</summary>
    public void Became(Guid sessionId) => SessionId = sessionId;
}
