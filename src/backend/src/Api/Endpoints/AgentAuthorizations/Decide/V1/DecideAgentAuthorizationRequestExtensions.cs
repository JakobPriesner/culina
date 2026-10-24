using Api.Agents.Authorization;
using Api.Infrastructure;
using Application.AgentAuthorizations.Decide;
using Request = Contracts.AgentAuthorizations.Decide.Request;

namespace Api.Endpoints.AgentAuthorizations.Decide.V1;

/// <summary>Turns the answer and the connection into the command.</summary>
internal static class DecideAgentAuthorizationRequestExtensions
{
    internal static DecideAgentAuthorizationCommand ToCommand(this Request request, Guid authorizationId, HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        return new DecideAgentAuthorizationCommand(
            authorizationId,
            request.Approved,
            OAuthAddresses.Issuer(context),
            context.ClientAddress(),
            context.Request.Headers.UserAgent.ToString());
    }
}
