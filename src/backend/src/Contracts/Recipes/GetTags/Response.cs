namespace Contracts.Recipes.GetTags;

/// <summary>The tags a household's recipes carry.</summary>
/// <remarks>
/// Not paged: a household's vocabulary is a few dozen words, and a filter bar that paged through
/// what it filters by would go unused.
/// </remarks>
public sealed record Response
{
    /// <summary>The tags, most used first.</summary>
    public required IReadOnlyList<TagInUse> Items { get; init; }
}

/// <summary>One tag, and how much of the collection carries it.</summary>
public sealed record TagInUse
{
    /// <summary>The normalised form, which is what filters and rules name.</summary>
    public required string Slug { get; init; }

    /// <summary>The words somebody actually typed.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// How many recipes carry it, shown beside each tag since a tag on two recipes and on forty are
    /// different offers.
    /// </summary>
    public required int RecipeCount { get; init; }
}
