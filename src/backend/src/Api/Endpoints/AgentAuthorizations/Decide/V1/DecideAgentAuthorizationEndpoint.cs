using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.AgentAuthorizations.Decide;
using Request = Contracts.AgentAuthorizations.Decide.Request;
using Response = Contracts.AgentAuthorizations.Decide.Response;

namespace Api.Endpoints.AgentAuthorizations.Decide.V1;

/// <summary>Answers an app's request to connect an AI agent to the caller's account.</summary>
/// <remarks>
/// An unsafe request like any other, so a browser's must come from this origin and carry the CSRF token: a page
/// elsewhere cannot approve on someone's behalf.
/// </remarks>
internal sealed class DecideAgentAuthorizationEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut($"{ApiPaths.V1}/agent-authorizations/{{authorizationId:guid}}/decision", async (
                Guid authorizationId,
                Request request,
                HttpContext context,
                ICommandHandler<DecideAgentAuthorizationCommand, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(request.ToCommand(authorizationId, context), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("decideAgentAuthorizationV1")
            .WithTags(Tags.AgentAuthorizations)
            .WithSummary("Answer a request to connect an AI agent")
            .WithDescription(
                "Approves or denies the request. Answers with the app's return address, carrying the authorization code "
                + "or error=access_denied, which the browser navigates to. The code becomes an agent session of the "
                + "caller's, which reaches only the MCP server and is listed and revoked with the other sessions.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
