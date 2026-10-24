using Domain.AgentAuthorizations;

namespace Application.AgentAuthorizations;

/// <summary>Maps requests to connect an AI agent onto their wire contracts.</summary>
internal static class AgentAuthorizationMappings
{
    internal static Contracts.AgentAuthorizations.GetById.Response ToGetByIdResponse(this AgentAuthorization authorization) =>
        new()
        {
            AuthorizationId = authorization.Id,
            ClientId = authorization.Client.ClientId,
            ClientName = authorization.Client.Name,
            VerifiedHost = authorization.Client.VerifiedHost,
            RedirectHost = RedirectAddress.HostOf(authorization.RedirectUri),
            RedirectsToThisComputer = RedirectAddress.IsLoopback(authorization.RedirectUri),
            ExpiresAt = authorization.ExpiresAt
        };
}
