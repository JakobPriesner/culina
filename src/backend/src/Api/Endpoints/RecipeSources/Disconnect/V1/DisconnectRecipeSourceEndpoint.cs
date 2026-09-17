using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.Sources;
using Domain.Import;

namespace Api.Endpoints.RecipeSources.Disconnect.V1;

/// <summary>Forgets a connection, and nothing else.</summary>
internal sealed class DisconnectRecipeSourceEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapDelete($"{ApiPaths.V1}/recipe-sources/{{sourceId:guid}}", async (
                Guid sourceId,
                HttpContext context,
                ICommandHandler<DisconnectSourceCommand> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new DisconnectSourceCommand(sourceId, context.CurrentUser().UserId),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(
                    Results.NoContent,
                    // Disconnecting something already gone is the outcome the
                    // caller wanted, so a second DELETE answers 204 too.
                    error => error.Code == ImportErrors.SourceNotFound(sourceId).Code
                        ? Results.NoContent()
                        : CustomResults.Problem(error));
            })
            .WithName("disconnectRecipeSourceV1")
            .WithTags(Tags.RecipeSources)
            .WithSummary("Disconnect a recipe library")
            .WithDescription(
                "Puts the token away. Every recipe it brought over stays exactly where it is, and "
                + "still knows which app and which id it came from — the link back is only what is "
                + "lost. Idempotent: disconnecting one that is already gone also answers 204.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();
    }
}
