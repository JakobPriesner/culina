namespace Application.Abstractions.Settings;

/// <summary>The mail account Culina sends from; off and empty by default, because a self-hoster may have none.</summary>
/// <remarks>
/// With mail off, nothing changes from an instance that never had it: recovery is by saved or issued codes, and
/// nobody is asked to confirm an address.
/// </remarks>
public sealed record MailSettings : IInstanceSettings<MailSettings>
{
    /// <summary>What the password is protected for; changing it invalidates the stored password.</summary>
    public const string PasswordProtection = "Culina.Mail.Password.v1";

    /// <summary>No security: only for a relay on the same machine or network.</summary>
    public const string SecurityNone = "none";

    /// <summary>Plain connection upgraded with STARTTLS, usually on port 587.</summary>
    public const string SecurityStartTls = "starttls";

    /// <summary>TLS from the first byte, usually on port 465.</summary>
    public const string SecurityTls = "tls";

    /// <summary>Longer than any host name, address, user name or password a mail provider hands out.</summary>
    public const int MaxLength = 320;

    /// <summary>The key this group is stored under.</summary>
    public static string GroupName => "mail";

    /// <summary>Whether Culina sends mail at all; lets an administrator stop it without clearing the account.</summary>
    public bool Enabled { get; set; }

    /// <summary>The SMTP server, such as <c>smtp.example.com</c>.</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>The SMTP port.</summary>
    public int Port { get; set; } = 587;

    /// <summary><see cref="SecurityNone"/>, <see cref="SecurityStartTls"/> or <see cref="SecurityTls"/>.</summary>
    public string Security { get; set; } = SecurityStartTls;

    /// <summary>Who to sign in to the server as, or empty for a server that takes mail without signing in.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>The encrypted password. Never leaves the server; contracts expose <c>passwordConfigured</c> instead.</summary>
    public string ProtectedPassword { get; set; } = string.Empty;

    /// <summary>The sender's address, such as <c>culina@example.com</c>.</summary>
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>The sender's name, as the inbox shows it.</summary>
    public string FromName { get; set; } = "Culina";

    /// <summary>Whether a password has been entered.</summary>
    public bool HasPassword => ProtectedPassword.Length > 0;

    /// <summary>Whether there is enough to send anything: a server and a sender.</summary>
    public bool IsComplete => Host.Length > 0 && FromAddress.Length > 0;

    /// <summary>Whether mail is on and could be sent.</summary>
    public bool IsOn => Enabled && IsComplete;

    /// <inheritdoc/>
    public void CopyFrom(MailSettings other)
    {
        ArgumentNullException.ThrowIfNull(other);

        Enabled = other.Enabled;
        Host = other.Host;
        Port = other.Port;
        Security = other.Security;
        Username = other.Username;
        ProtectedPassword = other.ProtectedPassword;
        FromAddress = other.FromAddress;
        FromName = other.FromName;
    }
}
