using System.Security.Cryptography;
using Api.Infrastructure;

namespace Api.Middleware;

/// <summary>
/// The one place any security header is set.
/// </summary>
/// <remarks>
/// <para>
/// Runs third in the pipeline, before anything can begin writing a body, and
/// applies the headers in <c>OnStarting</c>, just before they are sent. Set any
/// earlier, they did not survive a defect: the exception handler clears every
/// header before it writes the problem document, so a 500 went out with no
/// policy at all. The callback is not cleared with them.
/// </para>
/// <para>
/// The caching decision waits for <c>OnStarting</c> too, because whether a
/// response is authenticated is not known this early. By then the endpoint has
/// had its chance to set a deliberate policy (a conditional read sets
/// <c>private, no-cache</c>), and anything else authenticated falls back to
/// <c>no-store</c> so private data stays out of shared caches.
/// </para>
/// </remarks>
/// <param name="next">The rest of the pipeline.</param>
internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const int NonceBytes = 16;

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Decided now, not as the response starts: the app shell reads the
        // nonce while it renders.
        var contentSecurityPolicy = ContentSecurityPolicyFor(context);

        context.Response.OnStarting(() =>
        {
            ApplySecurityHeaders(context.Response.Headers, contentSecurityPolicy);
            ApplyCachePolicy(context);

            return Task.CompletedTask;
        });

        return next(context);
    }

    private static void ApplySecurityHeaders(IHeaderDictionary headers, string contentSecurityPolicy)
    {
        headers.XContentTypeOptions = SecurityHeaders.ContentTypeOptions;
        headers.XFrameOptions = SecurityHeaders.FrameOptions;
        headers["Referrer-Policy"] = SecurityHeaders.ReferrerPolicy;
        headers["Cross-Origin-Opener-Policy"] = SecurityHeaders.OpenerPolicy;
        headers["Cross-Origin-Resource-Policy"] = SecurityHeaders.ResourcePolicy;
        headers["Permissions-Policy"] = SecurityHeaders.PermissionsPolicy;
        headers.ContentSecurityPolicy = contentSecurityPolicy;
    }

    private static string ContentSecurityPolicyFor(HttpContext context)
    {
        if (ApiPaths.IsApi(context.Request.Path))
        {
            return SecurityHeaders.ApiContentSecurityPolicy;
        }

        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(NonceBytes));

        RequestContext.SetCspNonce(context, nonce);

        return SecurityHeaders.DocumentContentSecurityPolicy(nonce);
    }

    private static void ApplyCachePolicy(HttpContext context)
    {
        var isAuthenticated = context.User.Identity?.IsAuthenticated == true;

        if (isAuthenticated && string.IsNullOrEmpty(context.Response.Headers.CacheControl))
        {
            context.Response.Headers.CacheControl = "no-store";
        }
    }
}
