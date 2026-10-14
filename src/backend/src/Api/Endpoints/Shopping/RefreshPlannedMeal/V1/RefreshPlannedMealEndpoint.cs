using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Shopping;
using Response = Contracts.Shopping.Response;

namespace Api.Endpoints.Shopping.RefreshPlannedMeal.V1;

/// <summary>Brings one planned meal's shopping up to what the plan now says.</summary>
internal sealed class RefreshPlannedMealEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut($"{ApiPaths.V1}/households/{{householdId:guid}}/shopping-list/meals/{{entryId:guid}}", async (
                Guid householdId,
                Guid entryId,
                HttpContext context,
                ICommandHandler<RefreshPlannedMealOnListCommand, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new RefreshPlannedMealOnListCommand(householdId, context.CurrentUser().UserId, entryId),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("refreshPlannedMealOnShoppingListV1")
            .WithTags(Tags.Shopping)
            .WithSummary("Update a planned meal's ingredients on the list")
            .WithDescription(
                "Replaces what that meal contributed with what it asks for now: its current servings and day. "
                + "Another meal's share of a line stays, typed lines stay, and ticked lines stay because they have "
                + "been bought. A meal that is not on the list is left off it. Repeating it changes nothing.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
