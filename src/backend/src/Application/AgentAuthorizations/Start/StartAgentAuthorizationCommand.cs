using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.AgentAuthorizations;
using Domain.Shared;

namespace Application.AgentAuthorizations.Start;

/// <summary>An app sending someone's browser to Culina's authorization endpoint, asking to connect an AI agent.</summary>
/// <param name="ClientId">The app's <c>client_id</c>.</param>
/// <param name="RedirectUri">Where to send the browser back to.</param>
/// <param name="ResponseType">Must be <c>code</c>.</param>
/// <param name="CodeChallenge">The PKCE challenge.</param>
/// <param name="CodeChallengeMethod">Must be <c>S256</c>.</param>
/// <param name="State">The app's own value, handed back.</param>
/// <param name="Resource">The MCP server the token is for (RFC 8707); this Culina's when absent.</param>
/// <param name="Repeated">Parameters the request gave more than once, which OAuth forbids (RFC 6749, 3.1).</param>
/// <param name="Issuer">This Culina as an authorization server, for <c>iss</c>.</param>
/// <param name="McpResource">This Culina's MCP server, the only resource it issues tokens for.</param>
public sealed record StartAgentAuthorizationCommand(
    string? ClientId,
    string? RedirectUri,
    string? ResponseType,
    string? CodeChallenge,
    string? CodeChallengeMethod,
    string? State,
    string? Resource,
    IReadOnlySet<string> Repeated,
    string Issuer,
    string McpResource);

/// <summary>Where the browser goes next: the consent screen, or straight back to the app with an error.</summary>
/// <param name="AuthorizationId">The request the consent screen shows, when it may be shown.</param>
/// <param name="Back">The app's return address with the error, when the app asked for something Culina does not do.</param>
public sealed record AuthorizationStep(Guid? AuthorizationId, string? Back);

internal sealed class StartAgentAuthorizationCommandHandler(
    ClientRegistrations registrations,
    IAgentAuthorizationRepository authorizations,
    TimeProvider time)
    : ICommandHandler<StartAgentAuthorizationCommand, AuthorizationStep>
{
    private const int MaxStateLength = 2048;

    /// <summary>
    /// A failure is shown on Culina's own page, never sent anywhere: until the app and its return address check out,
    /// sending the browser to the address would make Culina an open redirector (RFC 6749, 4.1.2.1).
    /// </summary>
    public async Task<Result<AuthorizationStep>> Handle(
        StartAgentAuthorizationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("AgentAuthorizations.Start");

        if (command.ClientId is not { Length: > 0 } clientId || command.Repeated.Contains("client_id"))
        {
            return tracked.Record(Result<AuthorizationStep>.Failure(
                AgentAuthorizationErrors.InvalidRequest("it must name the app asking, once, as client_id")));
        }

        var found = await registrations.FindAsync(clientId, cancellationToken).ConfigureAwait(false);

        var step = await found.Match(
            registration => StepAsync(command, registration, cancellationToken),
            error => Task.FromResult(Result<AuthorizationStep>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(step);
    }

    private async Task<Result<AuthorizationStep>> StepAsync(
        StartAgentAuthorizationCommand command,
        ClientRegistration registration,
        CancellationToken cancellationToken)
    {
        if (command.RedirectUri is not { } redirectUri
            || command.Repeated.Contains("redirect_uri")
            || !registration.Accepts(redirectUri))
        {
            return AgentAuthorizationErrors.InvalidRedirectUri;
        }

        // From here on the app is told what was wrong, at the address it registered.
        if (Refusal(command) is { } refused)
        {
            return new AuthorizationStep(null, AuthorizationResponse.Error(redirectUri, refused, command.State, command.Issuer));
        }

        var authorization = AgentAuthorization.Start(
            registration.Client,
            redirectUri,
            command.CodeChallenge!,
            command.State,
            command.McpResource,
            time.GetUtcNow());

        await authorizations.AddAsync(authorization, cancellationToken).ConfigureAwait(false);

        return new AuthorizationStep(authorization.Id, null);
    }

    private static Error? Refusal(StartAgentAuthorizationCommand command)
    {
        if (command.Repeated.Count > 0)
        {
            return AgentAuthorizationErrors.InvalidRequest(
                $"it gives {string.Join(", ", command.Repeated.Order(StringComparer.Ordinal))} more than once");
        }

        if (command.ResponseType != "code")
        {
            return AgentAuthorizationErrors.UnsupportedResponseType;
        }

        if (command.CodeChallengeMethod != CodeChallenge.Method || !CodeChallenge.IsWellFormed(command.CodeChallenge))
        {
            return AgentAuthorizationErrors.InvalidRequest(
                "it must carry a PKCE code_challenge with code_challenge_method S256");
        }

        if (command.Resource is { } resource && !AgentAuthorization.Names(resource, command.McpResource))
        {
            return AgentAuthorizationErrors.InvalidTarget;
        }

        if (command.State is { Length: > MaxStateLength })
        {
            return AgentAuthorizationErrors.InvalidRequest("the state is too long");
        }

        return null;
    }
}
