using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Sessions;
using Application.Telemetry;
using Domain.AgentAuthorizations;
using Domain.Sessions;
using Domain.Shared;
using Microsoft.Extensions.Logging;

namespace Application.AgentAuthorizations.Redeem;

/// <summary>An app exchanging an authorization code for its agent's access token, at the token endpoint.</summary>
/// <param name="GrantType">Must be <c>authorization_code</c>.</param>
/// <param name="Code">The code the browser brought back.</param>
/// <param name="RedirectUri">The return address the code was sent to.</param>
/// <param name="ClientId">The app's <c>client_id</c>.</param>
/// <param name="CodeVerifier">The PKCE verifier.</param>
/// <param name="Resource">The MCP server the token is for (RFC 8707), if the app says.</param>
/// <param name="Repeated">Parameters given more than once.</param>
public sealed record RedeemAuthorizationCodeCommand(
    string? GrantType,
    string? Code,
    string? RedirectUri,
    string? ClientId,
    string? CodeVerifier,
    string? Resource,
    IReadOnlySet<string> Repeated);

/// <summary>The agent's access token: the token of a new agent session, exactly as signing in as client <c>agent</c> hands out.</summary>
/// <param name="Token">The bearer token, which exists nowhere else.</param>
public sealed record AgentAccessToken(string Token);

internal sealed class RedeemAuthorizationCodeCommandHandler(
    IAgentAuthorizationRepository authorizations,
    IAgentClientRepository clients,
    ISessionStore sessions,
    SessionStarter starter,
    ISecretTokens tokens,
    IUnitOfWork unitOfWork,
    TimeProvider time,
    ILogger<RedeemAuthorizationCodeCommandHandler> logger)
    : ICommandHandler<RedeemAuthorizationCodeCommand, AgentAccessToken>
{
    public async Task<Result<AgentAccessToken>> Handle(RedeemAuthorizationCodeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("AgentAuthorizations.Redeem");

        if (Refusal(command) is { } refused)
        {
            return tracked.Record(Result<AgentAccessToken>.Failure(refused));
        }

        var found = await authorizations.FindByCodeAsync(tokens.Digest(command.Code!), cancellationToken).ConfigureAwait(false);

        var redeemed = await found.Match(
            authorization => RedeemAsync(authorization, command, cancellationToken),
            error => Task.FromResult(Result<AgentAccessToken>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(redeemed);
    }

    private static Error? Refusal(RedeemAuthorizationCodeCommand command)
    {
        if (command.Repeated.Count > 0)
        {
            return AgentAuthorizationErrors.InvalidRequest(
                $"it gives {string.Join(", ", command.Repeated.Order(StringComparer.Ordinal))} more than once");
        }

        if (command.GrantType is not null && command.GrantType != "authorization_code")
        {
            return AgentAuthorizationErrors.UnsupportedGrantType;
        }

        return command is { GrantType: not null, Code.Length: > 0, RedirectUri: not null, ClientId: not null, CodeVerifier: not null }
            ? null
            : AgentAuthorizationErrors.InvalidRequest(
                "it must carry grant_type, code, redirect_uri, client_id and code_verifier");
    }

    private async Task<Result<AgentAccessToken>> RedeemAsync(
        AgentAuthorization authorization,
        RedeemAuthorizationCodeCommand command,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();

        // A code presented twice was stolen, or the app is broken: either way the session the first redemption
        // started is no longer trusted (OAuth 2.1, 4.1.3).
        if (authorization is { RedeemedAt: not null, SessionId: { } sessionId, UserId: { } owner })
        {
            await sessions.RevokeAsync(sessionId, owner, now, cancellationToken).ConfigureAwait(false);
            AuthenticationLogs.AuthorizationCodeReused(logger);

            return AgentAuthorizationErrors.InvalidGrant;
        }

        var allowed = authorization.Redeem(
            command.ClientId!, command.RedirectUri!, command.CodeVerifier, command.Resource, now);

        var issued = await allowed.Match(
            () => unitOfWork.InTransactionAsync(work => StartSessionAsync(authorization, now, work), cancellationToken),
            error => Task.FromResult(Result<StartedSession>.Failure(error))).ConfigureAwait(false);

        // A self-registered app that connects somebody stays registered (see IAgentClientRepository.AddAsync).
        await issued.Match(
            _ => clients.UsedAsync(authorization.Client.ClientId, now, cancellationToken),
            _ => Task.CompletedTask).ConfigureAwait(false);

        return issued.Map(started => new AgentAccessToken(started.SessionToken));
    }

    /// <summary>The same session signing in as client <c>agent</c> starts, listed with the browser that approved it.</summary>
    private async Task<Result<StartedSession>> StartSessionAsync(
        AgentAuthorization authorization,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var started = await starter.StartAsync(
            authorization.UserId!.Value,
            SessionClient.Agent,
            authorization.ApproverIpAddress,
            authorization.ApproverUserAgent,
            now,
            cancellationToken).ConfigureAwait(false);

        var claimed = await started.Match(
            session =>
            {
                authorization.Became(session.Session.Id);

                return authorizations.RedeemAsync(authorization, cancellationToken);
            },
            error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);

        return claimed.Bind(() => started);
    }
}
