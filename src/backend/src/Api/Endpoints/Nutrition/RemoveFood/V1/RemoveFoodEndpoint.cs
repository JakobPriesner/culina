using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Nutrition;

namespace Api.Endpoints.Nutrition.RemoveFood.V1;

/// <summary>Goes back to the default food for an ingredient name.</summary>
internal sealed class RemoveFoodEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapDelete($"{ApiPaths.V1}/households/{{householdId:guid}}/ingredients/{{name}}", async (
                Guid householdId,
                string name,
                HttpContext context,
                ICommandHandler<RemoveFoodCorrectionCommand> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new RemoveFoodCorrectionCommand(
                            householdId,
                            context.CurrentUser().UserId,
                            PathSegments.LastDecoded(context, name)),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.NoContent, CustomResults.Problem);
            })
            .WithName("removeHouseholdIngredientFoodV1")
            .WithTags(Tags.Nutrition)
            .WithSummary("Go back to the default food for an ingredient")
            .WithDescription(
                "Forgets this household's choice for the name, so the table's default applies "
                + "again. 204 also when there was no choice. `{name}` is encoded as for PUT.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
