using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Ingredients;
using Contracts.Ingredients;

namespace Api.Endpoints.Ingredients.GetNames.V1;

/// <summary>Lists the ingredient names a household's own recipes use.</summary>
internal sealed class GetIngredientNamesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/households/{{householdId:guid}}/ingredient-names", async (
                Guid householdId,
                HttpContext context,
                IQueryHandler<GetIngredientNamesQuery, IngredientNamesResponse> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new GetIngredientNamesQuery(householdId, context.CurrentUser().UserId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getIngredientNamesV1")
            .WithTags(Tags.Ingredients)
            .WithSummary("List a household's ingredient names")
            .WithDescription(
                "Every distinct ingredient name in this household's own recipes, exactly as written, "
                + "with how many recipes use it; most used first. Recipes it inherits are left out: "
                + "a merge only rewrites the household's own.")
            .Produces<IngredientNamesResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
