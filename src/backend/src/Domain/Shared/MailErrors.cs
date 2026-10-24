namespace Domain.Shared;

/// <summary>Failures when setting up or testing the mail account.</summary>
public static class MailErrors
{
    /// <summary>Mail switched on without a server or a sender.</summary>
    public static readonly Error Incomplete = new(
        "mail.incomplete",
        "Mail can only be switched on with a server and a sender address.",
        ErrorType.Validation);

    /// <summary>Mail switched on without a public address for the links in it.</summary>
    public static readonly Error SiteUrlRequired = new(
        "mail.site_url_required",
        "Mail can only be switched on once Culina knows its public address, for the links in it. Set Site__Url, such as https://culina.example.com.",
        ErrorType.Validation);

    /// <summary>A stored password would be sent to a server it was not entered for.</summary>
    public static readonly Error PasswordRequired = new(
        "mail.password_required",
        "Enter the password again: it is only sent to the server it was saved for.",
        ErrorType.Validation);

    /// <summary>A value of the mail account that cannot be right.</summary>
    public static readonly Error InvalidValue = new(
        "mail.invalid_value",
        "That is not a value the mail account accepts.",
        ErrorType.Validation);

    /// <summary>Mail is off, so a test has nothing to send through.</summary>
    public static readonly Error Off = new(
        "mail.off",
        "Mail is switched off. Switch it on and save first.",
        ErrorType.Conflict);

    /// <summary>The server or the network refused a mail.</summary>
    /// <param name="reason">What the server or network said.</param>
    public static Error SendFailed(string reason) => new(
        "mail.send_failed",
        $"The mail could not be sent. The server or network said: {reason.TrimEnd('.')}.",
        ErrorType.Unavailable);
}
