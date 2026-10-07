namespace Api.Infrastructure;

/// <summary>Tells API requests apart from single-page-app requests; one definition so the middlewares cannot disagree.</summary>
internal static class ApiPaths
{
    internal const string Prefix = "/api";

    internal const string V1 = "/api/v1";

    internal static bool IsApi(PathString path) => path.StartsWithSegments(Prefix);
}
