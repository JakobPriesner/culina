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
/// <param name="HouseholdId">Whose list.</param>
/// <param name="UserId">Who is asking.</param>
/// <param name="From">The day the week starts on.</param>
/// <param name="EntryId">Only this planned meal of the week, or null for all of them.</param>
/// <param name="Except">With an entry: its recipe's ingredients to leave off.</param>
public sealed record AddPlannedMealsToListCommand(
    Guid HouseholdId,
    Guid UserId,
    DateOnly From,
    Guid? EntryId = null,
    IReadOnlyCollection<Guid>? Except = null);

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
        var planned = await plans
            .ForWeekAsync(command.HouseholdId, command.From, PlanWeek.Days, cancellationToken)
            .ConfigureAwait(false);

        var week = command.EntryId is { } only
            ? [.. planned.Where(meal => meal.Entry.Id == only)]
            : planned;

        var cooked = await RecipesOfAsync(week, cancellationToken).ConfigureAwait(false);

        var overrides = await lists
            .SectionOverridesAsync(command.HouseholdId, cancellationToken)
            .ConfigureAwait(false);

        return Shop(list, week, cooked, overrides, command.EntryId is null ? [] : command.Except ?? []);
    }

    private static Result Shop(
        ShoppingList list,
        IReadOnlyList<PlannedRecipe> week,
        IReadOnlyDictionary<Guid, Recipe> cooked,
        IReadOnlyDictionary<string, ShoppingSection> overrides,
        IReadOnlyCollection<Guid> except)
    {
        var outcome = Result.Success();

        foreach (var planned in week)
        {
            var entry = planned.Entry;

            // Leftovers were bought with the meal they are the rest of.
            if (entry.LeftoverOf is not null ||
                list.IsShoppedFor(entry.Id) ||
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
                overrides,
                except));
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
