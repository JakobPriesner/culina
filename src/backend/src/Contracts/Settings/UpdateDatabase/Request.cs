namespace Contracts.Settings.UpdateDatabase;

/// <summary>How to reach PostgreSQL from now on.</summary>
public sealed record Request
{
    /// <summary>The server's host name or address.</summary>
    public required string Host { get; init; }

    /// <summary>The port it listens on.</summary>
    public required int Port { get; init; }

    /// <summary>The database name.</summary>
    public required string Name { get; init; }

    /// <summary>The role to connect as. Never a superuser.</summary>
    public required string Username { get; init; }

    /// <summary>
    /// A new password, or null to keep the one already set. Write-only: no
    /// response ever carries it back.
    /// </summary>
    public string? Password { get; init; }

    /// <summary>Whether the connection must use TLS.</summary>
    public required bool RequireSsl { get; init; }

    /// <summary>The most connections to keep open.</summary>
    public required int MaxPoolSize { get; init; }
}
