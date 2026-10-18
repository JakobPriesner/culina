using Domain.Sessions;

namespace Infrastructure.Identity;

/// <summary>The <c>sessions</c> row as PostgreSQL returns it.</summary>
internal sealed record SessionRow
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public byte[] TokenHash { get; init; } = [];

    public byte[] CsrfTokenHash { get; init; } = [];

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset LastSeenAt { get; init; }

    public DateTimeOffset ExpiresAt { get; init; }

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }

    public DateTimeOffset? RevokedAt { get; init; }

    public string Client { get; init; } = "browser";
}

/// <summary>Turns a stored row back into a domain session.</summary>
internal static class SessionRowMappings
{
    internal static Session ToDomain(this SessionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return Session.Restore(
            row.Id,
            row.UserId,
            row.TokenHash,
            row.CsrfTokenHash,
            row.CreatedAt,
            row.LastSeenAt,
            row.ExpiresAt,
            row.IpAddress,
            row.UserAgent,
            row.RevokedAt,
            ToClient(row.Client));
    }

    /// <summary>The <c>client</c> column's spelling.</summary>
    internal static string Of(SessionClient client) => client switch
    {
        SessionClient.Ios => "ios",
        SessionClient.Android => "android",
        SessionClient.Instance => "instance",
        SessionClient.Agent => "agent",
        _ => "browser"
    };

    private static SessionClient ToClient(string client) => client switch
    {
        "ios" => SessionClient.Ios,
        "android" => SessionClient.Android,
        "instance" => SessionClient.Instance,
        "agent" => SessionClient.Agent,
        _ => SessionClient.Browser
    };
}
