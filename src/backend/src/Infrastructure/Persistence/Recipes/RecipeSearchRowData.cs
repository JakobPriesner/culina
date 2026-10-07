using Application.Abstractions;

namespace Infrastructure.Persistence.Recipes;

/// <summary>One row of the search projection as PostgreSQL returns it.</summary>
internal sealed record RecipeSearchRowData
{
    public Guid Id { get; init; }

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

    /// <summary>Zero for every sort but <see cref="RecipeSort.Suggested"/>.</summary>
    public decimal SuggestionScore { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public int MatchedIngredients { get; init; }

    public int IngredientCount { get; init; }

    public int TotalCount { get; init; }

    public DateTimeOffset? AddedToCookbookAt { get; init; }

    public string Language { get; init; } = "de";

    /// <summary>Why a row that is not a title match is here; see <see cref="RecipeSearcher"/>.</summary>
    public string? ReasonKind { get; init; }

    public string? ReasonTerm { get; init; }

    /// <summary>Whether the title or a tag says the diet asked for.</summary>
    public bool DietAsserted { get; init; }

    /// <summary>Which kind of evidence put this row here. Lower is stronger.</summary>
    public int Tier { get; init; }

    /// <summary>How well it fits, within its tier.</summary>
    public double Score { get; init; }
}
