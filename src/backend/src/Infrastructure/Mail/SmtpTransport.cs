using Application.Abstractions;
using Application.Abstractions.Settings;
using Domain.Shared;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;

namespace Infrastructure.Mail;

/// <summary>Sends mail through the administrator's SMTP account with MailKit, one connection per mail.</summary>
/// <remarks>
/// A connection per mail, not a pool: Culina sends a handful a day, and a fresh connection always uses the account
/// as it is saved now.
/// </remarks>
internal sealed class SmtpTransport(
    [FromKeyedServices(MailSettings.PasswordProtection)] ISecretProtector protector) : IMailTransport
{
    /// <summary>Long enough for a slow server, short enough that a dead one is given up on.</summary>
    private const int TimeoutMilliseconds = 30_000;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "MailKit, the socket and TLS each throw their own types; every one of them means this mail did not go, and says why.")]
    public async Task<Result> SendAsync(MailSettings settings, OutgoingMail mail, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(mail);

        using var client = new SmtpClient { Timeout = TimeoutMilliseconds };

        try
        {
            await client.ConnectAsync(settings.Host, settings.Port, SecurityOf(settings.Security), cancellationToken)
                .ConfigureAwait(false);

            if (settings.Username.Length > 0)
            {
                // A key ring lost since the password was saved reads as no password: the server says what it thinks.
                var password = settings.HasPassword ? protector.Unprotect(settings.ProtectedPassword) ?? string.Empty : string.Empty;

                await client.AuthenticateAsync(settings.Username, password, cancellationToken).ConfigureAwait(false);
            }

            using var message = Message(settings, mail);

            await client.SendAsync(message, cancellationToken).ConfigureAwait(false);
            await client.DisconnectAsync(quit: true, cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }
        catch (Exception failure) when (failure is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return MailErrors.SendFailed(failure.Message);
        }
    }

    private static SecureSocketOptions SecurityOf(string security) => security switch
    {
        MailSettings.SecurityNone => SecureSocketOptions.None,
        MailSettings.SecurityTls => SecureSocketOptions.SslOnConnect,
        _ => SecureSocketOptions.StartTls
    };

    private static MimeMessage Message(MailSettings settings, OutgoingMail mail)
    {
        var message = new MimeMessage
        {
            Subject = mail.Subject,
            Body = new BodyBuilder { TextBody = mail.Text, HtmlBody = mail.Html }.ToMessageBody()
        };

        message.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(mail.To));

        return message;
    }
}
