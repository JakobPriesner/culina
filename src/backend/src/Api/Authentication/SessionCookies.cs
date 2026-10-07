using Application.Abstractions.Settings;

namespace Api.Authentication;

/// <summary>
/// Writes, renews and clears the session and CSRF cookies, always together with the same expiry.
/// </summary>
/// <remarks>
/// The session cookie is <c>HttpOnly</c> and <c>__Host-</c> prefixed (origin-pinned); the CSRF cookie is
/// readable on purpose, since the client must echo it in a header an attacker's page cannot build.
/// </remarks>
internal static class SessionCookies
{
    internal static void Write(
        HttpContext context,
        CookieSettings settings,
        DateTimeOffset now,
        string sessionToken,
        string csrfToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);

        Append(context, settings, now, Name(settings), sessionToken, httpOnly: true);
        Append(context, settings, now, CookieSettings.CsrfCookieName, csrfToken, httpOnly: false);
    }

    /// <summary>
    /// Slides the expiry of the cookies this request arrived with, so the browser does not drop them
    /// before the server-side session expires. Values are reused; no token is minted.
    /// </summary>
    internal static void Renew(HttpContext context, CookieSettings settings, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);

        if (Read(context, settings) is not { Length: > 0 } sessionToken)
        {
            return;
        }

        Append(context, settings, now, Name(settings), sessionToken, httpOnly: true);

        // Renewed only if the browser still has it: minting one here would rotate the stored digest mid-authentication.
        if (context.Request.Cookies.TryGetValue(CookieSettings.CsrfCookieName, out var csrfToken)
            && csrfToken.Length > 0)
        {
            Append(context, settings, now, CookieSettings.CsrfCookieName, csrfToken, httpOnly: false);
        }
    }

    internal static void Clear(HttpContext context, CookieSettings settings)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);

        var options = new CookieOptions
        {
            Secure = settings.Secure,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        };

        context.Response.Cookies.Delete(Name(settings), options);
        context.Response.Cookies.Delete(CookieSettings.CsrfCookieName, options);
    }

    /// <summary>Reads the session token, whichever name this deployment uses.</summary>
    internal static string? Read(HttpContext context, CookieSettings settings)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);

        return context.Request.Cookies.TryGetValue(Name(settings), out var token) ? token : null;
    }

    // Plain-HTTP development rejects a __Host- cookie, so the prefix is dropped when Cookies__Secure is false.
    private const string DevelopmentSessionCookieName = "culina.session";

    internal static string Name(CookieSettings settings) =>
        settings.Secure ? CookieSettings.SessionCookieName : DevelopmentSessionCookieName;

    private static void Append(
        HttpContext context,
        CookieSettings settings,
        DateTimeOffset now,
        string name,
        string value,
        bool httpOnly) =>
        context.Response.Cookies.Append(
            name,
            value,
            new CookieOptions
            {
                HttpOnly = httpOnly,
                Secure = settings.Secure,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                Expires = now.Add(settings.SessionLifetime),
                IsEssential = true
            });
}
