using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.RemoveImage;

namespace Api.Endpoints.Recipes.RemoveImage.V1;

/// <summary>Removes a recipe's image.</summary>
internal sealed class RemoveRecipeImageEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapDelete($"{ApiPaths.V1}/recipes/{{recipeId:guid}}/image", async (
                Guid recipeId,
                HttpContext context,
                ICommandHandler<RemoveRecipeImageCommand> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new RemoveRecipeImageCommand(recipeId, context.CurrentUser().UserId),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.NoContent, CustomResults.Problem);
            })
            .WithName("removeRecipeImageV1")
            .WithTags(Tags.Recipes)
            .WithSummary("Remove a recipe image")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
