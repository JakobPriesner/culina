using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.GetById;
using Contracts.Recipes;

namespace Api.Endpoints.Recipes.GetById.V1;

/// <summary>Reads one recipe.</summary>
internal sealed class GetRecipeEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/recipes/{{recipeId:guid}}", async (
                Guid recipeId,
                HttpContext context,
                IQueryHandler<GetRecipeQuery, RecipeDetail> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new GetRecipeQuery(recipeId, context.CurrentUser().UserId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(
                    recipe => ETag.Ok(context, recipe, recipe.Version),
                    CustomResults.Problem);
            })
            .WithName("getRecipeByIdV1")
            .WithTags(Tags.Recipes)
            .WithSummary("Read a recipe")
            .WithDescription(
                "The whole recipe, with steps as segments carrying each referenced ingredient's "
                + "name and base amount — so a client renders and scales without another request.")
            .Produces<RecipeDetail>()
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
