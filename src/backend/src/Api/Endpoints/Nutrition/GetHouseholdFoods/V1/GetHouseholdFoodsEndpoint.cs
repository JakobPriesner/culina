using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Nutrition;
using Contracts.Nutrition;

namespace Api.Endpoints.Nutrition.GetHouseholdFoods.V1;

/// <summary>Lists the foods a household typed from a package.</summary>
internal sealed class GetHouseholdFoodsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/households/{{householdId:guid}}/foods", async (
                Guid householdId,
                HttpContext context,
                IQueryHandler<GetHouseholdFoodsQuery, HouseholdFoodsResponse> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new GetHouseholdFoodsQuery(householdId, context.CurrentUser().UserId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getHouseholdFoodsV1")
            .WithTags(Tags.Nutrition)
            .WithSummary("List a household's foods from a package")
            .WithDescription(
                "Foods this household, and the households it inherits from, typed from a package's label, "
                + "per 100 g, for what the Bundeslebensmittelschlüssel does not have. Each one's `code` is chosen "
                + "for an ingredient like a BLS code, with `PUT /households/{householdId}/ingredients/{name}`.")
            .Produces<HouseholdFoodsResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
