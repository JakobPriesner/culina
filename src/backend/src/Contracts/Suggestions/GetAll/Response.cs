namespace Contracts.Suggestions.GetAll;

/// <summary>A handful of recipes for one occasion.</summary>
/// <remarks>Deliberately without a cursor: this is bounded. Ranking the whole library is <c>GET /recipes?sort=suggested</c>.</remarks>
public sealed record Response
{
    /// <summary>The suggestions, best first.</summary>
    public required IReadOnlyList<Suggestion> Items { get; init; }
}

/// <summary>One recipe, and why it is here.</summary>
public sealed record Suggestion
{
    /// <summary>Energy per serving or piece; null if nothing can be calculated.</summary>
    public Contracts.Recipes.GetNutrition.NutritionValue? Calories { get; init; }

    /// <summary>The recipe's id.</summary>
    public required Guid RecipeId { get; init; }

    /// <summary>The household it belongs to; not the one asked about when that one inherits it.</summary>
    public required Guid HouseholdId { get; init; }

    /// <summary>What it is called.</summary>
    public required string Title { get; init; }

    /// <summary>Its hero image, if it has one.</summary>
    public Guid? ImageId { get; init; }

    /// <summary>Prep plus cook, or null when neither is known.</summary>
    public int? TotalMinutes { get; init; }

    /// <summary>How many it makes.</summary>
    public required decimal YieldAmount { get; init; }

    /// <summary><c>servings</c> or <c>pieces</c>.</summary>
    public required string YieldKind { get; init; }

    /// <summary>
    /// The recipe's own word for what it makes, such as "Cake". Null for most; when set it is shown
    /// as written, otherwise the client words the yield from <c>yieldKind</c>.
    /// </summary>
    public string? YieldLabel { get; init; }

    /// <summary>Its tags.</summary>
    public required IReadOnlyList<string> Tags { get; init; }

    /// <summary>How many times the caller has made it.</summary>
    public required int CookCount { get; init; }

    /// <summary>When the caller last made it, or null.</summary>
    public DateTimeOffset? LastCookedAt { get; init; }

    /// <summary>When the recipe last changed.</summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Why this one, or null when no single term decided it. Null renders as nothing; never invent a reason.</summary>
    public SuggestionReasonView? Reason { get; init; }
}

/// <summary>Why a recipe was suggested.</summary>
/// <remarks>A code and at most a subject, never a sentence: the client words it in the reader's language.</remarks>
public sealed record SuggestionReasonView
{
    /// <summary>
    /// One of <c>affinity</c>, <c>rediscovery</c>, <c>tag</c>, <c>ingredient</c>, <c>season</c>, <c>slot</c>,
    /// <c>household</c>, <c>fresh</c>, <c>similar</c>.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>What the reason is about (a tag, ingredient or member name), when nameable.</summary>
    public string? Subject { get; init; }
}
