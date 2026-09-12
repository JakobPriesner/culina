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

    /// <summary>The same unit codes a recipe uses, so one vocabulary spans both.</summary>
    internal static string? Of(Unit? unit) => unit switch
    {
        null => null,
        Unit.Gram => "g",
        Unit.Kilogram => "kg",
        Unit.Millilitre => "ml",
        Unit.Litre => "l",
        Unit.Teaspoon => "tsp",
        Unit.Tablespoon => "tbsp",
        Unit.Piece => "piece",
        Unit.Clove => "clove",
        Unit.Bunch => "bunch",
        Unit.Slice => "slice",
        Unit.Can => "can",
        Unit.Pack => "pack",
        Unit.Pinch => "pinch",
        _ => null
    };

    internal static Unit? ToUnit(string? value) => value switch
    {
        "g" => Unit.Gram,
        "kg" => Unit.Kilogram,
        "ml" => Unit.Millilitre,
        "l" => Unit.Litre,
        "tsp" => Unit.Teaspoon,
        "tbsp" => Unit.Tablespoon,
        "piece" => Unit.Piece,
        "clove" => Unit.Clove,
        "bunch" => Unit.Bunch,
        "slice" => Unit.Slice,
        "can" => Unit.Can,
        "pack" => Unit.Pack,
        "pinch" => Unit.Pinch,
        _ => null
    };
}
