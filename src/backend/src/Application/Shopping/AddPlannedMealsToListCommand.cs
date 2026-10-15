using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Households;
using Application.Planning;
using Application.Telemetry;
using Contracts.Shopping;
using Domain.Recipes;
using Domain.Shared;
using Domain.Shopping;

namespace Application.Shopping;

/// <summary>Puts a planned week's shopping on the list — each meal once.</summary>
/// <remarks>Safe to repeat: a meal already on the list is skipped, and a recipe added by itself counts as shopping for a planned meal of it. Two planned meals of one recipe are two meals.</remarks>
public sealed record AddPlannedMealsToListCommand(Guid HouseholdId, Guid UserId, DateOnly From);

internal sealed class AddPlannedMealsToListCommandHandler(
    IShoppingListRepository lists,
    IIngredientNameRepository names,
    IMealPlanRepository plans,
    IRecipeRepository recipes,
    IHouseholdRepository households,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AddPlannedMealsToListCommand, Response>
{
    public async Task<Result<Response>> Handle(
        AddPlannedMealsToListCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Shopping.AddPlannedMeals");

        var allowed = await HouseholdAccess
            .MemberOfAsync(households, command.HouseholdId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await allowed.Bind(() => PlanningWords.Plannable(command.From)).Match(
            () => ShoppingListWrites.ApplyAsync(
                lists,
                names,
                unitOfWork,
                command.HouseholdId,
                (list, token) => ShopAsync(list, command, token),
                cancellationToken),
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    /// <summary>One attempt: the week is read inside it, so a retry sees a meal withdrawn since the last one.</summary>
    private async Task<Result> ShopAsync(
        ShoppingList list,
        AddPlannedMealsToListCommand command,
        CancellationToken cancellationToken)
    {
        var week = await plans
            .ForWeekAsync(command.HouseholdId, command.From, PlanWeek.Days, cancellationToken)
            .ConfigureAwait(false);

        var cooked = await RecipesOfAsync(week, cancellationToken).ConfigureAwait(false);

        var overrides = await lists
            .SectionOverridesAsync(command.HouseholdId, cancellationToken)
            .ConfigureAwait(false);

        return Shop(list, week, cooked, overrides);
    }

    private static Result Shop(
        ShoppingList list,
        IReadOnlyList<PlannedRecipe> week,
        IReadOnlyDictionary<Guid, Recipe> cooked,
        IReadOnlyDictionary<string, ShoppingSection> overrides)
    {
        var outcome = Result.Success();

        foreach (var planned in week)
        {
            var entry = planned.Entry;

            if (list.IsShoppedFor(entry.Id) ||
                list.CountFor(entry.RecipeId, entry.Id, entry.Date, entry.Slot))
            {
                continue;
            }

            // A recipe deleted since the week was read has nothing left to buy.
            if (!cooked.TryGetValue(entry.RecipeId, out var recipe))
            {
                continue;
            }

            outcome = outcome.Bind(() => RecipeContribution.Add(
                list,
                recipe,
                entry.Servings ?? recipe.Yield.Amount,
                entry.Id,
                entry.Date,
                entry.Slot,
                overrides));
        }

        return outcome;
    }

    /// <summary>Each recipe on the week, read once however often it is planned.</summary>
    private async Task<IReadOnlyDictionary<Guid, Recipe>> RecipesOfAsync(
        IReadOnlyList<PlannedRecipe> week,
        CancellationToken cancellationToken)
    {
        // A recipe deleted since the week was read is simply absent.
        return await recipes
            .FindManyAsync([.. week.Select(planned => planned.Entry.RecipeId).Distinct()], cancellationToken)
            .ConfigureAwait(false);
    }
}
