using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Ingredients;
using Contracts.Ingredients;

namespace Api.Endpoints.Ingredients.GetMerges.V1;

/// <summary>Lists the merges a household has made.</summary>
internal sealed class GetIngredientMergesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/households/{{householdId:guid}}/ingredient-merges", async (
                Guid householdId,
                HttpContext context,
                IQueryHandler<GetIngredientMergesQuery, IngredientMergesResponse> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new GetIngredientMergesQuery(householdId, context.CurrentUser().UserId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getIngredientMergesV1")
            .WithTags(Tags.Ingredients)
            .WithSummary("List a household's ingredient merges")
            .WithDescription(
                "The names this household has merged into another. Each one is applied whenever one "
                + "of its recipes is written, so an edit or an import cannot bring the old name back.")
            .Produces<IngredientMergesResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
