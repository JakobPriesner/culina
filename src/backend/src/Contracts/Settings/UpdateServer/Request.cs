namespace Contracts.Settings.UpdateServer;

/// <summary>
/// The server settings to run with from now on. Every group is complete: a
/// value left out is not "unchanged", it is missing.
/// </summary>
public sealed record Request
{
    /// <summary>The session cookie's attributes.</summary>
    public required CookiesContract Cookies { get; init; }

    /// <summary>Which proxies may say who the client really is.</summary>
    public required ForwardedHeadersContract ForwardedHeaders { get; init; }

    /// <summary>The ceilings on what one client may ask for.</summary>
    public required RateLimitsContract RateLimits { get; init; }

    /// <summary>Where traces, metrics and logs go.</summary>
    public required TelemetryContract Telemetry { get; init; }
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
    /// <summary>The OTLP collector's address, or null to export nothing.</summary>
    public string? OtlpEndpoint { get; init; }

    /// <summary><c>grpc</c> or <c>http_protobuf</c>.</summary>
    public required string OtlpProtocol { get; init; }
}
