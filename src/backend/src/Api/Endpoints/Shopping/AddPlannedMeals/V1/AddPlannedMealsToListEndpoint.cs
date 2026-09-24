using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Shopping;
using Request = Contracts.Shopping.AddPlannedMealsRequest;
using Response = Contracts.Shopping.Response;

namespace Api.Endpoints.Shopping.AddPlannedMeals.V1;

/// <summary>Puts a planned week's shopping on the list, each meal once.</summary>
internal sealed class AddPlannedMealsToListEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/households/{{householdId:guid}}/shopping-list/meals", async (
                Guid householdId,
                Request request,
                HttpContext context,
                ICommandHandler<AddPlannedMealsToListCommand, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new AddPlannedMealsToListCommand(
                            householdId,
                            context.CurrentUser().UserId,
                            request.From),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("addPlannedMealsToShoppingListV1")
            .WithTags(Tags.Shopping)
            .WithSummary("Add a planned week's ingredients")
            .WithDescription(
                "Every meal planned in the seven days from `from`, at its planned servings, merged "
                + "exactly as a single recipe is. Safe to repeat: a meal already on the list is "
                + "skipped, and a recipe that was added by itself counts as the shopping for a "
                + "planned meal of it. Each planned meal's `isOnShoppingList` says which are there.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
