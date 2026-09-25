namespace Contracts.Recipes.GetRelated;

/// <summary>The recipes of the same household most like one of its own.</summary>
public sealed record Response
{
    /// <summary>The closest first. Empty when nothing is alike enough to say so.</summary>
    public required IReadOnlyList<RelatedRecipe> Items { get; init; }
}

/// <summary>A recipe like the one being read, and why.</summary>
public sealed record RelatedRecipe
{
    /// <summary>Which recipe.</summary>
    public required Guid RecipeId { get; init; }

    /// <summary>Its title.</summary>
    public required string Title { get; init; }

    /// <summary>Its picture, if it has one.</summary>
    public Guid? ImageId { get; init; }

    /// <summary>Its total time, where it states one.</summary>
    public int? TotalMinutes { get; init; }

    /// <summary>How much it makes.</summary>
    public required decimal YieldAmount { get; init; }

    /// <summary><c>servings</c> or <c>pieces</c>.</summary>
    public required string YieldKind { get; init; }

    /// <summary>The recipe's own word for what it makes, or null for the usual one.</summary>
    public string? YieldLabel { get; init; }

    /// <summary>Its tag slugs.</summary>
    public required IReadOnlyList<string> Tags { get; init; }

    /// <summary>How many times the person asking has made it.</summary>
    public required int CookCount { get; init; }

    /// <summary>When the person asking last made it, or null.</summary>
    public DateTimeOffset? LastCookedAt { get; init; }

    /// <summary>When it was last changed.</summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>What the two have in common, to be shown beside it.</summary>
    public required RelatedReason Reason { get; init; }
}

/// <summary>
/// Why two recipes are related, in words a person can disagree with.
/// </summary>
/// <remarks>
/// A suggestion whose reason is shown is one that can be argued with, which
/// is what makes it feel like a tool rather than a slot machine.
/// </remarks>
public sealed record RelatedReason
{
    /// <summary>
    /// <c>kinds</c> when what they are is what they share most — both pasta
    /// bakes, both Italian — or <c>ingredients</c> when it is what they are
    /// made from.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// At most three things they share, the most telling first, worded in the
    /// language of the recipe being read.
    /// </summary>
    public required IReadOnlyList<string> Shared { get; init; }
}
