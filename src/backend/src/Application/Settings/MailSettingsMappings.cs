using Application.Abstractions.Settings;
using Response = Contracts.Settings.GetMail.Response;

namespace Application.Settings;

/// <summary>Maps the mail account onto what the screen reads; the password never leaves.</summary>
internal static class MailSettingsMappings
{
    internal static Response ToResponse(this MailSettings settings, SiteSettings site)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(site);

        return new Response
        {
            Enabled = settings.Enabled,
            Host = settings.Host,
            Port = settings.Port,
            Security = settings.Security,
            Username = settings.Username,
            PasswordConfigured = settings.HasPassword,
            FromAddress = settings.FromAddress,
            FromName = settings.FromName,
            SiteUrl = site.Url?.AbsoluteUri,
            Sending = settings.IsOn && site.Url is not null
        };
    }
}
