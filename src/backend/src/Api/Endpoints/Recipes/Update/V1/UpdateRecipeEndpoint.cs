using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.Update;
using Contracts.Recipes;
using Domain.Shared;
using Request = Contracts.Recipes.Update.Request;

namespace Api.Endpoints.Recipes.Update.V1;

/// <summary>Replaces a recipe.</summary>
internal sealed class UpdateRecipeEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut($"{ApiPaths.V1}/recipes/{{recipeId:guid}}", async (
                Guid recipeId,
                Request request,
                HttpContext context,
                ICommandHandler<UpdateRecipeCommand, RecipeDetail> handler,
                CancellationToken cancellationToken) =>
            {
                var expected = ETag.RequireIfMatch(context);

                var result = await expected.Match(
                    version => handler.Handle(
                        request.ToCommand(recipeId, context.CurrentUser().UserId, version),
                        cancellationToken),
                    error => Task.FromResult(Result<RecipeDetail>.Failure(error))).ConfigureAwait(false);

                return result.Match(
                    recipe => ETag.Ok(context, recipe, recipe.Version),
                    CustomResults.Problem);
            })
            .WithName("updateRecipeV1")
            .WithTags(Tags.Recipes)
            .WithSummary("Replace a recipe")
            .WithDescription(
                "A full replacement, because the editor holds the whole recipe on screen. Requires "
                + "If-Match: missing is 428, stale is 412.")
            .Produces<RecipeDetail>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .RequireAuthorization();
    }
}
