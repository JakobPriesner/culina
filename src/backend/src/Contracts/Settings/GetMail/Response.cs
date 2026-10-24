namespace Contracts.Settings.GetMail;

/// <summary>The mail account Culina sends from.</summary>
/// <remarks>A contract type, never the settings record: a password goes in and <c>passwordConfigured</c> comes out.</remarks>
public sealed record Response
{
    /// <summary>Whether mail is switched on.</summary>
    public required bool Enabled { get; init; }

    /// <summary>The SMTP server, or empty.</summary>
    public required string Host { get; init; }

    /// <summary>The SMTP port.</summary>
    public required int Port { get; init; }

    /// <summary><c>none</c>, <c>starttls</c> or <c>tls</c>.</summary>
    public required string Security { get; init; }

    /// <summary>Who Culina signs in to the server as, or empty for no sign-in.</summary>
    public required string Username { get; init; }

    /// <summary>Whether a password has been entered. Never the password.</summary>
    public required bool PasswordConfigured { get; init; }

    /// <summary>The sender's address, or empty.</summary>
    public required string FromAddress { get; init; }

    /// <summary>The sender's name.</summary>
    public required string FromName { get; init; }

    /// <summary>The public address links in mails point to (<c>Site__Url</c>), or null when it is not set and mail cannot be switched on.</summary>
    public string? SiteUrl { get; init; }

    /// <summary>Whether mail actually goes out: switched on, complete, and with a public address.</summary>
    public required bool Sending { get; init; }
}
