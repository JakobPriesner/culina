namespace Contracts.Recipes.GetShared;

/// <summary>A recipe as whoever follows the link sees it.</summary>
/// <remarks>
/// Its own type, not <see cref="RecipeDetail"/>: a stranger learns the recipe and nothing about who
/// keeps it (no household id, author or version), but gets everything the page draws, ingredient
/// references included, so amounts still scale.
/// </remarks>
public sealed record Response
{
    /// <summary>What it is called.</summary>
    public required string Title { get; init; }

    /// <summary>A short introduction, if there is one.</summary>
    public string? Description { get; init; }

    /// <summary>The language the title and steps are written in.</summary>
    public required string Language { get; init; }

    /// <summary>How many it makes.</summary>
    public required decimal YieldAmount { get; init; }

    /// <summary><c>servings</c> or <c>pieces</c>.</summary>
    public required string YieldKind { get; init; }

    /// <summary>The recipe's own word for what it makes, shown exactly as written.</summary>
    public string? YieldLabel { get; init; }

    /// <summary>Hands-on time.</summary>
    public int? PrepMinutes { get; init; }

    /// <summary>Time in the oven or on the hob.</summary>
    public int? CookMinutes { get; init; }

    /// <summary>Prep plus cook, or null when neither is known.</summary>
    public int? TotalMinutes { get; init; }

    /// <summary>
    /// Whether there is a photograph to fetch; a flag, not an id, since the picture is served under
    /// the same token and an id would reveal household storage.
    /// </summary>
    public required bool HasImage { get; init; }

    /// <summary>Its ingredient groups, in order.</summary>
    public required IReadOnlyList<IngredientGroupContract> Groups { get; init; }

    /// <summary>Its steps, in order.</summary>
    public required IReadOnlyList<StepContract> Steps { get; init; }

    /// <summary>Its tags.</summary>
    public required IReadOnlyList<string> Tags { get; init; }

    /// <summary>The address it was imported from, when it was.</summary>
    /// <remarks>
    /// Only the address, not the whole <see cref="RecipeProvenance"/>: library and fetch time are
    /// household setup, but where it was first published is a credit the page should carry.
    /// </remarks>
    public string? SourceUrl { get; init; }
}
