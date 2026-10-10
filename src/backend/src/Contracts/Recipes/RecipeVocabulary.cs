namespace Contracts.Recipes;

/// <summary>The closed sets a recipe carries as strings on the wire.</summary>
/// <remarks>
/// Wire codes, not domain names (<c>g</c>, never <c>gram</c>); they live in Contracts, a leaf, so
/// the domain can spell its enum as suits it. One list read three times (reader, writer, and the
/// OpenAPI union); <c>RecipeVocabularyTests</c> asserts the three cannot disagree.
/// </remarks>
public static class RecipeVocabulary
{
    /// <summary>The units every household starts with, in the order a picker offers them.</summary>
    /// <remarks>
    /// Not a closed set: these are the units that <em>convert</em> (a kilo is a thousand grams for
    /// everyone). A household's own unit counts things: it scales with portions, adds to itself and
    /// converts to nothing.
    /// </remarks>
    public static readonly IReadOnlyList<string> Units =
    [
        "g", "kg", "ml", "l", "tsp", "tbsp",
        "piece", "clove", "bunch", "slice", "can", "pack", "pinch"
    ];

    /// <summary>What a recipe makes.</summary>
    public static readonly IReadOnlyList<string> YieldKinds = ["servings", "pieces"];

    /// <summary>What a nutrition figure is per.</summary>
    public static readonly IReadOnlyList<string> NutritionBases = ["serving", "piece"];

    /// <summary>What can become of an ingredient line in a nutrition figure.</summary>
    public static readonly IReadOnlyList<string> NutritionStatuses =
        ["counted", "amountNotInGrams", "noAmount", "unknownFood", "excluded", "implausible"];

    /// <summary>Why a unit of a known food is not counted in a nutrition figure.</summary>
    public static readonly IReadOnlyList<string> NutritionReasons =
        ["spoonOfSolid", "volumeOfSolid", "count", "householdUnit"];

    /// <summary>How the grams of a counted line were reached.</summary>
    public static readonly IReadOnlyList<string> NutritionGramsBases = ["mass", "density", "eggSize"];

    /// <summary>The two kinds of piece a step's text is made of.</summary>
    public static readonly IReadOnlyList<string> StepSegmentKinds = ["text", "ingredient"];

    /// <summary>The languages a recipe can be written in.</summary>
    public static readonly IReadOnlyList<string> Languages = ["en", "de"];

    /// <summary>Where in a shop a thing is found, in the order a shop is walked.</summary>
    /// <remarks>
    /// The order is the value of sections (a route, not a scavenger hunt), so it is part of the
    /// contract.
    /// </remarks>
    public static readonly IReadOnlyList<string> ShoppingSections =
    [
        "produce", "dairy_eggs", "meat_fish", "bakery", "dry_goods",
        "canned_jars", "frozen", "spices_baking", "drinks", "household", "other"
    ];
}
