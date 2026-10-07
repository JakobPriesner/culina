using Application.Abstractions;

namespace Infrastructure.Persistence.Recipes;

/// <summary>What a recipe card shows, shared by search, suggestions and related recipes; each query adds its ranking columns.</summary>
internal record RecipeCardRow
{
    public Guid RecipeId { get; init; }

    public Guid HouseholdId { get; init; }

    public string Title { get; init; } = string.Empty;

    public Guid? ImageId { get; init; }

    public int? TotalMinutes { get; init; }

    public decimal YieldAmount { get; init; }

    public string YieldKind { get; init; } = "servings";

    public string? YieldLabel { get; init; }

    public string[] Tags { get; init; } = [];

    public int CookCount { get; init; }

    public DateTimeOffset? LastCookedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Zero for a query that does not ask about ingredients.</summary>
    public int MatchedIngredients { get; init; }

    public int IngredientCount { get; init; }

    /// <summary>Set only by a search inside a cookbook.</summary>
    public DateTimeOffset? AddedToCookbookAt { get; init; }

    internal RecipeSearchRow ToSearchRow() => new(
        RecipeId,
        HouseholdId,
        Title,
        ImageId,
        TotalMinutes,
        YieldAmount,
        YieldKind,
        YieldLabel,
        Tags,
        CookCount,
        LastCookedAt,
        UpdatedAt,
        MatchedIngredients,
        IngredientCount,
        AddedToCookbookAt);
}
