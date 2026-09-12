using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Shopping;
using Request = Contracts.Shopping.AddRecipeRequest;
using Response = Contracts.Shopping.Response;

namespace Api.Endpoints.Shopping.AddRecipe.V1;

/// <summary>Puts a recipe's ingredients on the list.</summary>
internal sealed class AddRecipeToListEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/households/{{householdId:guid}}/shopping-list/recipes", async (
                Guid householdId,
                Request request,
                HttpContext context,
                ICommandHandler<AddRecipeToListCommand, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new AddRecipeToListCommand(
                            householdId,
                            request.RecipeId,
                            context.CurrentUser().UserId,
                            request.Servings),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("addRecipeToShoppingListV1")
            .WithTags(Tags.Shopping)
            .WithSummary("Add a recipe's ingredients")
            .WithDescription(
                "At the scaling you are cooking, so the amounts are the real ones. Lines merge "
                + "when the names match — folded, so Müsli meets Muesli — and the units can be "
                + "added at all. Spoons never convert to millilitres, so those stay two lines.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
