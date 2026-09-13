using Domain.Recipes;
using Domain.Shopping;

namespace Infrastructure.Persistence.Shopping;

/// <summary>
/// How shopping values are spelled in the database.
/// </summary>
/// <remarks>
/// Stored as text rather than as an integer, so a migration that inserts a new
/// section in the middle cannot silently reassign every row — and so a person
/// reading the table can see what it says.
/// </remarks>
internal static class ShoppingWords
{
    internal static string Of(ShoppingSection section) => section switch
    {
        ShoppingSection.Produce => "produce",
        ShoppingSection.DairyEggs => "dairy_eggs",
        ShoppingSection.MeatFish => "meat_fish",
        ShoppingSection.Bakery => "bakery",
        ShoppingSection.DryGoods => "dry_goods",
        ShoppingSection.CannedJars => "canned_jars",
        ShoppingSection.Frozen => "frozen",
        ShoppingSection.SpicesBaking => "spices_baking",
        ShoppingSection.Drinks => "drinks",
        ShoppingSection.Household => "household",
        _ => "other"
    };

    internal static ShoppingSection? ToSection(string? value) => value switch
    {
        "produce" => ShoppingSection.Produce,
        "dairy_eggs" => ShoppingSection.DairyEggs,
        "meat_fish" => ShoppingSection.MeatFish,
        "bakery" => ShoppingSection.Bakery,
        "dry_goods" => ShoppingSection.DryGoods,
        "canned_jars" => ShoppingSection.CannedJars,
        "frozen" => ShoppingSection.Frozen,
        "spices_baking" => ShoppingSection.SpicesBaking,
        "drinks" => ShoppingSection.Drinks,
        "household" => ShoppingSection.Household,
        "other" => ShoppingSection.Other,
        _ => null
    };

    /// <summary>
    /// A unit is stored as the code it carries. One vocabulary spans a recipe
    /// and a shopping list, and there is nothing to translate between them.
    /// </summary>
    internal static string? Of(Unit? unit) => unit?.Code;

    /// <summary>
    /// A stored unit, or null when the row has none.
    /// </summary>
    /// <remarks>
    /// A row whose unit no longer parses is read as unmeasured rather than
    /// crashing the read: a list that mostly renders beats an error.
    /// </remarks>
    internal static Unit? ToUnit(string? value) =>
        string.IsNullOrEmpty(value) ? null : Unit.Create(value).Match<Unit?>(one => one, _ => null);
}
