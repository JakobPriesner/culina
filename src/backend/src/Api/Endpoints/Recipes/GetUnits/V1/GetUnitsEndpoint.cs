using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.GetUnits;
using Response = Contracts.Recipes.GetUnits.Response;

namespace Api.Endpoints.Recipes.GetUnits.V1;

/// <summary>Lists the units a household can measure in.</summary>
internal sealed class GetUnitsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/households/{{householdId:guid}}/units", async (
                Guid householdId,
                HttpContext context,
                IQueryHandler<GetUnitsQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new GetUnitsQuery(householdId, context.CurrentUser().UserId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getHouseholdUnitsV1")
            .WithTags(Tags.Recipes)
            .WithSummary("Read the units this kitchen measures in")
            .WithDescription(
                "The thirteen built-in units, which convert, and whatever else this household's "
                + "recipes have used. A unit is added by writing it: there is no list to maintain, "
                + "so the offered units and the recipes can never disagree.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
