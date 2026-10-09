using Domain.Planning;
using Domain.Recipes;
using Domain.Shared;
using Domain.Shopping;

namespace Application.Shopping;

/// <summary>What a recipe puts on the list, at the servings being cooked.</summary>
/// <remarks>
/// One path for every way a recipe reaches the list (page, cookbook, planned week): merging is the
/// list's whole value, and two paths would drift.
/// </remarks>
internal static class RecipeContribution
{
    /// <summary>
    /// Adds every ingredient, each remembering which recipe and meal asked for it; a null
    /// <c>planEntryId</c> means the recipe by itself.
    /// </summary>
    internal static Result Add(
        ShoppingList list,
        Recipe recipe,
        decimal servings,
        Guid? planEntryId,
        DateOnly? plannedDate,
        MealSlot? plannedSlot,
        IReadOnlyDictionary<string, ShoppingSection> overrides)
    {
        servings = Amounts.Round(servings);

        if (servings is <= 0 or > 1000)
        {
            return PlanningErrors.InvalidServings;
        }

        // Exact decimal arithmetic; the amount is rounded once, to what the list stores.
        var factor = recipe.Yield.Amount > 0 ? servings / recipe.Yield.Amount : 1m;

        // `Bind` short-circuits: half a recipe on the list is worse than none.
        return recipe.Groups
            .SelectMany(group => group.Ingredients)
            .Aggregate(
                Result.Success(),
                (outcome, ingredient) => outcome.Bind(() => ItemName
                    .Create(ingredient.Name)
                    .Bind(name => Scale(ingredient.Quantity, factor).Bind(quantity => list.Add(
                        name,
                        new ShoppingItemSource(
                            recipe.Id,
                            recipe.Title.Value,
                            planEntryId,
                            plannedDate,
                            plannedSlot,
                            quantity),
                        AddShoppingItemCommandHandler.SectionFor(name, overrides))))
                    .Bind(_ => Result.Success())));
    }

    /// <summary>
    /// Multiplies an amount, leaving alone the ones that do not scale: a pinch is a gesture, and no
    /// amount has nothing to multiply. An amount scaled below what can be stored is no measurable
    /// amount any more, so it becomes one with none.
    /// </summary>
    private static Result<Quantity> Scale(Quantity quantity, decimal factor)
    {
        if (!quantity.Scales || quantity.Amount is not { } amount)
        {
            return quantity;
        }

        var scaled = amount * factor;

        return scaled < Amounts.Min
            ? Quantity.Unmeasured
            : Quantity.Create(scaled, quantity.Unit);
    }
}
