using Domain.Recipes;
using Domain.Shared;

namespace Application.Recipes;

/// <summary>The wire spelling of each recipe enum, and the parse back; names the wrong field so a form can mark the control.</summary>
internal static class RecipeWords
{
    internal static string Of(Language language) => language == Language.De ? "de" : "en";

    internal static string Of(YieldKind kind) => kind == YieldKind.Pieces ? "pieces" : "servings";

    // The unit as written: it carries its own wire code.
    internal static string? Of(Unit? unit) => unit?.Code;

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

    // One call because "no unit" is legitimate and a Result cannot carry a null; the absence belongs inside Quantity.
    internal static Result<Quantity> ToQuantity(decimal? amount, string? unit) =>
        string.IsNullOrEmpty(unit)
            ? Quantity.Create(amount, null)
            : Unit.Create(unit).Match(
                measure => Quantity.Create(amount, measure),
                error => new FieldError("unit", error.Code, error.Description));
}
