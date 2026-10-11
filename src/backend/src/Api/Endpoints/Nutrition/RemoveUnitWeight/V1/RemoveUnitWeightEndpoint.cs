using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Nutrition;

namespace Api.Endpoints.Nutrition.RemoveUnitWeight.V1;

/// <summary>Forgets what a household said one unit of an ingredient weighs.</summary>
internal sealed class RemoveUnitWeightEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapDelete($"{ApiPaths.V1}/households/{{householdId:guid}}/ingredients/{{name}}/units/{{unit}}", async (
                Guid householdId,
                string name,
                string unit,
                HttpContext context,
                ICommandHandler<RemoveUnitWeightCommand> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new RemoveUnitWeightCommand(
                            householdId,
                            context.CurrentUser().UserId,
                            PathSegments.DecodedFromEnd(context, name, 2),
                            PathSegments.LastDecoded(context, unit)),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.NoContent, CustomResults.Problem);
            })
            .WithName("removeHouseholdIngredientUnitWeightV1")
            .WithTags(Tags.Nutrition)
            .WithSummary("Forget what one unit of an ingredient weighs")
            .WithDescription(
                "Forgets this household's weight for the ingredient in that unit, so a typical weight "
                + "(or none) applies again. 204 also when there was none. `{name}` and `{unit}` are "
                + "encoded as for PUT.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
