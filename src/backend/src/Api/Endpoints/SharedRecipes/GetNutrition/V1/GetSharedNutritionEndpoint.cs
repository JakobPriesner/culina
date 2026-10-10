using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.GetSharedNutrition;
using Contracts.Recipes.GetNutrition;

namespace Api.Endpoints.SharedRecipes.GetNutrition.V1;

/// <summary>Serves a shared recipe's nutrition to whoever follows its link.</summary>
internal sealed class GetSharedNutritionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/shared-recipes/{{token}}/nutrition", async (
                string token,
                HttpContext context,
                IQueryHandler<GetSharedNutritionQuery, SharedNutrition> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new GetSharedNutritionQuery(token), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(
                    nutrition => Served(context, nutrition),
                    CustomResults.Problem);
            })
            .WithName("getSharedRecipeNutritionV1")
            .WithTags(Tags.SharedRecipes)
            .WithSummary("Read a shared recipe's nutrition")
            .WithDescription(
                "No account needed: the token in the path is the whole of the authorisation. The same "
                + "figure as a recipe's own nutrition, but with no household corrections, since the "
                + "reader is in no household. Unrounded, with atLeast on any value that is a lower "
                + "bound. The source block must be shown wherever the numbers are (CC BY 4.0).")
            .Produces<Response>()
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitExtensions.SharedRecipe);
    }

    /// <summary>
    /// Revalidated by tag but never stored by anything in between: the address carries a credential,
    /// as on the shared recipe itself. The token names the recipe, so no recipe id goes into the tag.
    /// </summary>
    private static IResult Served(HttpContext context, SharedNutrition nutrition)
    {
        var result = ETag.Ok(context, nutrition.Body, nutrition.RecipeVersion, Guid.Empty, $"n{nutrition.DataVersion}");

        context.Response.Headers.CacheControl = "private, no-store";

        return result;
    }
}
