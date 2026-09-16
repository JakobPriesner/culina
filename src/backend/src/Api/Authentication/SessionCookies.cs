using Application.Abstractions.Settings;

namespace Api.Authentication;

/// <summary>
/// Writes, renews and clears the two cookies a session needs.
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
/// <para>
/// Both cookies always carry the same expiry, and both are re-issued together.
/// A browser holding one and not the other is signed in but unable to change
/// anything, which looks like a broken app rather than an expired session.
/// </para>
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
    /// Pushes the expiry of the cookies this request arrived with further out.
    /// </summary>
    /// <remarks>
    /// The server's own record of the session slides whenever it is used, and
    /// without this the browser would still drop the cookie a fixed number of
    /// days after sign-in — which is the same thing to the person holding the
    /// phone. The values are the ones already in the jar, so renewing costs no
    /// new token and invalidates nothing.
    /// </remarks>
    internal static void Renew(HttpContext context, CookieSettings settings, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);

        if (Read(context, settings) is not { Length: > 0 } sessionToken)
        {
            return;
        }

        Append(context, settings, now, Name(settings), sessionToken, httpOnly: true);

        // Renewed only if the browser still has it. Minting a replacement here
        // would mean rotating the stored digest from inside authentication,
        // and a request that raced it would be rejected as forged.
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

    /// <summary>
    /// Local development runs over plain HTTP, where the browser rejects a
    /// <c>__Host-</c> cookie outright. The prefix is dropped only when
    /// <c>Cookies__Secure</c> is false, which production never sets.
    /// </summary>
    private const string DevelopmentSessionCookieName = "culina.session";

    internal static string Name(CookieSettings settings) =>
        settings.Secure ? CookieSettings.SessionCookieName : DevelopmentSessionCookieName;

    /// <summary>
    /// One place that decides a cookie's attributes, so the pair written at
    /// sign-in and the pair written at renewal cannot drift apart.
    /// </summary>
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
