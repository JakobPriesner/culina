using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Shopping;
using Response = Contracts.Shopping.Response;

namespace Api.Endpoints.Shopping.WithdrawRecipe.V1;

/// <summary>Takes a recipe added by itself back off the list.</summary>
internal sealed class WithdrawRecipeEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapDelete($"{ApiPaths.V1}/households/{{householdId:guid}}/shopping-list/recipes/{{recipeId:guid}}", async (
                Guid householdId,
                Guid recipeId,
                HttpContext context,
                ICommandHandler<WithdrawRecipeCommand, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new WithdrawRecipeCommand(householdId, context.CurrentUser().UserId, recipeId),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("withdrawRecipeFromShoppingListV1")
            .WithTags(Tags.Shopping)
            .WithSummary("Remove a recipe's ingredients")
            .WithDescription(
                "Subtracts exactly what the recipe contributed when it was added by itself (not for a "
                + "planned meal). A planned meal of the same recipe keeps its share, typed lines stay, "
                + "and ticked lines stay because they have been bought. Repeating it changes nothing.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
