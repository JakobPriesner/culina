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
    /// <summary>Every unit code, in the order a picker should offer them.</summary>
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
}
