namespace Domain.Search;

/// <summary>What kind of thing a culinary concept is.</summary>
public enum ConceptKind
{
    /// <summary>Something a recipe is made from: chicken, tomato, rice.</summary>
    Ingredient,

    /// <summary>Something a recipe is: a lasagne, a curry, a soup.</summary>
    Dish,

    /// <summary>Where a dish comes from: Italian, Asian.</summary>
    Cuisine,

    /// <summary>When it is eaten: breakfast, dessert.</summary>
    Meal,

    /// <summary>How it is cooked: baked, grilled.</summary>
    Method,

    /// <summary>What it leaves out: vegetarian, vegan.</summary>
    Diet,

    /// <summary>What it is like: warm, light, hearty, summery.</summary>
    Character
}

/// <summary>
/// One entry of the culinary lexicon: the words that mean one thing.
/// </summary>
/// <param name="Key">Stable and never shown to anybody — "chicken".</param>
/// <param name="Kind">What kind of thing it is.</param>
/// <param name="De">
/// The German surface forms, written as a person writes them. The first is the
/// one a German reader is shown.
/// </param>
/// <param name="En">The English surface forms; the first is the one shown.</param>
/// <param name="Parents">
/// What it is a kind of, one level up. A recipe that uses chicken also counts
/// as using poultry, and poultry as meat; the lexicon closes the chain once, at
/// startup, rather than walking it while anybody waits.
/// </param>
public sealed record Concept(
    string Key,
    ConceptKind Kind,
    IReadOnlyList<string> De,
    IReadOnlyList<string> En,
    IReadOnlyList<string> Parents);
