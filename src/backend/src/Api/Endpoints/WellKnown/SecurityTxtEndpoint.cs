using System.Globalization;
using Application.Abstractions.Settings;

namespace Api.Endpoints.WellKnown;

/// <summary>
/// <c>/.well-known/security.txt</c> (RFC 9116), written from this instance's
/// own configuration.
/// </summary>
/// <remarks>
/// <para>
/// Rendered per request rather than built into the app: the image is the same
/// for every instance, and the contact belongs to whoever runs this one. With
/// no <c>Site__SecurityContact</c> it is a <c>404</c> — which is also what
/// stops the app shell answering this path with a page of HTML.
/// </para>
/// <para>
/// Not under <c>/api</c> and not authenticated, like the health endpoints:
/// it is for strangers.
/// </para>
/// </remarks>
internal static class SecurityTxtEndpoint
{
    /// <summary>
    /// How far ahead <c>Expires</c> is. The RFC asks for less than a year, and
    /// since the file is written fresh for every request, a date half a year
    /// out is always half a year out.
    /// </summary>
    private static readonly TimeSpan FreshFor = TimeSpan.FromDays(180);

    internal static WebApplication MapSecurityTxt(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/.well-known/security.txt", (SiteSettings site, TimeProvider time) =>
                site.SecurityContact is { } contact
                    ? Results.Text(Render(contact, site.Url, time.GetUtcNow()), "text/plain; charset=utf-8")
                    : Results.NotFound())
            .WithName("securityTxt")
            .ExcludeFromDescription()
            .AllowAnonymous();

        return app;
    }

    private static string Render(Uri contact, Uri? site, DateTimeOffset now)
    {
        var expires = now.UtcDateTime.Add(FreshFor).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

        List<string> lines =
        [
            $"Contact: {contact.OriginalString}",
            $"Expires: {expires}",
            "Preferred-Languages: en, de"
        ];

        if (site is not null)
        {
            lines.Add($"Canonical: {new Uri(site, "/.well-known/security.txt")}");
        }

        return string.Join('\n', lines) + "\n";
    }
}
