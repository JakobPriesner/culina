using Microsoft.AspNetCore.Http.Features;

namespace Api.Infrastructure;

/// <summary>Reads a path segment that carries free text, such as an ingredient name.</summary>
internal static class PathSegments
{
    /// <summary>
    /// The last segment of the request path, percent-decoded.
    /// </summary>
    /// <remarks>
    /// ASP.NET Core decodes the path before routing but leaves <c>%2F</c> encoded (a decoded slash
    /// would change which route matches), so a route value for "Salz/Pfeffer" arrives as
    /// <c>Salz%2FPfeffer</c> while "100%" arrives as <c>100%</c>, and no later step can tell them
    /// apart. The raw request target is unambiguous: split it first, decode second.
    /// </remarks>
    /// <param name="context">The request.</param>
    /// <param name="routeValue">What routing made of the segment, used when no raw target is known.</param>
    internal static string LastDecoded(HttpContext context, string routeValue)
    {
        ArgumentNullException.ThrowIfNull(context);

        var target = context.Features.Get<IHttpRequestFeature>()?.RawTarget;

        if (string.IsNullOrEmpty(target))
        {
            // No raw target (an in-memory host): %2F is the only thing routing leaves encoded.
            return routeValue.Replace("%2F", "/", StringComparison.OrdinalIgnoreCase);
        }

        var path = target.Split('?', 2)[0];

        return Uri.UnescapeDataString(path[(path.LastIndexOf('/') + 1)..]);
    }
}
