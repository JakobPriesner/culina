namespace Api.Authentication;

/// <summary>Reads an app's session token from <c>Authorization: Bearer</c>.</summary>
/// <remarks>
/// The same token a browser holds in the session cookie, for a client that cannot keep a cookie: the
/// installed apps call the instance from their own origin, where WebKit drops third-party cookies
/// (docs/native-app-design.md, 3.3). A bearer header is never attached by a browser on its own,
/// so a request carrying one needs no CSRF token.
/// </remarks>
internal static class BearerToken
{
    private const string Scheme = "Bearer ";

    internal static string? Read(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Request.Headers.Authorization is [{ } header]
            && header.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase)
            && header[Scheme.Length..].Trim() is { Length: > 0 } token
                ? token
                : null;
    }
}
