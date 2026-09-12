using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.CookSessions;
using Request = Contracts.CookSessions.UpdateRequest;
using Response = Contracts.CookSessions.Response;

namespace Api.Endpoints.CookSessions.Update.V1;

/// <summary>Moves a cook along.</summary>
internal sealed class UpdateCookSessionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPatch($"{ApiPaths.V1}/cook-sessions/{{sessionId:guid}}", async (
                Guid sessionId,
                Request request,
                HttpContext context,
                ICommandHandler<UpdateCookSessionCommand, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new UpdateCookSessionCommand(
                            sessionId,
                            context.CurrentUser().UserId,
                            request.CurrentStepIndex,
                            request.Servings),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("updateCookSessionV1")
            .WithTags(Tags.CookSessions)
            .WithSummary("Move to a step, or rescale")
            .WithDescription(
                "Called on every step advance, so it is deliberately cheap: a step touches two "
                + "columns and does not bump the version, because the last tap genuinely is the "
                + "truth about where the cook is. Rescaling does bump it.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization();
    }
}
