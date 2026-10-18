using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Contracts.Shopping;
using Domain.Households;
using Domain.Shared;

namespace Application.Shopping;

/// <summary>
/// Takes a recipe added by itself (from its page, a cookbook, the list) back off the list: only what it
/// added; a planned meal of the same recipe, typed lines and bought items stay. Idempotent.
/// </summary>
/// <param name="HouseholdId">Whose list.</param>
/// <param name="UserId">Who is asking.</param>
/// <param name="RecipeId">The recipe.</param>
public sealed record WithdrawRecipeCommand(Guid HouseholdId, Guid UserId, Guid RecipeId);

internal sealed class WithdrawRecipeCommandHandler(
    IShoppingListRepository lists,
    IIngredientNameRepository names,
    IHouseholdRepository households,
    IUnitOfWork unitOfWork)
    : ICommandHandler<WithdrawRecipeCommand, Response>
{
    public async Task<Result<Response>> Handle(
        WithdrawRecipeCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Shopping.WithdrawRecipe");

        var member = await households
            .IsMemberAsync(command.HouseholdId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (!member)
        {
            return tracked.Record(
                Result<Response>.Failure(HouseholdErrors.NotFound(command.HouseholdId)));
        }

        var result = await ShoppingListWrites
            .ApplyAsync(
                lists,
                names,
                unitOfWork,
                command.HouseholdId,
                (list, _) =>
                {
                    list.WithdrawRecipeAlone(command.RecipeId);

                    return Task.FromResult(Result.Success());
                },
                cancellationToken)
            .ConfigureAwait(false);

        return tracked.Record(result);
    }
}
