using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Shopping;
using Response = Contracts.Shopping.Response;

namespace Api.Endpoints.Shopping.WithdrawPlannedMeal.V1;

/// <summary>Takes one planned meal's shopping back off the list.</summary>
internal sealed class WithdrawPlannedMealEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapDelete($"{ApiPaths.V1}/households/{{householdId:guid}}/shopping-list/meals/{{entryId:guid}}", async (
                Guid householdId,
                Guid entryId,
                HttpContext context,
                ICommandHandler<WithdrawPlannedMealCommand, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new WithdrawPlannedMealCommand(householdId, context.CurrentUser().UserId, entryId),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("withdrawPlannedMealFromShoppingListV1")
            .WithTags(Tags.Shopping)
            .WithSummary("Remove a planned meal's ingredients")
            .WithDescription(
                "Subtracts exactly what that meal contributed, whether or not it is still planned. "
                + "Another meal's share of a line stays, typed lines stay, and ticked lines stay "
                + "because they have been bought. Repeating it changes nothing.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
