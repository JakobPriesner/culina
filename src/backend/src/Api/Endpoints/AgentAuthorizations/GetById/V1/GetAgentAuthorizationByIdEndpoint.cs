using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.AgentAuthorizations.GetById;
using Response = Contracts.AgentAuthorizations.GetById.Response;

namespace Api.Endpoints.AgentAuthorizations.GetById.V1;

/// <summary>Reads an app's pending request to connect an AI agent, for the consent screen.</summary>
internal sealed class GetAgentAuthorizationByIdEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/agent-authorizations/{{authorizationId:guid}}", async (
                Guid authorizationId,
                IQueryHandler<GetAgentAuthorizationByIdQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new GetAgentAuthorizationByIdQuery(authorizationId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getAgentAuthorizationByIdV1")
            .WithTags(Tags.AgentAuthorizations)
            .WithSummary("Read a request to connect an AI agent")
            .WithDescription(
                "The app asking, through OAuth, to connect an AI agent to the caller's account, and where it sends the "
                + "browser back to. Only while it waits for an answer; afterwards, or once expired, agent_authorizations.not_found.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
