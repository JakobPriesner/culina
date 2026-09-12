using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.CookSessions;

namespace Api.Endpoints.CookSessions.End.V1;

/// <summary>Finishes a session, or gives up on it.</summary>
internal sealed class EndCookSessionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapDelete($"{ApiPaths.V1}/cook-sessions/{{sessionId:guid}}", async (
                Guid sessionId,
                HttpContext context,
                ICommandHandler<EndCookSessionCommand> handler,
                CancellationToken cancellationToken) =>
            {
                // Finishing and giving up are both "this session is over", but
                // only one of them means the recipe worked, and the cook log
                // cares about the difference.
                var completed = context.Request.Query["completed"] == "true";

                var result = await handler
                    .Handle(
                        new EndCookSessionCommand(sessionId, context.CurrentUser().UserId, completed),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.NoContent, CustomResults.Problem);
            })
            .WithName("endCookSessionV1")
            .WithTags(Tags.CookSessions)
            .WithSummary("Finish or abandon")
            .WithDescription("Pass `completed=true` when the cooking was finished.")
            .WithRepeatableQueryParameters(["completed"], [])
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
