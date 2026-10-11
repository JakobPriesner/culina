namespace Contracts.Searches;

/// <summary>A household's saved searches.</summary>
/// <remarks>Not paged: a kitchen keeps a handful.</remarks>
public sealed record SavedSearchesResponse
{
    /// <summary>The searches, oldest first, so the row of chips stops moving.</summary>
    public required IReadOnlyList<SavedSearchDetail> Items { get; init; }
}

/// <summary>One saved search.</summary>
public sealed record SavedSearchDetail
{
    /// <summary>The saved search's id.</summary>
    public required Guid SearchId { get; init; }

    /// <summary>Which household owns it.</summary>
    public required Guid HouseholdId { get; init; }

    /// <summary>What it is called.</summary>
    public required string Name { get; init; }

    /// <summary>What it asks the library for.</summary>
    public required SearchCriteriaContract Criteria { get; init; }

    /// <summary>Whose search it was.</summary>
    public required Guid CreatedBy { get; init; }

    /// <summary>When it was saved.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>When it last changed.</summary>
    public required DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>What a saved search asks for: the <c>query</c>, <c>tag</c>, <c>maxMinutes</c> and <c>sort</c> of <c>GET /recipes</c>.</summary>
public sealed record SearchCriteriaContract
{
    /// <summary>The words that were in the search box, or omit.</summary>
    public string? Query { get; init; }

    /// <summary>Tag slugs a recipe must all carry.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>The longest a recipe may take, or omit for any length.</summary>
    public int? MaxMinutes { get; init; }

    /// <summary>Maximum kcal per serving or piece, or omit.</summary>
    public int? MaxKcal { get; init; }

    /// <summary>
    /// The order to read in, or omit for the default. One of <c>relevance</c>, <c>suggested</c>, <c>-updatedAt</c>,
    /// <c>title</c>, <c>totalMinutes</c> or <c>-cookCount</c> (not <c>cookbookOrder</c>, which needs a cookbook).
    /// </summary>
    public string? Sort { get; init; }
}

/// <summary>Saves a search.</summary>
public sealed record CreateSavedSearchRequest
{
    /// <summary>Whose kitchen it is for.</summary>
    public required Guid HouseholdId { get; init; }

    /// <summary>What to call it.</summary>
    public required string Name { get; init; }

    /// <summary>What it should ask for. At least one of the four.</summary>
    public required SearchCriteriaContract Criteria { get; init; }
}

/// <summary>Renames a saved search, and rewrites what it asks for.</summary>
/// <remarks>Both together, as one gesture, so half of it cannot fail.</remarks>
public sealed record UpdateSavedSearchRequest
{
    /// <summary>The new name.</summary>
    public required string Name { get; init; }

    /// <summary>What it should now ask for.</summary>
    public required SearchCriteriaContract Criteria { get; init; }
}
