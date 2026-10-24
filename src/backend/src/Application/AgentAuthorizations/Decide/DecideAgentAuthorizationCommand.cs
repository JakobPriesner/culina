using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.AgentAuthorizations;
using Domain.Shared;
using Response = Contracts.AgentAuthorizations.Decide.Response;

namespace Application.AgentAuthorizations.Decide;

/// <summary>The signed-in person answering an app that asks to connect an AI agent to their account.</summary>
/// <param name="AuthorizationId">Which request.</param>
/// <param name="Approved">Whether they let it in.</param>
/// <param name="Issuer">This Culina as an authorization server, for <c>iss</c>.</param>
/// <param name="IpAddress">The browser's address, which the agent's session is listed with.</param>
/// <param name="UserAgent">The browser, which the agent's session is listed with.</param>
public sealed record DecideAgentAuthorizationCommand(
    Guid AuthorizationId,
    bool Approved,
    string Issuer,
    string? IpAddress,
    string? UserAgent);

internal sealed class DecideAgentAuthorizationCommandHandler(
    IAgentAuthorizationRepository authorizations,
    IUserContext user,
    ISecretTokens tokens,
    TimeProvider time)
    : ICommandHandler<DecideAgentAuthorizationCommand, Response>
{
    public async Task<Result<Response>> Handle(DecideAgentAuthorizationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("AgentAuthorizations.Decide");

        var found = await authorizations.FindAsync(command.AuthorizationId, cancellationToken).ConfigureAwait(false);

        var decided = await found.Match(
            authorization => DecideAsync(authorization, command, cancellationToken),
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        tracked.Tag("culina.approved", command.Approved);

        return tracked.Record(decided);
    }

    private async Task<Result<Response>> DecideAsync(
        AgentAuthorization authorization,
        DecideAgentAuthorizationCommand command,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();

        // The code exists in the clear only in the address the browser is sent to.
        var code = command.Approved ? tokens.NewToken() : null;

        var answered = code is null
            ? authorization.Deny(user.UserId, now)
            : authorization.Approve(user.UserId, tokens.Digest(code), command.IpAddress, command.UserAgent, now);

        var saved = await answered.Match(
            () => authorizations.DecideAsync(authorization, cancellationToken),
            error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);

        return saved.Map(() => new Response
        {
            Approved = code is not null,
            RedirectUri = code is null
                ? AuthorizationResponse.Error(
                    authorization.RedirectUri, AgentAuthorizationErrors.AccessDenied, authorization.State, command.Issuer)
                : AuthorizationResponse.Code(authorization.RedirectUri, code, authorization.State, command.Issuer)
        });
    }
}
