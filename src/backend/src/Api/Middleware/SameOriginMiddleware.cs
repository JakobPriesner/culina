using Api.Infrastructure;
using Application.Abstractions.Settings;
using Application.Telemetry;

namespace Api.Middleware;

/// <summary>
/// Requires unsafe cookie-authenticated requests to come from our own origin.
/// </summary>
/// <remarks>
/// <para>
/// <c>SameSite=Lax</c> already blocks a cross-site form post, and the CSRF
/// token is the real defence. This is the cheap check that runs first, so a
/// foreign origin never reaches the token comparison at all.
/// </para>
/// <para>
/// Our origin is derived from the request's own scheme and host, which is
/// correct behind a proxy because forwarded headers have already been applied
/// by then. There is no configured origin list to get out of date.
/// </para>
/// </remarks>
/// <param name="next">The rest of the pipeline.</param>
internal sealed class SameOriginMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(
        HttpContext context,
        ILogger<SameOriginMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        if (IsSafe(context.Request.Method) || !CarriesSessionCookie(context.Request))
        {
            return next(context);
        }

        if (IsOurs(context.Request))
        {
            return next(context);
        }

        CulinaTelemetry.ForeignOriginRejections.Add(1);
        logger.Rejected(context.Request.Method, context.Request.Path, RequestErrors.ForeignOrigin.Code);

        return CustomResults.WriteProblemAsync(context, RequestErrors.ForeignOrigin);
    }

    /// <summary>
    /// Safe methods are exempt, which is only sound because no <c>GET</c>
    /// endpoint in Culina changes state.
    /// </summary>
    private static bool IsSafe(string method) =>
        HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);

    private static bool CarriesSessionCookie(HttpRequest request) =>
        request.Cookies.ContainsKey(CookieSettings.SessionCookieName);

    private static bool IsOurs(HttpRequest request)
    {
        var expected = $"{request.Scheme}://{request.Host.Value}";

        if (request.Headers.Origin.Count > 0)
        {
            return string.Equals(request.Headers.Origin[0], expected, StringComparison.OrdinalIgnoreCase);
        }

        // Some browsers omit Origin on same-origin navigations, so Referer is
        // the documented fallback. A request with neither is rejected: an
        // unsafe cookie-authenticated request that will not say where it came
        // from does not get the benefit of the doubt.
        return request.Headers.Referer.Count > 0
            && Uri.TryCreate(request.Headers.Referer[0], UriKind.Absolute, out var referer)
            && string.Equals(
                $"{referer.Scheme}://{referer.Authority}",
                expected,
                StringComparison.OrdinalIgnoreCase);
    }
}
