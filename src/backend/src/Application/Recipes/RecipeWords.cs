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

    /// <summary>
    /// The unit, as it is written.
    /// </summary>
    /// <remarks>
    /// A unit carries its own wire code, so there is nothing to translate. The
    /// table that used to live here — and its two copies, in the shopping
    /// mapper and the row mapper — existed only because the unit was an enum.
    /// </remarks>
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
    internal static Result<Quantity> ToQuantity(decimal? amount, string? unit) =>
        string.IsNullOrEmpty(unit)
            ? Quantity.Create(amount, null)
            : Unit.Create(unit).Match(
                measure => Quantity.Create(amount, measure),
                error => new FieldError("unit", error.Code, error.Description));
}
