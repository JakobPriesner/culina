namespace Contracts.Settings.UpdateMail;

/// <summary>The mail account to send from, all at once.</summary>
public sealed record Request
{
    /// <summary>Whether mail is switched on. Needs a server, a sender and Culina's public address.</summary>
    public required bool Enabled { get; init; }

    /// <summary>The SMTP server, such as <c>smtp.example.com</c>.</summary>
    public required string Host { get; init; }

    /// <summary>The SMTP port, usually 587 for <c>starttls</c> and 465 for <c>tls</c>.</summary>
    public required int Port { get; init; }

    /// <summary><c>none</c>, <c>starttls</c> or <c>tls</c>.</summary>
    public required string Security { get; init; }

    /// <summary>Who to sign in to the server as, or empty for a server that takes mail without signing in.</summary>
    public required string Username { get; init; }

    /// <summary>A new password, or null to keep the stored one. An empty string removes it.</summary>
    /// <remarks>Null with a different <c>host</c> is refused with <c>mail.password_required</c>: a stored password is only sent to the server it was saved for.</remarks>
    public string? Password { get; init; }

    /// <summary>The sender's address.</summary>
    public required string FromAddress { get; init; }

    /// <summary>The sender's name, as inboxes show it.</summary>
    public required string FromName { get; init; }
}
