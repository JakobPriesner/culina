using Domain.Recipes;
using Domain.Shared;

namespace Application.Recipes;

/// <summary>
/// The wire spelling of each recipe enum, and the parse back.
/// </summary>
/// <remarks>
/// The read and the write both need this, so it lives once. Parsing names the
/// field that was wrong, so a form can mark the right control.
/// </remarks>
internal static class RecipeWords
{
    internal static string Of(Language language) => language == Language.De ? "de" : "en";

    internal static string Of(YieldKind kind) => kind == YieldKind.Pieces ? "pieces" : "servings";

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
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown unit.")
    };

    internal static Result<Language> ToLanguage(string? value) => value switch
    {
        "en" => Language.En,
        "de" => Language.De,
        _ => new FieldError("language", RecipeErrors.InvalidTitle.Code, "Language must be 'en' or 'de'.")
    };

    internal static Result<YieldKind> ToYieldKind(string? value) => value switch
    {
        "servings" => YieldKind.Servings,
        "pieces" => YieldKind.Pieces,
        _ => new FieldError(
            "yieldKind",
            RecipeErrors.InvalidYield.Code,
            "A recipe makes either 'servings' or 'pieces'.")
    };

    /// <summary>
    /// Parses an amount and a unit together.
    /// </summary>
    /// <param name="amount">How much, or null.</param>
    /// <param name="unit">The unit code, or null for no unit.</param>
    /// <remarks>
    /// One call rather than parsing the unit on its own, because "no unit" is a
    /// legitimate answer and a Result cannot carry a null: modelling absence as
    /// a null inside a success is exactly what Result&lt;T&gt; refuses, and
    /// rightly — the absence belongs inside Quantity, which has a name for it.
    /// </remarks>
    internal static Result<Quantity> ToQuantity(decimal? amount, string? unit) => unit switch
    {
        null or "" => Quantity.Create(amount, null),
        "g" => Quantity.Create(amount, Unit.Gram),
        "kg" => Quantity.Create(amount, Unit.Kilogram),
        "ml" => Quantity.Create(amount, Unit.Millilitre),
        "l" => Quantity.Create(amount, Unit.Litre),
        "tsp" => Quantity.Create(amount, Unit.Teaspoon),
        "tbsp" => Quantity.Create(amount, Unit.Tablespoon),
        "piece" => Quantity.Create(amount, Unit.Piece),
        "clove" => Quantity.Create(amount, Unit.Clove),
        "bunch" => Quantity.Create(amount, Unit.Bunch),
        "slice" => Quantity.Create(amount, Unit.Slice),
        "can" => Quantity.Create(amount, Unit.Can),
        "pack" => Quantity.Create(amount, Unit.Pack),
        "pinch" => Quantity.Create(amount, Unit.Pinch),
        _ => new FieldError("unit", RecipeErrors.InvalidQuantity.Code, $"'{unit}' is not a unit.")
    };
}
