using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Nutrition;

namespace Api.Endpoints.Nutrition.DeleteHouseholdFood.V1;

/// <summary>Removes a food typed from a package.</summary>
internal sealed class DeleteHouseholdFoodEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapDelete($"{ApiPaths.V1}/households/{{householdId:guid}}/foods/{{foodId:guid}}", async (
                Guid householdId,
                Guid foodId,
                HttpContext context,
                ICommandHandler<DeleteHouseholdFoodCommand> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new DeleteHouseholdFoodCommand(householdId, context.CurrentUser().UserId, foodId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.NoContent, CustomResults.Problem);
            })
            .WithName("deleteHouseholdFoodV1")
            .WithTags(Tags.Nutrition)
            .WithSummary("Remove a food from a package")
            .WithDescription(
                "Removes one of this household's foods. Every choice of it, an heir's too, goes with it, so those "
                + "ingredients count as they did before it was chosen. Only the household that added a food removes "
                + "it. Idempotent; 204.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
