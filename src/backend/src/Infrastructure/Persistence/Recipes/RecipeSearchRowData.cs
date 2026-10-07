using Application.Abstractions;

namespace Infrastructure.Persistence.Recipes;

/// <summary>One row of the search projection as PostgreSQL returns it.</summary>
internal sealed record RecipeSearchRowData : RecipeCardRow
{
    /// <summary>The search selects the recipe as <c>id</c>.</summary>
    public Guid Id
    {
        get => RecipeId;
        init => RecipeId = value;
    }

    /// <summary>Zero for every sort but <see cref="RecipeSort.Suggested"/>.</summary>
    public decimal SuggestionScore { get; init; }

    public int TotalCount { get; init; }

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
