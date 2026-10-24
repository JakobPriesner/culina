using Api.Extensions;
using Application.Abstractions.Messaging;
using Application.AgentAuthorizations.Redeem;
using Application.AgentAuthorizations.Register;
using Application.AgentAuthorizations.Start;
using Domain.AgentAuthorizations;
using ModelContextProtocol.Authentication;

namespace Api.Agents.Authorization;

/// <summary>
/// Culina as the OAuth 2.1 authorization server for its own MCP server, for MCP clients that connect only through
/// OAuth (Claude's and ChatGPT's connectors): discovery, the authorization endpoint, the token endpoint and dynamic
/// client registration. See docs/api.md, "AI agents (MCP)".
/// </summary>
/// <remarks>
/// The token is an agent session, exactly as signing in as client <c>agent</c> starts one, so
/// <see cref="AgentAccess"/>, the <see cref="AgentOperations"/>, revocation and the devices screen apply to it
/// unchanged. Only the authorization-code grant with PKCE <c>S256</c>, only public clients, and no refresh tokens: an
/// agent session slides while used, and when it ends the MCP server's 401 sends the app through this flow again.
/// </remarks>
internal static class AuthorizationServerExtensions
{
    /// <summary>The protected resource metadata (RFC 9728) for the MCP server, served by the SDK's handler.</summary>
    internal static ProtectedResourceMetadata ProtectedResource(HttpContext context) => new()
    {
        Resource = OAuthAddresses.McpResource(context),
        AuthorizationServers = [OAuthAddresses.Issuer(context)],
        ScopesSupported = [OAuthAddresses.Scope],
        BearerMethodsSupported = ["header"],
        ResourceName = "Culina"
    };

    internal static WebApplication MapAuthorizationServer(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(OAuthAddresses.AuthorizationServerMetadataPath, (HttpContext context) =>
                Results.Json(Metadata(context), OAuthResults.Json))
            .WithName("oauthAuthorizationServerMetadata")
            .ExcludeFromDescription()
            .AllowAnonymous();

        app.MapGet(OAuthAddresses.AuthorizationPath, async (
                HttpContext context,
                ICommandHandler<StartAgentAuthorizationCommand, AuthorizationStep> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(context.ToStartCommand(), cancellationToken).ConfigureAwait(false);

                // Back to the app only once it and its address checked out; anything earlier is said on Culina's page.
                return result.Match(
                    step => Results.Redirect(step.Back ?? $"{OAuthAddresses.ConsentPath}?request={step.AuthorizationId}"),
                    error => Results.Redirect($"{OAuthAddresses.ConsentPath}?problem={Uri.EscapeDataString(error.Code)}"));
            })
            .WithName("oauthAuthorize")
            .ExcludeFromDescription()
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitExtensions.AgentAuthorization);

        app.MapPost(OAuthAddresses.TokenPath, async (
                HttpContext context,
                ICommandHandler<RedeemAuthorizationCodeCommand, AgentAccessToken> handler,
                CancellationToken cancellationToken) =>
            {
                // Form-encoded, as RFC 6749 (4.1.3) has it; anything else carries no parameters at all.
                if (!context.Request.HasFormContentType)
                {
                    return OAuthResults.Error(context, AgentAuthorizationErrors.InvalidRequest(
                        "a token request is application/x-www-form-urlencoded"));
                }

                var form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
                var result = await handler.Handle(form.ToRedeemCommand(), cancellationToken).ConfigureAwait(false);

                return result.Match(
                    token => OAuthResults.Ok(context, new TokenResponse(token.Token, "Bearer", OAuthAddresses.Scope)),
                    error => OAuthResults.Error(context, error));
            })
            .WithName("oauthToken")
            .ExcludeFromDescription()
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitExtensions.AgentAuthorization);

        app.MapPost(OAuthAddresses.RegistrationPath, async (
                ClientRegistrationRequest request,
                HttpContext context,
                ICommandHandler<RegisterAgentClientCommand, RegisteredAgentClient> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(request.ToCommand(), cancellationToken).ConfigureAwait(false);

                return result.Match(
                    registered => OAuthResults.Ok(context, Registered(registered), StatusCodes.Status201Created),
                    error => OAuthResults.Error(context, error));
            })
            .WithName("oauthRegister")
            .ExcludeFromDescription()
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitExtensions.AgentRegistration);

        return app;
    }

    /// <summary>The authorization server metadata (RFC 8414), with what Claude and ChatGPT look for before choosing how to register.</summary>
    private static AuthorizationServerMetadata Metadata(HttpContext context) => new(
        Issuer: OAuthAddresses.Issuer(context),
        AuthorizationEndpoint: OAuthAddresses.Endpoint(context, OAuthAddresses.AuthorizationPath),
        TokenEndpoint: OAuthAddresses.Endpoint(context, OAuthAddresses.TokenPath),
        RegistrationEndpoint: OAuthAddresses.Endpoint(context, OAuthAddresses.RegistrationPath),
        ScopesSupported: [OAuthAddresses.Scope],
        ResponseTypesSupported: ["code"],
        GrantTypesSupported: ["authorization_code"],
        CodeChallengeMethodsSupported: [CodeChallenge.Method],
        TokenEndpointAuthMethodsSupported: ["none"],
        ClientIdMetadataDocumentSupported: true,
        AuthorizationResponseIssParameterSupported: true);

    private static ClientRegistrationResponse Registered(RegisteredAgentClient registered) => new(
        ClientId: registered.Registration.Client.ClientId,
        ClientIdIssuedAt: registered.IssuedAt.ToUnixTimeSeconds(),
        ClientName: registered.Registration.Client.Name,
        RedirectUris: registered.Registration.RedirectUris,
        GrantTypes: ["authorization_code"],
        ResponseTypes: ["code"],
        TokenEndpointAuthMethod: "none");

    private sealed record AuthorizationServerMetadata(
        string Issuer,
        string AuthorizationEndpoint,
        string TokenEndpoint,
        string RegistrationEndpoint,
        IReadOnlyList<string> ScopesSupported,
        IReadOnlyList<string> ResponseTypesSupported,
        IReadOnlyList<string> GrantTypesSupported,
        IReadOnlyList<string> CodeChallengeMethodsSupported,
        IReadOnlyList<string> TokenEndpointAuthMethodsSupported,
        bool ClientIdMetadataDocumentSupported,
        bool AuthorizationResponseIssParameterSupported);

    private sealed record TokenResponse(string AccessToken, string TokenType, string Scope);

    private sealed record ClientRegistrationResponse(
        string ClientId,
        long ClientIdIssuedAt,
        string ClientName,
        IReadOnlyList<string> RedirectUris,
        IReadOnlyList<string> GrantTypes,
        IReadOnlyList<string> ResponseTypes,
        string TokenEndpointAuthMethod);
}
