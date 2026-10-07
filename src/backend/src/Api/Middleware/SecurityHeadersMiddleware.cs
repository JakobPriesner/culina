using System.Security.Cryptography;
using Api.Infrastructure;

namespace Api.Middleware;

/// <summary>The one place any security header is set.</summary>
/// <remarks>
/// Headers are applied in <c>OnStarting</c> because the exception handler clears headers before writing a problem
/// document, so earlier-set ones did not survive a 500. The caching decision also waits for it: whether a response is
/// authenticated is unknown earlier, and an authenticated response without an endpoint policy falls back to <c>no-store</c>.
/// </remarks>
internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const int NonceBytes = 16;

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Decided now, not as the response starts: the app shell reads the nonce while it renders.
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
