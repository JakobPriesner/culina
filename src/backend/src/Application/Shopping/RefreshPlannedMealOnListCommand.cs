using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Contracts.Shopping;
using Domain.Households;
using Domain.Shared;

namespace Application.Shopping;

/// <summary>Writes a planned meal's shopping again at what the plan now says, when it is on the list at all.</summary>
/// <remarks>
/// For after the meal's servings or day changed: lines keep what other meals, typed amounts and bought items hold, and only this meal's share is replaced. A meal that is not on the list stays off it.
/// </remarks>
/// <param name="HouseholdId">Whose list.</param>
/// <param name="UserId">Who is asking.</param>
/// <param name="PlanEntryId">The planned meal.</param>
public sealed record RefreshPlannedMealOnListCommand(Guid HouseholdId, Guid UserId, Guid PlanEntryId);

internal sealed class RefreshPlannedMealOnListCommandHandler(
    IShoppingListRepository lists,
    IIngredientNameRepository names,
    IMealPlanRepository plans,
    IRecipeRepository recipes,
    IHouseholdRepository households,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RefreshPlannedMealOnListCommand, Response>
{
    public async Task<Result<Response>> Handle(
        RefreshPlannedMealOnListCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Shopping.RefreshPlannedMeal");

        var member = await households
            .IsMemberAsync(command.HouseholdId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (!member)
        {
            return tracked.Record(
                Result<Response>.Failure(HouseholdErrors.NotFound(command.HouseholdId)));
        }

        var found = await plans
            .FindAsync(command.PlanEntryId, command.HouseholdId, cancellationToken)
            .ConfigureAwait(false);

        var result = await found.Match(
            entry => RefreshAsync(command.HouseholdId, entry, cancellationToken),
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result<Response>> RefreshAsync(
        Guid householdId,
        Domain.Planning.MealPlanEntry entry,
        CancellationToken cancellationToken)
    {
        var cooked = await recipes
            .FindManyAsync([entry.RecipeId], cancellationToken)
            .ConfigureAwait(false);

        var overrides = await lists
            .SectionOverridesAsync(householdId, cancellationToken)
            .ConfigureAwait(false);

        return await ShoppingListWrites
            .ApplyAsync(
                lists,
                names,
                unitOfWork,
                householdId,
                (list, _) =>
                {
                    if (!list.IsShoppedFor(entry.Id))
                    {
                        return Task.FromResult(Result.Success());
                    }

                    list.Withdraw(entry.Id);

                    // A recipe deleted since has nothing left to buy, so only the old share is taken off.
                    return Task.FromResult(
                        cooked.TryGetValue(entry.RecipeId, out var recipe)
                            ? RecipeContribution.Add(
                                list,
                                recipe,
                                entry.Servings ?? recipe.Yield.Amount,
                                entry.Id,
                                entry.Date,
                                entry.Slot,
                                overrides)
                            : Result.Success());
                },
                cancellationToken)
            .ConfigureAwait(false);
    }
}
