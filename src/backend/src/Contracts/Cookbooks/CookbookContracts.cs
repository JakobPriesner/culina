namespace Contracts.Cookbooks;

/// <summary>A page of a household's cookbooks.</summary>
/// <remarks>
/// Wrapped, never a bare array, for the same reason every other collection here
/// is: an array has nowhere to grow paging metadata, and adding it later would
/// break every client.
/// </remarks>
public sealed record CookbooksResponse
{
    /// <summary>The cookbooks on this page, most recently changed first.</summary>
    public required IReadOnlyList<CookbookSummary> Items { get; init; }

    /// <summary>Pass this back as <c>cursor</c> for the next page, or null at the end.</summary>
    public string? NextCursor { get; init; }

    /// <summary>How many cookbooks the household has, across all pages.</summary>
    public required int Total { get; init; }
}

/// <summary>
/// A cookbook as it appears on a shelf.
/// </summary>
/// <remarks>
/// Carries what the card draws and nothing else. The recipes themselves are
/// read through <c>GET /recipes?cookbookId=…</c>, which is what gives a
/// cookbook search, filters and paging without a second implementation of any
/// of them.
/// </remarks>
public sealed record CookbookSummary
{
    /// <summary>The cookbook's id.</summary>
    public required Guid CookbookId { get; init; }

    /// <summary>What it is called.</summary>
    public required string Name { get; init; }

    /// <summary>What it is for, if whoever made it said.</summary>
    public string? Description { get; init; }

    /// <summary><c>manual</c> or <c>smart</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>What it asks for, when it fills itself.</summary>
    public CookbookRulesContract? Rules { get; init; }

    /// <summary>How many recipes are on it.</summary>
    public required int RecipeCount { get; init; }

    /// <summary>
    /// Up to four photographed recipes for the cover, oldest first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Recipes and not images, because a recipe's picture is served from the
    /// recipe's own address — there is no route that takes an image id.
    /// </para>
    /// <para>
    /// Oldest first, and not newest, so a cover stops moving once four
    /// photographed recipes are on the shelf. A face that changed every time
    /// something was added is not one anybody would learn to recognise.
    /// </para>
    /// </remarks>
    public required IReadOnlyList<Guid> CoverRecipeIds { get; init; }

    /// <summary>When it, or what is on it, last changed.</summary>
    public required DateTimeOffset UpdatedAt { get; init; }
}


/// <summary>
/// What a cookbook that fills itself asks for.
/// </summary>
/// <remarks>
/// Every rule must hold. A shelf asking for chicken and a main course means
/// both, because the shelves worth having are the narrow ones.
/// </remarks>
public sealed record CookbookRulesContract
{
    /// <summary>Tag slugs a recipe must all carry.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Ingredient names a recipe must all use. Matched as substrings.</summary>
    public IReadOnlyList<string> Ingredients { get; init; } = [];

    /// <summary>The longest a recipe may take, or omit for any length.</summary>
    public int? MaxMinutes { get; init; }
}

/// <summary>One cookbook, with everything its own page needs.</summary>
public sealed record CookbookDetail
{
    /// <summary>The cookbook's id.</summary>
    public required Guid CookbookId { get; init; }

    /// <summary>Which household owns it.</summary>
    public required Guid HouseholdId { get; init; }

    /// <summary>What it is called.</summary>
    public required string Name { get; init; }

    /// <summary>What it is for, if whoever made it said.</summary>
    public string? Description { get; init; }

    /// <summary><c>manual</c> or <c>smart</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>What it asks for, when it fills itself.</summary>
    public CookbookRulesContract? Rules { get; init; }

    /// <summary>How many recipes are on it.</summary>
    public required int RecipeCount { get; init; }

    /// <summary>Up to four photographed recipes for the cover, oldest first.</summary>
    public required IReadOnlyList<Guid> CoverRecipeIds { get; init; }

    /// <summary>Whose idea it was.</summary>
    public required Guid CreatedBy { get; init; }

    /// <summary>When it was made.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>When it, or what is on it, last changed.</summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Bumped by every write. This is the ETag.</summary>
    public required long Version { get; init; }
}

/// <summary>Starts a cookbook.</summary>
public sealed record CreateCookbookRequest
{
    /// <summary>Whose shelf it goes on.</summary>
    public required Guid HouseholdId { get; init; }

    /// <summary>What to call it. The only thing required.</summary>
    public required string Name { get; init; }

    /// <summary>What it is for, or omit.</summary>
    public string? Description { get; init; }

    /// <summary>
    /// What it should ask for.
    /// </summary>
    /// <remarks>
    /// Supplying this makes a cookbook that fills itself; omitting it makes one
    /// you put recipes on yourself. There is no third setting, and the choice
    /// cannot be changed afterwards — the two answer "why is this recipe here?"
    /// differently, and a shelf that was both could not answer at all.
    /// </remarks>
    public CookbookRulesContract? Rules { get; init; }
}

/// <summary>Renames a cookbook, and rewrites what it is for.</summary>
/// <remarks>
/// Both fields together rather than one patch per field: they are edited in one
/// form, and two requests for one form is two ways for half of it to fail.
/// </remarks>
public sealed record UpdateCookbookRequest
{
    /// <summary>The new name.</summary>
    public required string Name { get; init; }

    /// <summary>The new description, or null to clear it.</summary>
    public string? Description { get; init; }

    /// <summary>
    /// What it should now ask for. Required for a cookbook that fills itself,
    /// refused for one you fill yourself.
    /// </summary>
    public CookbookRulesContract? Rules { get; init; }
}

/// <summary>Which cookbooks a recipe is on.</summary>
/// <remarks>
/// Not paged. A recipe is on a handful of shelves or none, and this answers the
/// tick marks in a sheet — a cursor would be machinery for a list that fits on
/// one screen.
/// </remarks>
public sealed record RecipeCookbooksResponse
{
    /// <summary>The cookbooks containing it, by name.</summary>
    public required IReadOnlyList<RecipeCookbook> Items { get; init; }
}

/// <summary>One cookbook a recipe is on.</summary>
public sealed record RecipeCookbook
{
    /// <summary>The cookbook's id.</summary>
    public required Guid CookbookId { get; init; }

    /// <summary>What it is called.</summary>
    public required string Name { get; init; }
}
