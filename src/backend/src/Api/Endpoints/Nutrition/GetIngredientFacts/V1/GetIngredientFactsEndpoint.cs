using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Nutrition;
using Contracts.Nutrition;

namespace Api.Endpoints.Nutrition.GetIngredientFacts.V1;

/// <summary>Lists what a household has said about its ingredients, for nutrition.</summary>
internal sealed class GetIngredientFactsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/households/{{householdId:guid}}/nutrition/ingredients", async (
                Guid householdId,
                HttpContext context,
                IQueryHandler<GetIngredientFactsQuery, IngredientFactsResponse> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new GetIngredientFactsQuery(householdId, context.CurrentUser().UserId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getHouseholdNutritionIngredientsV1")
            .WithTags(Tags.Nutrition)
            .WithSummary("List what a household said about its ingredients")
            .WithDescription(
                "One entry per ingredient name that has a food the household chose (or chose not to "
                + "count) or a weight it set, by name. `name` is the folded form every spelling shares. "
                + "`corrected` is true for a food choice; `food` is then the chosen food, absent for "
                + "\"do not count\". `weights` lists the household's weight per unit. Not paged: a "
                + "household has tens of these. Not the suggestions at `GET .../ingredients`, which "
                + "are what to type, not what was said.")
            .Produces<IngredientFactsResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
