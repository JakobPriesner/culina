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
                var expected = ETag.RequireIfMatch(context);

                return await expected.Match(
                    async version =>
                    {
                        var result = await handler
                            .Handle(
                                new DeleteRecipeCommand(recipeId, context.CurrentUser().UserId, version),
                                cancellationToken)
                            .ConfigureAwait(false);

                        return result.Match(
                            Results.NoContent,
                            // Deleting something already gone is the outcome the caller
                            // wanted, so a second DELETE answers 204 rather than 404.
                            error => error.Code == Domain.Recipes.RecipeErrors.NotFound(recipeId).Code
                                ? Results.NoContent()
                                : CustomResults.Problem(error));
                    },
                    error => Task.FromResult(CustomResults.Problem(error))).ConfigureAwait(false);
            })
            .WithName("deleteRecipeV1")
            .WithTags(Tags.Recipes)
            .WithSummary("Delete a recipe")
            .WithDescription(
                "Idempotent: deleting a recipe that is already gone also answers 204. `If-Match` is "
                + "required — missing is 428, stale is 412 — so a recipe somebody else just changed "
                + "is not deleted on the strength of the version you saw before.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .RequireAuthorization();
    }
}
