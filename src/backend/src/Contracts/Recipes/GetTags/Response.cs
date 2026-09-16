namespace Contracts.Recipes.GetTags;

/// <summary>The tags a household's recipes carry.</summary>
/// <remarks>
/// Not paged. A household's vocabulary is a few dozen words at most, and a
/// filter bar that had to page through the things it filters by would be a
/// filter bar nobody used.
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
    /// How many recipes carry it.
    /// </summary>
    /// <remarks>
    /// Shown beside each one, because a tag on two recipes and a tag on forty
    /// are different offers and a list that hid the difference would rank them
    /// the same.
    /// </remarks>
    public required int RecipeCount { get; init; }
}
