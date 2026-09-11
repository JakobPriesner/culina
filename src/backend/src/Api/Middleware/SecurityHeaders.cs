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
    /// The SPA document's policy. There is no <c>unsafe-inline</c> and no
    /// <c>unsafe-eval</c> anywhere — either one disables the protection the
    /// rest of the policy provides. The single inline script the app needs (the
    /// theme applied before first paint) carries the per-response nonce.
    /// </summary>
    internal static string DocumentContentSecurityPolicy(string nonce) =>
        string.Join(
            "; ",
            "default-src 'self'",
            $"script-src 'self' 'nonce-{nonce}'",
            "style-src 'self'",
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
