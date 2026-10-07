using Api.Infrastructure;
using Api.Middleware;

namespace Api.Extensions;

/// <summary>
/// One named extension per middleware, so <c>Program.cs</c> reads as a list of intentions rather
/// than of types.
/// </summary>
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

    /// <summary>Puts the authenticated user on the logging scope and the span.</summary>
    internal static IApplicationBuilder UseSessionContext(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<SessionContextMiddleware>();
    }

    /// <summary>Requires unsafe cookie-authenticated requests to carry the CSRF token.</summary>
    internal static IApplicationBuilder UseCsrfGuard(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<CsrfMiddleware>();
    }

    /// <summary>Counts the costly limits against the signed-in person, not the session.</summary>
    internal static IApplicationBuilder UsePersonalRateLimits(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<PersonalRateLimitMiddleware>();
    }

    /// <summary>Gives framework-generated statuses a problem document body.</summary>
    internal static IApplicationBuilder UseProblemStatusPages(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseStatusCodePages(context =>
            StatusCodeProblems.WriteAsync(context.HttpContext));
    }
}
