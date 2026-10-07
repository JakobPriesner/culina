using Api.Infrastructure;
using Application.Abstractions.Settings;
using Application.Telemetry;

namespace Api.Middleware;

/// <summary>Requires unsafe cookie-authenticated requests to come from our own origin.</summary>
/// <remarks>
/// A cheap check ahead of the CSRF token comparison. The origin comes from the request's own scheme and host
/// (forwarded headers are already applied), so there is no origin list to go stale.
/// </remarks>
internal sealed class SameOriginMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(
        HttpContext context,
        CookieSettings cookies,
        ILogger<SameOriginMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(cookies);
        ArgumentNullException.ThrowIfNull(logger);

        if (SafeMethods.Includes(context.Request.Method)
            || !CarriesSessionCookie(context.Request, cookies))
        {
            return next(context);
        }

        if (IsOurs(context.Request))
        {
            return next(context);
        }

        CulinaTelemetry.ForeignOriginRejections.Add(1);
        logger.Rejected(context.Request, RequestErrors.ForeignOrigin.Code);

        return CustomResults.WriteProblemAsync(context, RequestErrors.ForeignOrigin);
    }

    // The cookie name depends on Secure (no __Host- prefix over plain HTTP in development); asking the same
    // source the cookie is written from keeps this guard identical to the shipped one.
    private static bool CarriesSessionCookie(HttpRequest request, CookieSettings cookies) =>
        request.Cookies.ContainsKey(Authentication.SessionCookies.Name(cookies));

    private static bool IsOurs(HttpRequest request)
    {
        var expected = $"{request.Scheme}://{request.Host.Value}";

        if (request.Headers.Origin.Count > 0)
        {
            return string.Equals(request.Headers.Origin[0], expected, StringComparison.OrdinalIgnoreCase);
        }

        // Some browsers omit Origin on same-origin navigations, so Referer is the fallback; with neither, rejected.
        return request.Headers.Referer.Count > 0
            && Uri.TryCreate(request.Headers.Referer[0], UriKind.Absolute, out var referer)
            && string.Equals(
                $"{referer.Scheme}://{referer.Authority}",
                expected,
                StringComparison.OrdinalIgnoreCase);
    }
}
