using Application.Abstractions.Settings;
using Domain.Shared;

namespace Application.Abstractions;

/// <summary>Hands one mail to the configured SMTP server.</summary>
public interface IMailTransport
{
    /// <summary>Sends a mail now, through the given account.</summary>
    /// <param name="settings">The account to send from.</param>
    /// <param name="mail">What to send.</param>
    /// <param name="cancellationToken">Cancels the attempt.</param>
    /// <returns>Success, or <c>mail.send_failed</c> saying what the server or network answered.</returns>
    Task<Result> SendAsync(MailSettings settings, OutgoingMail mail, CancellationToken cancellationToken);
}
