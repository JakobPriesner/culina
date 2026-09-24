namespace Contracts.Recipes.GetCompletions;

/// <summary>What a search field could offer while somebody is still typing.</summary>
public sealed record Response
{
    /// <summary>Recipes first, then ingredients, tags and at most one refinement.</summary>
    public required IReadOnlyList<Completion> Items { get; init; }
}

/// <summary>
/// One thing a half-typed query could become.
/// </summary>
/// <remarks>
/// Four kinds, and they do four different things, which is why they are told
/// apart rather than mixed into one list: a <c>recipe</c> is a destination, an
/// <c>ingredient</c> and a <c>tag</c> are filters, and a <c>refinement</c>
/// completes the query. Mixing them makes Enter mean four things.
/// </remarks>
public sealed record Completion
{
    /// <summary><c>recipe</c>, <c>ingredient</c>, <c>tag</c> or <c>refinement</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>
    /// What to show: a recipe's title, an ingredient's or a tag's name as the
    /// household writes it, or for a refinement the ingredient it narrows.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>The recipe, for a recipe.</summary>
    public Guid? RecipeId { get; init; }

    /// <summary>Its picture, for a recipe that has one.</summary>
    public Guid? ImageId { get; init; }

    /// <summary>Its total time, for a recipe that states one.</summary>
    public int? TotalMinutes { get; init; }

    /// <summary>The tag's slug, for a tag.</summary>
    public string? Slug { get; init; }

    /// <summary>How many recipes it would find, for an ingredient, a tag or a refinement.</summary>
    public int? RecipeCount { get; init; }

    /// <summary>
    /// The time ceiling a refinement adds, in minutes. The client words the
    /// query itself — "Hähnchen unter 30 Minuten" or "chicken under 30
    /// minutes" — because both read the same way.
    /// </summary>
    public int? MaxMinutes { get; init; }
}
