namespace Contracts.Settings.GetServer;

/// <summary>
/// The server settings an administrator can change from the app, as the
/// running process uses them.
/// </summary>
public sealed record Response
{
    /// <summary>The session cookie's attributes.</summary>
    public required CookiesContract Cookies { get; init; }

    /// <summary>Which proxies may say who the client really is.</summary>
    public required ForwardedHeadersContract ForwardedHeaders { get; init; }

    /// <summary>The ceilings on what one client may ask for.</summary>
    public required RateLimitsContract RateLimits { get; init; }

    /// <summary>Where traces, metrics and logs go.</summary>
    public required TelemetryContract Telemetry { get; init; }

    /// <summary>How the request that asked for this reached the server.</summary>
    public required ConnectionContract Connection { get; init; }

    /// <summary>
    /// The settings the deployment fixes, by the environment variable that sets
    /// them — <c>Cookies__Secure</c>. Saving cannot change these.
    /// </summary>
    public required IReadOnlyList<string> Pinned { get; init; }

    /// <summary>Whether changes can be saved at all.</summary>
    public required bool Writable { get; init; }
}

/// <summary>The session cookie's attributes.</summary>
public sealed record CookiesContract
{
    /// <summary>Whether cookies are only sent over HTTPS.</summary>
    public required bool Secure { get; init; }

    /// <summary>How many days an unused session lasts.</summary>
    public required int SessionDays { get; init; }

    /// <summary>How many hours a session may go unused before a request extends it.</summary>
    public required int RenewAfterHours { get; init; }
}

/// <summary>Which proxies may say who the client really is.</summary>
public sealed record ForwardedHeadersContract
{
    /// <summary>Proxy addresses.</summary>
    public required IReadOnlyList<string> KnownProxies { get; init; }

    /// <summary>Proxy networks, as CIDR ranges.</summary>
    public required IReadOnlyList<string> KnownNetworks { get; init; }
}

/// <summary>The ceilings on what one client may ask for.</summary>
public sealed record RateLimitsContract
{
    /// <summary>Sign-in attempts per minute from one address.</summary>
    public required int LoginPerIpPerMinute { get; init; }

    /// <summary>Sign-in attempts per minute against one account.</summary>
    public required int LoginPerAccountPerMinute { get; init; }

    /// <summary>New accounts per hour from one address.</summary>
    public required int RegisterPerIpPerHour { get; init; }

    /// <summary>Invitations redeemed per hour from one address.</summary>
    public required int InvitationPerIpPerHour { get; init; }

    /// <summary>Recipe imports from a web page per hour from one person.</summary>
    public required int ImportsPerHour { get; init; }

    /// <summary>Requests to a connected recipe library per hour.</summary>
    public required int SourceRequestsPerHour { get; init; }

    /// <summary>Reads of shared recipes per minute from one address.</summary>
    public required int SharedRecipesPerIpPerMinute { get; init; }

    /// <summary>Requests to the assistant per hour from one person.</summary>
    public required int AssistantRequestsPerHour { get; init; }

    /// <summary>Requests per minute from one signed-in session.</summary>
    public required int RequestsPerSessionPerMinute { get; init; }
}

/// <summary>Where traces, metrics and logs go.</summary>
public sealed record TelemetryContract
{
    /// <summary>The OTLP collector's address, or null when nothing is exported.</summary>
    public string? OtlpEndpoint { get; init; }

    /// <summary><c>grpc</c> or <c>http_protobuf</c>.</summary>
    public required string OtlpProtocol { get; init; }
}

/// <summary>
/// How the request that asked for these settings reached the server.
/// </summary>
/// <remarks>
/// The one thing nobody setting up a proxy can see from the outside: which
/// address the proxy connects from, and whether Culina already believes what it
/// says. It turns "which address do I trust?" into a choice between the
/// address on the screen and none.
/// </remarks>
public sealed record ConnectionContract
{
    /// <summary>
    /// The address the connection came from, as Culina sees it — the proxy's,
    /// when there is one it does not trust yet.
    /// </summary>
    public string? RemoteAddress { get; init; }

    /// <summary>Whether the request said it was forwarded for someone else.</summary>
    public required bool Forwarded { get; init; }

    /// <summary>Whether Culina believed it, because the proxy is trusted.</summary>
    public required bool ProxyTrusted { get; init; }
}
