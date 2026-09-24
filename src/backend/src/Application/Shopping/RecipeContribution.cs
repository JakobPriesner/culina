using Domain.Recipes;
using Domain.Shared;
using Domain.Shopping;

namespace Application.Shopping;

/// <summary>
/// What a recipe puts on the list, at the servings being cooked.
/// </summary>
/// <remarks>
/// One path for every way a recipe reaches the list — from its page, from a
/// cookbook, from a planned week — because merging is the entire value of the
/// list, and two paths would be two places for it to be subtly different.
/// </remarks>
internal static class RecipeContribution
{
    /// <summary>Adds every ingredient, each remembering which recipe and meal asked for it.</summary>
    /// <param name="list">The list to add to.</param>
    /// <param name="recipe">The recipe.</param>
    /// <param name="servings">How many it is being made for.</param>
    /// <param name="planEntryId">The planned meal it is for, or null for the recipe by itself.</param>
    /// <param name="overrides">Where this household says things are found.</param>
    internal static Result Add(
        ShoppingList list,
        Recipe recipe,
        decimal servings,
        Guid? planEntryId,
        IReadOnlyDictionary<string, ShoppingSection> overrides)
    {
        // Exact decimal arithmetic, and the sum is stored unrounded. A recipe
        // for two scaled to five contributes 2.5 × its amounts, and three such
        // recipes must add up to what they actually add up to — rounding each
        // one first would compound the error into a number nobody asked for.
        var factor = recipe.Yield.Amount > 0 ? servings / recipe.Yield.Amount : 1m;

        // `Bind` short-circuits, so the first ingredient that cannot be read
        // stops the rest: half a recipe on the list is worse than none of it.
        return recipe.Groups
            .SelectMany(group => group.Ingredients)
            .Aggregate(
                Result.Success(),
                (outcome, ingredient) => outcome.Bind(() => ItemName
                    .Create(ingredient.Name)
                    .Bind(name => list.Add(
                        name,
                        new ShoppingItemSource(recipe.Id, planEntryId, Scale(ingredient.Quantity, factor)),
                        AddShoppingItemCommandHandler.SectionFor(name, overrides)))
                    .Bind(_ => Result.Success())));
    }

    /// <summary>
    /// Multiplies an amount, leaving alone the ones that do not scale.
    /// </summary>
    /// <remarks>
    /// A pinch is a gesture: doubling a recipe does not double it. An
    /// ingredient with no amount has nothing to multiply.
    /// </remarks>
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
