namespace Contracts.Sessions.GetAll;

/// <summary>The caller's active sessions.</summary>
public sealed record Response
{
    /// <summary>One entry per signed-in device, most recently used first.</summary>
    public required IReadOnlyList<SessionSummary> Items { get; init; }
}

/// <summary>One signed-in device.</summary>
public sealed record SessionSummary
{
    /// <summary>The session's id, for revoking it.</summary>
    public required Guid SessionId { get; init; }

    /// <summary>When it was started.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>When a request last used it.</summary>
    public required DateTimeOffset LastSeenAt { get; init; }

    /// <summary>Where it was started from.</summary>
    public string? IpAddress { get; init; }

    /// <summary>What browser started it.</summary>
    public string? UserAgent { get; init; }

    /// <summary>Whether this is the session making the request.</summary>
    public required bool IsCurrent { get; init; }
}
