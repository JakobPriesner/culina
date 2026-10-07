using Domain.Recipes;
using Domain.Shopping;

namespace Infrastructure.Persistence.Shopping;

/// <summary>How shopping values are spelled in the database: text, so a migration inserting a section cannot reassign rows.</summary>
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

    /// <summary>A unit is stored as its code; recipes and shopping lists share one vocabulary.</summary>
    internal static string? Of(Unit? unit) => unit?.Code;

    /// <summary>A stored unit, or null; an unparseable unit reads as unmeasured rather than failing the read.</summary>
    internal static Unit? ToUnit(string? value) =>
        string.IsNullOrEmpty(value) ? null : Unit.Create(value).Match<Unit?>(one => one, _ => null);
}
