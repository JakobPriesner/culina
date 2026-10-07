namespace Domain.Search;

/// <summary>
/// What a meal looks like, in the lexicon's terms, when no recipe says it is one.
/// </summary>
/// <remarks>
/// Hardly anybody tags a recipe <em>Abendessen</em>, so the search sets the meal aside as a
/// preference, not a filter. It needs what a meal resembles (lunch and dinner, usually warm) and
/// what it does not (breakfast or dessert is not dinner).
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

    /// <summary>
    /// The other meals, which a recipe that says it is one of them does not suit.
    /// </summary>
    public static IReadOnlyList<string> Unlike(string meal) =>
        [.. Meals.Except(LookLike(meal), StringComparer.Ordinal)];
}
