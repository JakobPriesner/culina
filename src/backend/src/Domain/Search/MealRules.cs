namespace Domain.Search;

/// <summary>
/// What a meal looks like, in the lexicon's terms, when no recipe says it is
/// one.
/// </summary>
/// <remarks>
/// <para>
/// Hardly anybody tags a recipe <em>Abendessen</em>, so "schnelles
/// Abendessen" usually finds no dinner at all and the search sets the meal
/// aside. Set aside, it used to vanish: the quickest things in the library
/// came first, and those are yoghurt bowls. It stays a preference instead,
/// and a preference needs to know two things the lexicon alone does not say.
/// </para>
/// <para>
/// What a meal resembles: lunch and dinner are the same kind of meal, and
/// both are usually something warm. And what it does not: a recipe that says
/// it is breakfast or dessert is the one answer to "dinner" that is certainly
/// wrong.
/// </para>
/// </remarks>
public static class MealRules
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Resembled =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["lunch"] = ["dinner", "warm"],
            ["dinner"] = ["lunch", "warm"]
        };

    private static readonly string[] Meals =
        [.. CulinaryLexicon.All.Where(concept => concept.Kind == ConceptKind.Meal).Select(concept => concept.Key)];

    /// <summary>The meal, and what a recipe that suits it without saying so is.</summary>
    public static IReadOnlyList<string> LookLike(string meal) =>
        [meal, .. Resembled.GetValueOrDefault(meal) ?? []];

    /// <summary>The other meals, which a recipe that says it is one of them does not suit.</summary>
    public static IReadOnlyList<string> Unlike(string meal) =>
        [.. Meals.Except(LookLike(meal), StringComparer.Ordinal)];
}
