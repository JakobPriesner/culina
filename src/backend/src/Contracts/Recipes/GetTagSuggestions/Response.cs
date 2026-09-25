namespace Contracts.Recipes.GetTagSuggestions;

/// <summary>Tags a recipe could carry and does not, for somebody to add with one tap.</summary>
public sealed record Response
{
    /// <summary>At most five, the household's own tags first.</summary>
    public required IReadOnlyList<TagSuggestion> Items { get; init; }
}

/// <summary>One tag worth offering.</summary>
public sealed record TagSuggestion
{
    /// <summary>
    /// What to show and, when added, what to save: the household's own name for
    /// it where it has one, or the lexicon's word in the recipe's language.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The household's tag, when it already uses one for this; null for a tag
    /// that adding would create.
    /// </summary>
    public string? Slug { get; init; }
}
