using Domain.AgentAuthorizations;
using Domain.Shared;

namespace Application.AgentAuthorizations;

/// <summary>Where the browser goes back to: the app's return address with the code, or the error, its state and Culina's issuer (RFC 9207).</summary>
internal static class AuthorizationResponse
{
    internal static string Code(string redirectUri, string code, string? state, string issuer) =>
        With(redirectUri, [("code", code), ("state", state), ("iss", issuer)]);

    internal static string Error(string redirectUri, Error error, string? state, string issuer) =>
        With(redirectUri, [
            ("error", AgentAuthorizationErrors.OAuthCode(error)),
            ("error_description", error.Description),
            ("state", state),
            ("iss", issuer)
        ]);

    private static string With(string redirectUri, IEnumerable<(string Name, string? Value)> parameters)
    {
        var query = string.Join('&', parameters
            .Where(parameter => parameter.Value is not null)
            .Select(parameter => $"{parameter.Name}={Uri.EscapeDataString(parameter.Value!)}"));

        // A registered address may carry a query of its own, which is kept (RFC 6749, 3.1.2).
        return $"{redirectUri}{(redirectUri.Contains('?', StringComparison.Ordinal) ? '&' : '?')}{query}";
    }
}
