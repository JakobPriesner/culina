using Application.Abstractions.Settings;

namespace Api.Authentication;

/// <summary>
/// Writes and clears the two cookies a session needs.
/// </summary>
/// <remarks>
/// <para>
/// The session cookie is <c>HttpOnly</c>, so a cross-site script cannot read
/// it. The CSRF cookie deliberately is not: the client has to read it to echo
/// the value in a header, and that echo is the whole point — an attacker's
/// page can cause a request to be sent with our cookies, but cannot read them
/// to build the matching header.
/// </para>
/// <para>
/// The <c>__Host-</c> prefix pins the session cookie to this exact origin: the
/// browser refuses it unless it is <c>Secure</c>, has <c>Path=/</c> and carries
/// no <c>Domain</c>, so a subdomain cannot set or overwrite it.
/// </para>
/// </remarks>
internal static class SessionCookies
{
    internal static void Write(
        HttpContext context,
        CookieSettings settings,
        string sessionToken,
        string csrfToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);

        var expiry = DateTimeOffset.UtcNow.AddDays(settings.SessionDays);

        context.Response.Cookies.Append(
            settings.Secure ? CookieSettings.SessionCookieName : DevelopmentSessionCookieName,
            sessionToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = settings.Secure,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                Expires = expiry,
                IsEssential = true
            });

        context.Response.Cookies.Append(
            CookieSettings.CsrfCookieName,
            csrfToken,
            new CookieOptions
            {
                HttpOnly = false,
                Secure = settings.Secure,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                Expires = expiry,
                IsEssential = true
            });
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

    /// <summary>
    /// Local development runs over plain HTTP, where the browser rejects a
    /// <c>__Host-</c> cookie outright. The prefix is dropped only when
    /// <c>Cookies__Secure</c> is false, which production never sets.
    /// </summary>
    private const string DevelopmentSessionCookieName = "culina.session";

    internal static string Name(CookieSettings settings) =>
        settings.Secure ? CookieSettings.SessionCookieName : DevelopmentSessionCookieName;
}
