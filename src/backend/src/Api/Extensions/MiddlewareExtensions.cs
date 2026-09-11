using Api.Infrastructure;
using Api.Middleware;

namespace Api.Extensions;

/// <summary>
/// One named extension per middleware.
/// </summary>
/// <remarks>
/// <c>app.UseMiddleware&lt;T&gt;()</c> is never written inline in
/// <c>Program.cs</c>: the extension's name is what makes the pipeline readable
/// as a list of intentions rather than a list of types.
/// </remarks>
internal static class MiddlewareExtensions
{
    /// <summary>Correlation id, response header and logging scope.</summary>
    internal static IApplicationBuilder UseRequestContext(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<RequestContextMiddleware>();
    }

    /// <summary>Every security header, and the per-response CSP nonce.</summary>
    internal static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<SecurityHeadersMiddleware>();
    }

    /// <summary>Rejects query parameters an endpoint does not declare.</summary>
    internal static IApplicationBuilder UseQueryParameterGuard(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<QueryParameterGuardMiddleware>();
    }

    /// <summary>Requires unsafe cookie-authenticated requests to be same-origin.</summary>
    internal static IApplicationBuilder UseSameOriginGuard(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<SameOriginMiddleware>();
    }

    /// <summary>Gives framework-generated statuses a problem document body.</summary>
    internal static IApplicationBuilder UseProblemStatusPages(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseStatusCodePages(context =>
            StatusCodeProblems.WriteAsync(context.HttpContext));
    }
}
