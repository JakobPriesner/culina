namespace Application.Abstractions.Settings;

/// <summary>
/// How to reach PostgreSQL.
/// </summary>
/// <remarks>
/// Configured as parts rather than as one connection string, so every part can
/// be validated on its own and the password can be sourced separately from the
/// host — a Docker secret for the password, plain environment variables for the
/// rest.
/// </remarks>
public sealed record DatabaseSettings
{
    /// <summary>The configuration section these values are read from.</summary>
    public const string SectionName = "Database";

    /// <summary>The host name or address of the server.</summary>
    public required string Host { get; init; }

    /// <summary>The TCP port the server listens on.</summary>
    public required int Port { get; init; }

    /// <summary>The database name.</summary>
    public required string Name { get; init; }

    /// <summary>The application role to connect as. Never a superuser.</summary>
    public required string Username { get; init; }

    /// <summary>The application role's password.</summary>
    public required string Password { get; init; }

    /// <summary>
    /// Whether TLS is required. Only a local, non-TLS server justifies false.
    /// </summary>
    public bool RequireSsl { get; init; } = true;

    /// <summary>The largest number of pooled connections.</summary>
    public int MaxPoolSize { get; init; } = 20;

    /// <summary>Throws when any value would make the process unable to serve.</summary>
    public void Validate()
    {
        SettingsGuard.NotBlank(Host, SectionName, nameof(Host));
        SettingsGuard.NotBlank(Name, SectionName, nameof(Name));
        SettingsGuard.NotBlank(Username, SectionName, nameof(Username));
        SettingsGuard.NotBlank(Password, SectionName, nameof(Password));
        SettingsGuard.InRange(Port, 1, 65535, SectionName, nameof(Port));
        SettingsGuard.InRange(MaxPoolSize, 1, 500, SectionName, nameof(MaxPoolSize));
    }
}
