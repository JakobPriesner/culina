namespace Contracts.Recipes;

/// <summary>
/// The closed sets a recipe carries as strings on the wire.
/// </summary>
/// <remarks>
/// <para>
/// These are wire codes, not domain names: an ingredient is <c>g</c>, never
/// <c>gram</c>, because that is what a recipe writes and what a form shows.
/// They live in Contracts because that is what they are — part of the format —
/// and because Contracts is a leaf, so the domain can spell its enum however
/// suits the domain.
/// </para>
/// <para>
/// One list, read three times: the reader maps a unit onto it, the writer maps
/// it back, and the OpenAPI document publishes it so a generated client gets a
/// union rather than <c>string</c>. Anything that lets those three disagree is
/// a contract that lies, and <c>RecipeVocabularyTests</c> asserts they cannot.
/// </para>
/// </remarks>
public static class RecipeVocabulary
{
    /// <summary>
    /// The units every household starts with, in the order a picker offers
    /// them.
    /// </summary>
    /// <remarks>
    /// Not a closed set. These are the units that <em>convert</em> — a kilo is
    /// a thousand grams for everyone — and they are published so a generated
    /// client shares that table rather than redeclaring it. A household adds a
    /// unit by writing one, and what it writes counts things: it scales with
    /// the portions, it adds to itself, and it converts to nothing.
    /// </remarks>
    public static readonly IReadOnlyList<string> Units =
    [
        "g", "kg", "ml", "l", "tsp", "tbsp",
        "piece", "clove", "bunch", "slice", "can", "pack", "pinch"
    ];

    /// <summary>What a recipe makes.</summary>
    public static readonly IReadOnlyList<string> YieldKinds = ["servings", "pieces"];

    /// <summary>The two kinds of piece a step's text is made of.</summary>
    public static readonly IReadOnlyList<string> StepSegmentKinds = ["text", "ingredient"];

    /// <summary>The languages a recipe can be written in.</summary>
    public static readonly IReadOnlyList<string> Languages = ["en", "de"];

    /// <summary>
    /// Where in a shop a thing is found, in the order a shop is walked.
    /// </summary>
    /// <remarks>
    /// The order is the entire value of sections — a list read top to bottom is
    /// a route rather than a scavenger hunt — so it is part of the contract
    /// rather than something each client decides for itself.
    /// </remarks>
    public static readonly IReadOnlyList<string> ShoppingSections =
    [
        "produce", "dairy_eggs", "meat_fish", "bakery", "dry_goods",
        "canned_jars", "frozen", "spices_baking", "drinks", "household", "other"
    ];
}
