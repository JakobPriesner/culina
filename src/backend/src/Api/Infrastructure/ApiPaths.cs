namespace Api.Infrastructure;

/// <summary>
/// Tells API requests apart from requests for the single-page app.
/// </summary>
/// <remarks>
/// Both are served from the same origin — which is what removes CORS entirely —
/// so several middlewares need to know which of the two they are looking at.
/// One definition, so they cannot disagree.
/// </remarks>
internal static class ApiPaths
{
    internal const string Prefix = "/api";

    internal const string V1 = "/api/v1";

    internal static bool IsApi(PathString path) => path.StartsWithSegments(Prefix);
}
