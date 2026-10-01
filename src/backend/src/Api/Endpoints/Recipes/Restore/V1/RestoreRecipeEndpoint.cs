using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.Restore;

namespace Api.Endpoints.Recipes.Restore.V1;

/// <summary>Takes a recipe out of its household's bin.</summary>
internal sealed class RestoreRecipeEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/recipes/{{recipeId:guid}}/restorations", async (
                Guid recipeId,
                HttpContext context,
                ICommandHandler<RestoreRecipeCommand> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new RestoreRecipeCommand(recipeId, context.CurrentUser().UserId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.NoContent, CustomResults.Problem);
            })
            .WithName("restoreRecipeV1")
            .WithTags(Tags.Recipes)
            .WithSummary("Restore a deleted recipe")
            .WithDescription(
                "Any member of the recipe's household may, until it is purged. 404 when it is not in a "
                + "bin the caller can open — never deleted, already restored, purged, or in a household "
                + "that is itself deleted.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
