namespace Domain.Search;

/// <summary>What a diet rules out, in the lexicon's terms.</summary>
/// <remarks>
/// Ruling a recipe out by ingredient is reliable; ruling it in is only a presumption (Brühe, Gelatine
/// and Parmesan hide behind their names). Diets an ingredient list cannot refute are only ever what a title or tag says.
/// </remarks>
public static class DietRules
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Refuted =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["vegetarian"] = ["meat", "fish", "seafood", "gelatine", "not_vegetarian"],
            ["vegan"] =
            [
                "meat", "fish", "seafood", "gelatine", "dairy", "egg", "animal_product", "not_vegetarian", "not_vegan"
            ]
        };

    /// <summary>The concepts that rule a recipe out of this diet, or null when no ingredient can.</summary>
    public static IReadOnlyList<string>? RefutedBy(string diet) =>
        Refuted.GetValueOrDefault(diet);
}
