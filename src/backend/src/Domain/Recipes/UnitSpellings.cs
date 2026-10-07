namespace Domain.Recipes;

/// <summary>The words people write for the built-in units, in both languages.</summary>
/// <remarks>
/// "Gramm" and "EL" are <c>g</c> and <c>tbsp</c> written out; kept as written they would be
/// counting units that never convert or sum with the built-in. Plurals are listed, not stripped
/// (<c>Zitronen</c> is no unit). The frontend's <c>parseIngredientLine.ts</c> reads the same
/// spellings.
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
    internal static Unit? Resolve(string written) =>
        Unit.BuiltIn.FirstOrDefault(one => one.Code.Equals(written, StringComparison.OrdinalIgnoreCase))
            ?? ByFold.GetValueOrDefault(Fold(written));

    /// <summary>
    /// Lower case, no closing full stop, umlauts written out, so <c>Stück</c>, <c>Stueck</c> and
    /// <c>Stk.</c> all find their entry.
    /// </summary>
    private static string Fold(string written) =>
        written.TrimEnd('.').ToLowerInvariant()
            .Replace("ä", "ae", StringComparison.Ordinal)
            .Replace("ö", "oe", StringComparison.Ordinal)
            .Replace("ü", "ue", StringComparison.Ordinal)
            .Replace("ß", "ss", StringComparison.Ordinal);
}
