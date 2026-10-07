using Domain.Recipes;

namespace Domain.Shopping;

/// <summary>Whether two lines are the same thing to buy.</summary>
/// <remarks>
/// Lines merge when the folded names match (Müsli meets Muesli) and the units can be added. Spoons
/// never convert to millilitres (tablespoons differ by country), so "2 tbsp oil" and "30 ml oil"
/// stay two lines rather than invent precision.
/// </remarks>
public static class ItemMergePolicy
{
    /// <summary>Whether an existing line can absorb a new amount of the same name.</summary>
    public static bool CanMerge(ShoppingListItem existing, ItemName name, Quantity quantity)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(quantity);

        if (!NamesMatch(existing.Name, name))
        {
            return false;
        }

        // Two lines with no amount are still one thing to buy: "salt" and "salt" is salt.
        if (!existing.Quantity.IsMeasured && !quantity.IsMeasured)
        {
            return true;
        }

        return existing.Quantity.CanCombineWith(quantity);
    }

    /// <summary>Whether two names refer to the same thing in a trolley.</summary>
    public static bool NamesMatch(ItemName left, ItemName right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return string.Equals(left.ComparisonKey, right.ComparisonKey, StringComparison.Ordinal);
    }
}
