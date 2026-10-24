using Domain.Shared;

namespace Domain.AgentAuthorizations;

/// <summary>Why an app could not connect an AI agent through OAuth.</summary>
/// <remarks>
/// The part after <c>oauth.</c> is the OAuth error code itself (RFC 6749, 7591, 8707), which the protocol endpoints
/// send as <c>error</c>; the description becomes <c>error_description</c>.
/// </remarks>
public static class AgentAuthorizationErrors
{
    /// <summary>The prefix the OAuth error codes share.</summary>
    public const string Prefix = "oauth.";

    /// <summary>The OAuth error code an error stands for: <c>oauth.invalid_target</c> is <c>invalid_target</c>.</summary>
    public static string OAuthCode(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return error.Code.StartsWith(Prefix, StringComparison.Ordinal) ? error.Code[Prefix.Length..] : "invalid_request";
    }

    /// <summary>A parameter is missing, repeated or malformed; <paramref name="problem"/> says which, as a clause.</summary>
    public static Error InvalidRequest(string problem) =>
        new("oauth.invalid_request", $"The request is not valid: {problem}.", ErrorType.Validation);

    /// <summary>The app is not one Culina knows, or its metadata document could not be read.</summary>
    public static Error InvalidClient(string problem) =>
        new("oauth.invalid_client", $"Culina cannot tell which app this is: {problem}.", ErrorType.Validation);

    /// <summary>The return address is not https or loopback, or not one the app registered.</summary>
    public static readonly Error InvalidRedirectUri = new(
        "oauth.invalid_redirect_uri",
        "The app's return address is not one it registered, or is neither https nor on this computer.",
        ErrorType.Validation);

    /// <summary>A registration whose metadata Culina cannot accept.</summary>
    public static Error InvalidClientMetadata(string problem) =>
        new("oauth.invalid_client_metadata", $"Culina cannot register this app: {problem}.", ErrorType.Validation);

    /// <summary>Anything but the authorization-code flow was asked for.</summary>
    public static readonly Error UnsupportedResponseType = new(
        "oauth.unsupported_response_type",
        "Culina only answers response_type=code.",
        ErrorType.Validation);

    /// <summary>The token was asked for somewhere other than this Culina's MCP server.</summary>
    public static readonly Error InvalidTarget = new(
        "oauth.invalid_target",
        "Culina issues tokens only for its own MCP server; resource must be its address.",
        ErrorType.Validation);

    /// <summary>The code is unknown, used, expired, or not this app's; or the PKCE verifier does not match.</summary>
    /// <remarks>One error for all of them, so a guesser learns nothing about which part was wrong.</remarks>
    public static readonly Error InvalidGrant = new(
        "oauth.invalid_grant",
        "The authorization code is not valid for this request. Connect the app again.",
        ErrorType.Validation);

    /// <summary>A grant other than the authorization code.</summary>
    public static readonly Error UnsupportedGrantType = new(
        "oauth.unsupported_grant_type",
        "Culina only accepts grant_type=authorization_code.",
        ErrorType.Validation);

    /// <summary>The person said no.</summary>
    public static readonly Error AccessDenied = new(
        "oauth.access_denied",
        "The person declined to connect this app.",
        ErrorType.Forbidden);

    /// <summary>The request expired, was answered already, or never existed.</summary>
    public static readonly Error NotFound = new(
        "agent_authorizations.not_found",
        "This connection request has expired or was already answered. Start connecting from the app again.",
        ErrorType.NotFound);
}
