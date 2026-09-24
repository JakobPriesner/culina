namespace Domain.Recipes;

/// <summary>
/// The words people write for the built-in units, in both languages.
/// </summary>
/// <remarks>
/// <para>
/// "Milliliter", "Gramm", "EL" and "Teelöffel" are not units a household made
/// up: they are <c>ml</c>, <c>g</c>, <c>tbsp</c> and <c>tsp</c> written out.
/// Kept as written, each would be a counting unit that never converts, never
/// sums with the built-in it means and shows its German name in an English
/// kitchen. Recipes arrive this way from every other app that stores units as
/// names, and cooks type them this way too.
/// </para>
/// <para>
/// Plurals are listed rather than stripped: German plurals are not a suffix
/// rule, and a rule that guessed would read <c>Zitronen</c> as a unit. The
/// frontend's line parser (<c>parseIngredientLine.ts</c>) reads the same
/// spellings out of a pasted line before anything reaches the server.
/// </para>
/// </remarks>
internal static class UnitSpellings
{
    private static readonly Dictionary<string, Unit> ByFold = new(StringComparer.Ordinal)
    {
        ["gr"] = Unit.Gram,
        ["gramm"] = Unit.Gram,
        ["gram"] = Unit.Gram,
        ["grams"] = Unit.Gram,
        ["gramme"] = Unit.Gram,
        ["kilo"] = Unit.Kilogram,
        ["kilos"] = Unit.Kilogram,
        ["kilogramm"] = Unit.Kilogram,
        ["kilogram"] = Unit.Kilogram,
        ["kilograms"] = Unit.Kilogram,
        ["milliliter"] = Unit.Millilitre,
        ["millilitre"] = Unit.Millilitre,
        ["milliliters"] = Unit.Millilitre,
        ["millilitres"] = Unit.Millilitre,
        ["liter"] = Unit.Litre,
        ["litre"] = Unit.Litre,
        ["liters"] = Unit.Litre,
        ["litres"] = Unit.Litre,
        ["tsps"] = Unit.Teaspoon,
        ["teaspoon"] = Unit.Teaspoon,
        ["teaspoons"] = Unit.Teaspoon,
        ["tl"] = Unit.Teaspoon,
        ["teeloeffel"] = Unit.Teaspoon,
        ["tbsps"] = Unit.Tablespoon,
        ["tbs"] = Unit.Tablespoon,
        ["tablespoon"] = Unit.Tablespoon,
        ["tablespoons"] = Unit.Tablespoon,
        ["el"] = Unit.Tablespoon,
        ["essloeffel"] = Unit.Tablespoon,
        ["stk"] = Unit.Piece,
        ["stueck"] = Unit.Piece,
        ["pieces"] = Unit.Piece,
        ["zehe"] = Unit.Clove,
        ["zehen"] = Unit.Clove,
        ["cloves"] = Unit.Clove,
        ["bund"] = Unit.Bunch,
        ["bunches"] = Unit.Bunch,
        ["scheibe"] = Unit.Slice,
        ["scheiben"] = Unit.Slice,
        ["slices"] = Unit.Slice,
        ["dose"] = Unit.Can,
        ["dosen"] = Unit.Can,
        ["cans"] = Unit.Can,
        ["packung"] = Unit.Pack,
        ["packungen"] = Unit.Pack,
        ["paeckchen"] = Unit.Pack,
        ["packs"] = Unit.Pack,
        ["packet"] = Unit.Pack,
        ["packets"] = Unit.Pack,
        ["prise"] = Unit.Pinch,
        ["prisen"] = Unit.Pinch,
        ["pinches"] = Unit.Pinch,
    };

    /// <summary>The built-in unit this word is a spelling of, or null.</summary>
    /// <param name="written">A unit as somebody wrote it, already trimmed.</param>
    internal static Unit? Resolve(string written) =>
        Unit.BuiltIn.FirstOrDefault(one => one.Code.Equals(written, StringComparison.OrdinalIgnoreCase))
            ?? ByFold.GetValueOrDefault(Fold(written));

    /// <summary>
    /// Lower case, no closing full stop, umlauts written out — so <c>Stück</c>,
    /// <c>Stueck</c> and <c>Stk.</c> all find their entry.
    /// </summary>
    private static string Fold(string written) =>
        written.TrimEnd('.').ToLowerInvariant()
            .Replace("ä", "ae", StringComparison.Ordinal)
            .Replace("ö", "oe", StringComparison.Ordinal)
            .Replace("ü", "ue", StringComparison.Ordinal)
            .Replace("ß", "ss", StringComparison.Ordinal);
}
