namespace Contracts.Cookbooks;

/// <summary>A page of a household's cookbooks, wrapped so paging metadata can grow.</summary>
public sealed record CookbooksResponse
{
    /// <summary>The cookbooks on this page, most recently changed first.</summary>
    public required IReadOnlyList<CookbookSummary> Items { get; init; }

    /// <summary>Pass this back as <c>cursor</c> for the next page, or null at the end.</summary>
    public string? NextCursor { get; init; }

    /// <summary>How many cookbooks the household has, across all pages.</summary>
    public required int Total { get; init; }
}

/// <summary>A cookbook as it appears on a shelf. Its recipes are read through <c>GET /recipes?cookbookId=…</c>.</summary>
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

    /// <summary>Up to four photographed recipes for the cover, oldest first so the cover stops moving.</summary>
    /// <remarks>Recipes, not images: a recipe's picture is served from the recipe's own address.</remarks>
    public required IReadOnlyList<Guid> CoverRecipeIds { get; init; }

    /// <summary>The same pictures, with each recipe's current image id so a replaced picture gets a new address.</summary>
    public required IReadOnlyList<CookbookCoverPicture> CoverPictures { get; init; }

    /// <summary>When it, or what is on it, last changed.</summary>
    public required DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>One picture on a cookbook's cover.</summary>
public sealed record CookbookCoverPicture
{
    /// <summary>Whose picture.</summary>
    public required Guid RecipeId { get; init; }

    /// <summary>Which picture the recipe has now.</summary>
    public required Guid ImageId { get; init; }
}


/// <summary>What a cookbook that fills itself asks for. Every rule must hold.</summary>
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

    /// <summary>The same pictures, with which picture each recipe has now.</summary>
    public required IReadOnlyList<CookbookCoverPicture> CoverPictures { get; init; }

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

    /// <summary>What it should ask for. Supplying it makes a smart cookbook, omitting it a manual one; this cannot change later.</summary>
    public CookbookRulesContract? Rules { get; init; }
}

/// <summary>Renames a cookbook, and rewrites what it is for. Both fields together: they are one form.</summary>
public sealed record UpdateCookbookRequest
{
    /// <summary>The new name.</summary>
    public required string Name { get; init; }

    /// <summary>The new description, or null to clear it.</summary>
    public string? Description { get; init; }

    /// <summary>What it should now ask for. Required for a smart cookbook, refused for a manual one.</summary>
    public CookbookRulesContract? Rules { get; init; }
}

/// <summary>Which recipes are on a cookbook, by id. Not paged: it answers "is it already on?" for a whole picker.</summary>
public sealed record CookbookRecipesResponse
{
    /// <summary>Every recipe on it.</summary>
    public required IReadOnlyList<Guid> RecipeIds { get; init; }
}

/// <summary>Which cookbooks a recipe is on. Not paged: a recipe is on a handful of shelves.</summary>
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
