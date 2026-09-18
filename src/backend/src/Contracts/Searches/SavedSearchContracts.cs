namespace Contracts.Searches;

/// <summary>A household's saved searches.</summary>
/// <remarks>
/// Wrapped, never a bare array, for the same reason every other collection here
/// is. Not paged: a kitchen keeps a handful of these and they are drawn as a
/// row of chips, so a cursor would be machinery for a list that fits on one
/// line.
/// </remarks>
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

/// <summary>
/// What a saved search asks the library for.
/// </summary>
/// <remarks>
/// The same four values <c>GET /recipes</c> takes as <c>query</c>, <c>tag</c>,
/// <c>maxMinutes</c> and <c>sort</c>, so applying one is assigning them rather
/// than translating anything.
/// </remarks>
public sealed record SearchCriteriaContract
{
    /// <summary>The words that were in the search box, or omit.</summary>
    public string? Query { get; init; }

    /// <summary>Tag slugs a recipe must all carry.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>The longest a recipe may take, or omit for any length.</summary>
    public int? MaxMinutes { get; init; }

    /// <summary>
    /// The order to read in, or omit for whatever the library would choose.
    /// </summary>
    /// <remarks>
    /// One of <c>relevance</c>, <c>suggested</c>, <c>-updatedAt</c>,
    /// <c>title</c>, <c>totalMinutes</c> or <c>-cookCount</c> — the same words
    /// <c>GET /recipes</c> accepts, minus <c>cookbookOrder</c>, which needs a
    /// cookbook to be an order of.
    /// </remarks>
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
/// <remarks>
/// Both together rather than one patch per field: renaming a search and
/// pointing it at what you are looking at now are the same gesture from the
/// same sheet, and two requests for one gesture is two ways for half of it to
/// fail.
/// </remarks>
public sealed record UpdateSavedSearchRequest
{
    /// <summary>The new name.</summary>
    public required string Name { get; init; }

    /// <summary>What it should now ask for.</summary>
    public required SearchCriteriaContract Criteria { get; init; }
}
