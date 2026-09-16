using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.GetAll;
using Response = Contracts.Recipes.GetAll.Response;

namespace Api.Endpoints.Recipes.GetAll.V1;

/// <summary>Finds recipes.</summary>
internal sealed class GetRecipesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/recipes", async (
                HttpContext context,
                IQueryHandler<GetRecipesQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var search = context.Request.Query.ToRecipeSearch(context.CurrentUser().UserId);

                return await search.Match(
                    async criteria =>
                    {
                        var result = await handler
                            .Handle(new GetRecipesQuery(criteria), cancellationToken)
                            .ConfigureAwait(false);

                        return result.Match(Results.Ok, CustomResults.Problem);
                    },
                    error => Task.FromResult(CustomResults.Problem(error))).ConfigureAwait(false);
            })
            .WithName("getRecipesV1")
            .WithTags(Tags.Recipes)
            .WithSummary("Find recipes")
            .WithDescription(
                "Cursor-paginated. Repeat `ingredient` to ask what you can cook from what you have: "
                + "results rank by how many of them a recipe uses and how few extras it needs, and "
                + "each carries `ingredientMatch`. There is no pantry to maintain, so nothing can "
                + "go stale.\n\n"
                + "`cookbookId` reads inside one cookbook. Every other filter still applies, so a "
                + "cookbook is a view of the collection rather than a second one; it defaults to "
                + "`cookbookOrder`, the order the cookbook was built in. An unknown cookbook is an "
                + "empty page rather than a 404 — it is a filter value, not a resource named in "
                + "the path.")
            .WithRepeatableQueryParameters(
                ["householdId", "query", "maxMinutes", "cookbookId", "sort", "cursor", "limit"],
                ["tag", "ingredient"],
                ["maxMinutes", "limit"])
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
