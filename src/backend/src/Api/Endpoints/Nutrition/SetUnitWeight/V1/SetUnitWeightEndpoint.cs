using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Nutrition;
using Request = Contracts.Nutrition.SetUnitWeightRequest;

namespace Api.Endpoints.Nutrition.SetUnitWeight.V1;

/// <summary>Says what one unit of an ingredient weighs in a household.</summary>
internal sealed class SetUnitWeightEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut($"{ApiPaths.V1}/households/{{householdId:guid}}/ingredients/{{name}}/units/{{unit}}", async (
                Guid householdId,
                string name,
                string unit,
                Request request,
                HttpContext context,
                ICommandHandler<SetUnitWeightCommand> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new SetUnitWeightCommand(
                            householdId,
                            context.CurrentUser().UserId,
                            PathSegments.DecodedFromEnd(context, name, 2),
                            PathSegments.LastDecoded(context, unit),
                            request.Grams),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.NoContent, CustomResults.Problem);
            })
            .WithName("setHouseholdIngredientUnitWeightV1")
            .WithTags(Tags.Nutrition)
            .WithSummary("Say what one unit of an ingredient weighs")
            .WithDescription(
                "States what one `{unit}` of the ingredient `{name}` weighs in this household: "
                + "`{ \"grams\": 150 }` for \"bei uns wiegt 1 Zwiebel 150 g\". It counts every line of "
                + "this household with that name in that unit, ahead of a typical weight, a density "
                + "and the egg size, and never in another household, including the one this household "
                + "inherits from. `{name}` is encoded as for the food correction, a slash is `%2F`. "
                + "`{unit}` is a spelling of a known unit (`piece`, `Stück`, `Zehe`, `EL` ...) or the "
                + "household's own word; all spellings of one unit share one weight (see `unitKey` of "
                + "a nutrition line). Grams (g, kg) and volumes (ml, l, cup, fl oz) already have a size "
                + "and are refused (`nutrition.unit_has_a_size`); grams must be more than 0 and at most "
                + "10000 (`nutrition.invalid_grams`). Any member may. Idempotent; 204.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
