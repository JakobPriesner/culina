using Api.Infrastructure;
using Microsoft.Net.Http.Headers;

namespace Api.Middleware;

/// <summary>Lets the installed apps call the API from their own origin.</summary>
/// <remarks>
/// <para>
/// The apps ship the web UI inside them and serve it from a fixed local origin, so every API call is
/// cross-origin (docs/native-app-design.md, 3.3). They authenticate with a bearer token, never a
/// cookie, and this never sends <c>Access-Control-Allow-Credentials</c>: CORS protects ambient
/// credentials, and a page can only send a bearer token it already holds. A browser on any other
/// origin gets no CORS headers at all and is refused by the browser, as before.
/// </para>
/// <para>
/// The headers are added as the response starts, so a problem document written by the exception
/// handler (which clears headers) is still readable by the app.
/// </para>
/// </remarks>
internal sealed class NativeAppCorsMiddleware(RequestDelegate next)
{
    /// <summary>The origins the apps serve their UI from: Capacitor's iOS scheme, and the Android hostname the app sets.</summary>
    internal static readonly IReadOnlyList<string> Origins = ["capacitor://localhost", "https://app.culina.localhost"];

    private const string AllowedMethods = "GET, POST, PUT, PATCH, DELETE";

    private const string AllowedHeaders =
        "Authorization, Content-Type, Accept, If-Match, If-None-Match, Last-Event-ID";

    // Without these an app cannot read the validators its ETag cache and retry logic depend on.
    private const string ExposedHeaders =
        $"ETag, Retry-After, Location, {CulinaHeaders.RequestId}";

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var request = context.Request;

        if (!request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
            || request.Headers.Origin is not [{ } origin]
            || !Origins.Contains(origin, StringComparer.Ordinal))
        {
            return next(context);
        }

        var response = context.Response;

        if (HttpMethods.IsOptions(request.Method)
            && request.Headers.ContainsKey(HeaderNames.AccessControlRequestMethod))
        {
            response.StatusCode = StatusCodes.Status204NoContent;
            response.Headers.AccessControlAllowOrigin = origin;
            response.Headers.AccessControlAllowMethods = AllowedMethods;
            response.Headers.AccessControlAllowHeaders = AllowedHeaders;
            response.Headers.AccessControlMaxAge = "600";
            response.Headers.Vary = HeaderNames.Origin;

            return Task.CompletedTask;
        }

        response.OnStarting(() =>
        {
            response.Headers.AccessControlAllowOrigin = origin;
            response.Headers.AccessControlExposeHeaders = ExposedHeaders;
            response.Headers.Append(HeaderNames.Vary, HeaderNames.Origin);

            return Task.CompletedTask;
        });

        return next(context);
    }
}
