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
        // Exact decimal arithmetic, summed unrounded: rounding each recipe first would compound the
        // error.
        var factor = recipe.Yield.Amount > 0 ? servings / recipe.Yield.Amount : 1m;

        // `Bind` short-circuits: half a recipe on the list is worse than none.
        return recipe.Groups
            .SelectMany(group => group.Ingredients)
            .Aggregate(
                Result.Success(),
                (outcome, ingredient) => outcome.Bind(() => ItemName
                    .Create(ingredient.Name)
                    .Bind(name => list.Add(
                        name,
                        new ShoppingItemSource(
                            recipe.Id,
                            recipe.Title.Value,
                            planEntryId,
                            plannedDate,
                            plannedSlot,
                            Scale(ingredient.Quantity, factor)),
                        AddShoppingItemCommandHandler.SectionFor(name, overrides)))
                    .Bind(_ => Result.Success())));
    }

    /// <summary>
    /// Multiplies an amount, leaving alone the ones that do not scale: a pinch is a gesture, and no
    /// amount has nothing to multiply.
    /// </summary>
    private static Quantity Scale(Quantity quantity, decimal factor)
    {
        if (!quantity.Scales || quantity.Amount is not { } amount)
        {
            return quantity;
        }

        return Quantity.Create(amount * factor, quantity.Unit)
            .Match(scaled => scaled, _ => quantity);
    }
}
