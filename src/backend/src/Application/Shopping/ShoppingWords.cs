using Domain.Shared;
using Domain.Shopping;

namespace Application.Shopping;

/// <summary>
/// How shopping values travel on the wire.
/// </summary>
/// <remarks>
/// The same shape as <c>RecipeWords</c>, and for the same reason: a contract is
/// a wire format, and the domain should be free to spell its enums however suits
/// the domain.
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

    internal static Result<ShoppingSection> ToSection(string? value) => value switch
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
        _ => new FieldError("section", ShoppingErrors.ItemNotFound.Code, $"'{value}' is not a section.")
    };
}
