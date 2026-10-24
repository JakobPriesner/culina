namespace Api.Agents.Authorization;

/// <summary>The addresses Culina answers OAuth at, as an authorization server for its own MCP server.</summary>
/// <remarks>
/// Built from the request's own scheme and host, which forwarded headers have already made the public ones, exactly as
/// the same-origin guard does: so the issuer, the resource and every endpoint agree with the address the person typed
/// into their MCP client, and there is no configured URL to go stale. The protocol endpoints sit outside <c>/api</c>:
/// OAuth asks a server to ignore parameters it does not know (RFC 6749, 3.1), where the API refuses them.
/// </remarks>
internal static class OAuthAddresses
{
    internal const string ProtectedResourceMetadataPath = $"/.well-known/oauth-protected-resource{AgentServerExtensions.Path}";

    internal const string AuthorizationServerMetadataPath = "/.well-known/oauth-authorization-server";

    internal const string AuthorizationPath = "/oauth/authorize";

    internal const string TokenPath = "/oauth/token";

    internal const string RegistrationPath = "/oauth/register";

    /// <summary>The web app's consent screen, where the authorization endpoint sends the browser.</summary>
    internal const string ConsentPath = "/authorize";

    /// <summary>The one scope: everything the MCP server offers, which is all an agent session reaches.</summary>
    internal const string Scope = "mcp";

    /// <summary>This Culina as an authorization server: its origin, with no trailing slash (RFC 8414, 2).</summary>
    internal static string Issuer(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return $"{context.Request.Scheme}://{context.Request.Host.ToUriComponent()}{context.Request.PathBase}";
    }

    /// <summary>This Culina's MCP server, the one resource it issues tokens for (RFC 8707).</summary>
    internal static string McpResource(HttpContext context) => $"{Issuer(context)}{AgentServerExtensions.Path}";

    internal static string Endpoint(HttpContext context, string path) => $"{Issuer(context)}{path}";
}
