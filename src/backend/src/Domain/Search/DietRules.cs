namespace Domain.Search;

/// <summary>
/// What a diet rules out, in the lexicon's terms.
/// </summary>
/// <remarks>
/// <para>
/// Culina has no nutrition table and will not have one, so a diet is honest in
/// one direction only. Ruling a recipe <em>out</em> is reliable: an ingredient
/// that is a kind of meat, fish or seafood means what it says, and the lexicon
/// already knows that Hackfleisch, Speck and Lachs are. Ruling one <em>in</em>
/// is not, because "no meat was found" is a presumption — Brühe, Gelatine and
/// Parmesan are exactly the things a name does not give away.
/// </para>
/// <para>
/// So a recipe counts as keeping a diet when somebody said so — its title or a
/// tag names the diet — or, for the two diets an ingredient list can refute,
/// when nothing in it does. The other diets (gluten-free, lactose-free, low
/// carb) cannot be refuted by a name at all, and so are only ever what
/// somebody said.
/// </para>
/// </remarks>
public static class DietRules
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Refuted =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["vegetarian"] = ["meat", "fish", "seafood", "gelatine"],
            ["vegan"] = ["meat", "fish", "seafood", "gelatine", "dairy", "egg", "animal_product"]
        };

    /// <summary>
    /// The concepts that rule a recipe out of this diet, or null when no
    /// ingredient can: then only a recipe that says so keeps it.
    /// </summary>
    public static IReadOnlyList<string>? RefutedBy(string diet) =>
        Refuted.GetValueOrDefault(diet);
}
