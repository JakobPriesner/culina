namespace Api.Middleware;

/// <summary>The security headers, and the reason each one is set.</summary>
/// <remarks>
/// Absent on purpose: HSTS and the HTTPS redirect (TLS ends at the operator's proxy) and
/// compression (BREACH).
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

    /// <summary>Least privilege for device APIs: only the camera, for recipe photos.</summary>
    internal const string PermissionsPolicy = "camera=(self), microphone=(), geolocation=()";

    /// <summary>
    /// A JSON response needs nothing, so it is allowed nothing; applies to every API response.
    /// </summary>
    internal const string ApiContentSecurityPolicy =
        "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    /// <summary>
    /// The one style attribute the document may carry: SvelteKit's route announcer, hidden via a
    /// fixed <c>style</c>.
    /// </summary>
    /// <remarks>
    /// A nonce cannot go on an attribute; <c>unsafe-hashes</c> allows only this exact value. If a
    /// <c>@sveltejs/kit</c> upgrade changes the string, the <c>@image</c> end-to-end spec reports
    /// the violation.
    /// </remarks>
    internal const string AnnouncerStyleHash = "sha256-S8qMpvofolR8Mpjy4kQvEm7m1q8clzU4dfDH0AmvZjo=";

    /// <summary>The only Trusted Types policies a page may create.</summary>
    /// <remarks>
    /// No <c>default</c> policy, so a library writing strings into the DOM is refused;
    /// <c>culina-worker-url</c> lives in <c>updates.svelte.ts</c>.
    /// </remarks>
    internal const string TrustedTypesPolicies =
        "svelte-trusted-html sveltekit-trusted-url culina-worker-url";

    /// <summary>
    /// The SPA document's policy: no <c>unsafe-inline</c> or <c>unsafe-eval</c>; the inline blocks
    /// carry the per-response nonce.
    /// </summary>
    /// <remarks>
    /// The nonce covers styles too: without it <c>style-src 'self'</c> blocks inline styles, which
    /// the dev server (no policy) hides.
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
            "form-action 'self'",
            // DOM sinks that take script must be given a value a named policy made.
            "require-trusted-types-for 'script'",
            $"trusted-types {TrustedTypesPolicies}");
}
