using Application.Abstractions;
using Application.Abstractions.Settings;
using Domain.Shared;
using Domain.Users;

namespace Application.Mail;

/// <summary>
/// Every mail Culina sends about an account: makes the secret a link needs, words the mail in the recipient's
/// language and queues it, in the caller's transaction. Does nothing at all while mail is off.
/// </summary>
internal sealed class AccountMail(
    MailSettings settings,
    SiteSettings site,
    IMailOutbox outbox,
    IEmailTokenRepository emailTokens,
    IRecoveryCodeRepository recoveryCodes,
    IUserPreferencesRepository preferences,
    IDeviceLanguage device,
    ISecretTokens tokens,
    TimeProvider time)
{
    /// <summary>Whether mail goes out: switched on, complete, and with a public address for the links.</summary>
    internal bool Sends => settings.IsOn && site.Url is not null;

    /// <summary>Mails a link that confirms the account's address.</summary>
    internal async Task SendConfirmationAsync(User user, CancellationToken cancellationToken)
    {
        if (!Sends)
        {
            return;
        }

        var token = await IssueAsync(user, EmailTokenPurpose.EmailConfirmation, cancellationToken).ConfigureAwait(false);
        var language = await LanguageOfAsync(user, cancellationToken).ConfigureAwait(false);

        await EnqueueAsync(
            user,
            MailWording.ConfirmAddress(language, user.DisplayName.Value, Link($"confirm-email#token={token}")),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Mails a link that signs the owner in without a password.</summary>
    internal async Task SendSignInLinkAsync(User user, CancellationToken cancellationToken)
    {
        if (!Sends)
        {
            return;
        }

        var token = await IssueAsync(user, EmailTokenPurpose.SignIn, cancellationToken).ConfigureAwait(false);
        var language = await LanguageOfAsync(user, cancellationToken).ConfigureAwait(false);

        await EnqueueAsync(
            user,
            MailWording.SignInLink(language, user.DisplayName.Value, Link($"login/link#token={token}")),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Mails a one-hour recovery code, as a link to the reset page and as a code to type.</summary>
    internal async Task SendPasswordResetAsync(User user, CancellationToken cancellationToken)
    {
        if (!Sends)
        {
            return;
        }

        var code = tokens.NewRecoveryCode();

        await recoveryCodes
            .AddAsync(RecoveryCode.Mailed(user.Id, tokens.Digest(RecoveryCode.Normalise(code)), time.GetUtcNow()), cancellationToken)
            .ConfigureAwait(false);

        var language = await LanguageOfAsync(user, cancellationToken).ConfigureAwait(false);
        var link = Link($"password-reset#email={Uri.EscapeDataString(user.Email.Value)}&code={code}");

        await EnqueueAsync(
            user,
            MailWording.PasswordReset(language, user.DisplayName.Value, link, code),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Tells the owner a device it has not seen them use signed in.</summary>
    internal Task TellNewDeviceAsync(User user, string description, DateTimeOffset at, CancellationToken cancellationToken) =>
        TellAsync(
            user,
            language => MailWording.NewDevice(language, user.DisplayName.Value, description, at, Link("me/security")),
            cancellationToken);

    /// <summary>Tells the owner their password changed.</summary>
    internal Task TellPasswordChangedAsync(User user, DateTimeOffset at, CancellationToken cancellationToken) =>
        TellAsync(
            user,
            language => MailWording.PasswordChanged(language, user.DisplayName.Value, at, Link("password-reset")),
            cancellationToken);

    /// <summary>Tells the owner a new set of recovery codes was made.</summary>
    internal Task TellRecoveryCodesMadeAsync(User user, DateTimeOffset at, CancellationToken cancellationToken) =>
        TellAsync(
            user,
            language => MailWording.RecoveryCodesMade(language, user.DisplayName.Value, at, Link("me/security")),
            cancellationToken);

    /// <summary>Tells the owner the administrator issued a recovery code for them.</summary>
    internal Task TellRecoveryCodeIssuedAsync(User user, DateTimeOffset at, CancellationToken cancellationToken) =>
        TellAsync(
            user,
            language => MailWording.RecoveryCodeIssued(language, user.DisplayName.Value, at, Link("me/security")),
            cancellationToken);

    /// <summary>Mails an invitation to somebody who may not have an account, in the inviter's language.</summary>
    /// <returns>Whether it was queued; false while mail is off.</returns>
    internal async Task<bool> InviteAsync(
        Email to,
        string inviter,
        string household,
        string code,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(to);

        if (!Sends)
        {
            return false;
        }

        var words = MailWording.Invitation(device.Current, inviter, household, expiresAt, Link($"join/{Uri.EscapeDataString(code)}"));

        await outbox.EnqueueAsync(MailLayout.Render(to.Value, words), cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>The test mail, ready to hand to the transport directly.</summary>
    internal async Task<OutgoingMail> TestAsync(User user, CancellationToken cancellationToken)
    {
        var language = await LanguageOfAsync(user, cancellationToken).ConfigureAwait(false);

        return MailLayout.Render(user.Email.Value, MailWording.Test(language, user.DisplayName.Value));
    }

    private async Task TellAsync(User user, Func<Language, MailWords> words, CancellationToken cancellationToken)
    {
        if (!Sends)
        {
            return;
        }

        var language = await LanguageOfAsync(user, cancellationToken).ConfigureAwait(false);

        await EnqueueAsync(user, words(language), cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> IssueAsync(User user, EmailTokenPurpose purpose, CancellationToken cancellationToken)
    {
        var token = tokens.NewToken();

        await emailTokens
            .AddAsync(EmailToken.Issue(user.Id, purpose, tokens.Digest(token), time.GetUtcNow()), cancellationToken)
            .ConfigureAwait(false);

        return token;
    }

    private Task EnqueueAsync(User user, MailWords words, CancellationToken cancellationToken) =>
        outbox.EnqueueAsync(MailLayout.Render(user.Email.Value, words), cancellationToken);

    /// <summary>The language the person chose, or the asking device's when they chose none.</summary>
    private async Task<Language> LanguageOfAsync(User user, CancellationToken cancellationToken) =>
        (await preferences.GetAsync(user.Id, cancellationToken).ConfigureAwait(false)).Language ?? device.Current;

    /// <summary>
    /// A page of this instance. Secrets go after <c>#</c>: a fragment is never sent to a server, so it stays out of
    /// access logs, proxies and Referer headers.
    /// </summary>
    private Uri Link(string pathAndFragment) =>
        new($"{site.Url!.AbsoluteUri.TrimEnd('/')}/{pathAndFragment}");
}
