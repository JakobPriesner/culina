using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.AgentAuthorizations;
using Domain.Shared;

namespace Application.AgentAuthorizations.Register;

/// <summary>An app registering itself to connect AI agents (OAuth dynamic client registration, RFC 7591).</summary>
/// <remarks>
/// Deprecated by the MCP authorization spec in favour of Client ID Metadata Documents, and kept because apps still use
/// it. Every app registered here is a public client: it proves itself with PKCE, never a secret, whatever
/// <c>token_endpoint_auth_method</c> it asked for (RFC 7591, 3.2.1 lets the server choose).
/// </remarks>
/// <param name="RedirectUris">Where the app may be sent back to: https, or http on loopback.</param>
/// <param name="ClientName">What the app calls itself.</param>
/// <param name="GrantTypes">The grants it means to use; must allow <c>authorization_code</c> when given.</param>
/// <param name="ResponseTypes">The responses it expects; must allow <c>code</c> when given.</param>
public sealed record RegisterAgentClientCommand(
    IReadOnlyList<string>? RedirectUris,
    string? ClientName,
    IReadOnlyList<string>? GrantTypes,
    IReadOnlyList<string>? ResponseTypes);

/// <summary>A registered app and when its id was issued.</summary>
public sealed record RegisteredAgentClient(ClientRegistration Registration, DateTimeOffset IssuedAt);

internal sealed class RegisterAgentClientCommandHandler(
    IAgentClientRepository clients,
    ISecretTokens tokens,
    TimeProvider time)
    : ICommandHandler<RegisterAgentClientCommand, RegisteredAgentClient>
{
    private const int MaxRedirectUris = 10;

    private const int MaxRedirectUriLength = 2048;

    public async Task<Result<RegisteredAgentClient>> Handle(
        RegisterAgentClientCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("AgentAuthorizations.Register");

        if (Refusal(command) is { } refused)
        {
            return tracked.Record(Result<RegisteredAgentClient>.Failure(refused));
        }

        var redirectUris = command.RedirectUris!;
        var now = time.GetUtcNow();
        var name = AgentClient.CleanName(command.ClientName, RedirectAddress.HostOf(redirectUris[0]));

        // Not a secret: anybody may read it in the consent screen's address. The token is merely unguessable and unique.
        var registration = new ClientRegistration(new AgentClient(tokens.NewToken(), name, VerifiedHost: null), redirectUris);

        await clients.AddAsync(registration, now, cancellationToken).ConfigureAwait(false);

        return tracked.Record(Result<RegisteredAgentClient>.Success(new RegisteredAgentClient(registration, now)));
    }

    private static Error? Refusal(RegisterAgentClientCommand command)
    {
        if (command.RedirectUris is not { Count: > 0 and <= MaxRedirectUris } redirectUris
            || redirectUris.Any(uri => uri.Length > MaxRedirectUriLength || !RedirectAddress.IsAcceptable(uri)))
        {
            return AgentAuthorizationErrors.InvalidRedirectUri;
        }

        if (command.GrantTypes is { } grants && !grants.Contains("authorization_code", StringComparer.Ordinal))
        {
            return AgentAuthorizationErrors.InvalidClientMetadata("its grant_types must include authorization_code");
        }

        if (command.ResponseTypes is { } responses && !responses.Contains("code", StringComparer.Ordinal))
        {
            return AgentAuthorizationErrors.InvalidClientMetadata("its response_types must include code");
        }

        return null;
    }
}
