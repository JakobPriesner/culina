using System.Globalization;
using Application.Abstractions.Settings;

namespace Api.Endpoints.WellKnown;

/// <summary><c>/.well-known/security.txt</c> (RFC 9116), written per request from this instance's configuration.</summary>
/// <remarks>A <c>404</c> without <c>Site__SecurityContact</c>, which also stops the app shell answering this path. Unauthenticated and outside <c>/api</c>.</remarks>
internal static class SecurityTxtEndpoint
{
    // The RFC asks for under a year; the file is written fresh per request, so half a year is always half a year out.
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
