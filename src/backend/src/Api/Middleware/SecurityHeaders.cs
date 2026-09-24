namespace Api.Middleware;

/// <summary>
/// The security headers, and the reason each one is set.
/// </summary>
/// <remarks>
/// Deliberately absent: HSTS, an HTTPS redirect, and response compression. TLS
/// terminates at the operator's reverse proxy, which owns HSTS, and compressing
/// cookie-authenticated responses invites BREACH.
/// </remarks>
internal static class SecurityHeaders
{
    /// <summary>Stops a browser MIME-sniffing a response into script.</summary>
    internal const string ContentTypeOptions = "nosniff";

    /// <summary>Legacy clickjacking defence, beside CSP's frame-ancestors.</summary>
    internal const string FrameOptions = "DENY";

    /// <summary>Ids travel in paths, so no referrer may leak to a third party.</summary>
    internal const string ReferrerPolicy = "no-referrer";

    /// <summary>Isolates the browsing context from anything that opened it.</summary>
    internal const string OpenerPolicy = "same-origin";

    /// <summary>Blocks cross-origin embedding of our responses.</summary>
    internal const string ResourcePolicy = "same-origin";

    /// <summary>
    /// Least privilege for device APIs: only the camera is listed, because
    /// taking a recipe photo is the one capability the app asks for.
    /// </summary>
    internal const string PermissionsPolicy = "camera=(self), microphone=(), geolocation=()";

    /// <summary>
    /// A JSON response needs nothing at all, so it is allowed nothing. This is
    /// the policy that applies to every API response.
    /// </summary>
    internal const string ApiContentSecurityPolicy =
        "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    /// <summary>
    /// The one style attribute the document may carry: SvelteKit's route
    /// announcer, which its generated root component writes with a fixed
    /// visually-hidden <c>style</c> after the first client-side navigation.
    /// </summary>
    /// <remarks>
    /// A nonce cannot be put on an attribute, so without this the announcer
    /// lost its styles on every signed-out deep link and the browser logged a
    /// violation. <c>unsafe-hashes</c> allows this exact value and nothing
    /// else — any other style attribute is still refused. The hash is of the
    /// string in <c>@sveltejs/kit</c>'s <c>write_root.js</c>; if an upgrade
    /// changes it, the <c>@image</c> end-to-end spec reports the violation.
    /// </remarks>
    internal const string AnnouncerStyleHash = "sha256-S8qMpvofolR8Mpjy4kQvEm7m1q8clzU4dfDH0AmvZjo=";

    /// <summary>
    /// The SPA document's policy. There is no <c>unsafe-inline</c> and no
    /// <c>unsafe-eval</c> anywhere — either one disables the protection the
    /// rest of the policy provides. The inline blocks the app needs (the theme
    /// applied before first paint, SvelteKit's boot script, and the boot
    /// screen's styles) carry the per-response nonce.
    /// </summary>
    /// <remarks>
    /// The nonce is named for styles as well as scripts. Without it
    /// <c>style-src 'self'</c> blocks the inline block <em>and</em> every
    /// <c>style</c> attribute in the document, which is how the boot screen
    /// came to render as unstyled text in the corner in production while
    /// looking correct under the dev server, which sets no policy at all.
    /// </remarks>
    internal static string DocumentContentSecurityPolicy(string nonce) =>
        string.Join(
            "; ",
            "default-src 'self'",
            $"script-src 'self' 'nonce-{nonce}'",
            $"style-src 'self' 'nonce-{nonce}'",
            $"style-src-attr 'unsafe-hashes' '{AnnouncerStyleHash}'",
            "img-src 'self' data: blob:",
            "connect-src 'self'",
            "font-src 'self'",
            "manifest-src 'self'",
            "worker-src 'self'",
            "object-src 'none'",
            "base-uri 'none'",
            "frame-ancestors 'none'",
            "form-action 'self'");
}
