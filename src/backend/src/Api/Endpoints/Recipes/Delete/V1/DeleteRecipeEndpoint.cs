using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.Delete;

namespace Api.Endpoints.Recipes.Delete.V1;

/// <summary>Deletes a recipe.</summary>
internal sealed class DeleteRecipeEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapDelete($"{ApiPaths.V1}/recipes/{{recipeId:guid}}", async (
                Guid recipeId,
                HttpContext context,
                ICommandHandler<DeleteRecipeCommand> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new DeleteRecipeCommand(recipeId, context.CurrentUser().UserId),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(
                    Results.NoContent,
                    // Deleting something already gone is the outcome the caller
                    // wanted, so a second DELETE answers 204 rather than 404.
                    error => error.Code == Domain.Recipes.RecipeErrors.NotFound(recipeId).Code
                        ? Results.NoContent()
                        : CustomResults.Problem(error));
            })
            .WithName("deleteRecipeV1")
            .WithTags(Tags.Recipes)
            .WithSummary("Delete a recipe")
            .WithDescription("Idempotent: deleting a recipe that is already gone also answers 204.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();
    }
}
