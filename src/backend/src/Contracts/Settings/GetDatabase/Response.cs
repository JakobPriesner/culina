namespace Contracts.Settings.GetDatabase;

/// <summary>
/// How this instance reaches PostgreSQL. Never the password.
/// </summary>
public sealed record Response
{
    /// <summary>The server's host name or address, or empty when none is set.</summary>
    public required string Host { get; init; }

    /// <summary>The port it listens on.</summary>
    public required int Port { get; init; }

    /// <summary>The database name, or empty when none is set.</summary>
    public required string Name { get; init; }

    /// <summary>The role Culina connects as, or empty when none is set.</summary>
    public required string Username { get; init; }

    /// <summary>Whether a password has been set. Never the password.</summary>
    public required bool PasswordConfigured { get; init; }

    /// <summary>Whether the connection must use TLS.</summary>
    public required bool RequireSsl { get; init; }

    /// <summary>The most connections Culina keeps open.</summary>
    public required int MaxPoolSize { get; init; }

    /// <summary>
    /// The settings the deployment fixes, by the environment variable that sets
    /// them — <c>Database__Host</c>. Saving cannot change these.
    /// </summary>
    public required IReadOnlyList<string> Pinned { get; init; }

    /// <summary>Whether changes can be saved at all.</summary>
    public required bool Writable { get; init; }
}
