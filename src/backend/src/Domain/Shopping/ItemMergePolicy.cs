using Domain.Recipes;

namespace Domain.Shopping;

/// <summary>
/// Whether two lines are the same thing to buy.
/// </summary>
/// <remarks>
/// <para>
/// The substance of the feature. Adding three recipes to a list and getting
/// "200 g butter", "50 g butter" and "1 tbsp butter" as three lines is how a
/// shopping list stops being worth using.
/// </para>
/// <para>
/// Two lines merge when the names match — folded, so Müsli meets Muesli — and
/// the units can be added at all. Spoons never convert to millilitres (a US
/// tablespoon is 14.8 ml, a metric one 15, an Australian one 20, and a recipe
/// rarely says which), so "2 tbsp oil" and "30 ml oil" stay two lines. That is
/// not a shortcoming: inventing the conversion would invent precision the
/// recipe never had.
/// </para>
/// </remarks>
public static class ItemMergePolicy
{
    /// <summary>Whether an existing line can absorb a new amount of the same name.</summary>
    /// <param name="existing">The line already on the list.</param>
    /// <param name="name">The name being added.</param>
    /// <param name="quantity">The amount being added.</param>
    public static bool CanMerge(ShoppingListItem existing, ItemName name, Quantity quantity)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(quantity);

        if (!NamesMatch(existing.Name, name))
        {
            return false;
        }

        // Two lines with no amount are still one thing to buy: "salt" and
        // "salt" is salt.
        if (!existing.Quantity.IsMeasured && !quantity.IsMeasured)
        {
            return true;
        }

        return existing.Quantity.CanCombineWith(quantity);
    }

    /// <summary>Whether two names refer to the same thing in a trolley.</summary>
    /// <param name="left">One name.</param>
    /// <param name="right">The other.</param>
    public static bool NamesMatch(ItemName left, ItemName right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return string.Equals(left.ComparisonKey, right.ComparisonKey, StringComparison.Ordinal);
    }
}
