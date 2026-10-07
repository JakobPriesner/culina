namespace Contracts.Recipes;

/// <summary>
/// A recipe in full.
/// </summary>
/// <remarks>One type for read, create and update, since all answer "what does this recipe look like now".</remarks>
public sealed record RecipeDetail
{
    /// <summary>The recipe's id.</summary>
    public required Guid RecipeId { get; init; }

    /// <summary>Which household owns it.</summary>
    public required Guid HouseholdId { get; init; }

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

    /// <summary>
    /// The recipe's own word for what it makes — "Cake", "Gläser", "Blech".
    /// </summary>
    /// <remarks>Null for nearly every recipe; shown exactly as written, never pluralised or translated.</remarks>
    public string? YieldLabel { get; init; }

    /// <summary>Hands-on time.</summary>
    public int? PrepMinutes { get; init; }

    /// <summary>Time in the oven or on the hob.</summary>
    public int? CookMinutes { get; init; }

    /// <summary>Prep plus cook, or null when neither is known.</summary>
    public int? TotalMinutes { get; init; }

    /// <summary>Its hero image, if it has one.</summary>
    public Guid? ImageId { get; init; }

    /// <summary>Its ingredient groups, in order.</summary>
    public required IReadOnlyList<IngredientGroupContract> Groups { get; init; }

    /// <summary>Its steps, in order.</summary>
    public required IReadOnlyList<StepContract> Steps { get; init; }

    /// <summary>Its tags.</summary>
    public required IReadOnlyList<string> Tags { get; init; }

    /// <summary>
    /// Where it came from, when it was not written here.
    /// </summary>
    public RecipeProvenance? Origin { get; init; }

    /// <summary>Who wrote it down.</summary>
    public required Guid CreatedBy { get; init; }

    /// <summary>When it was written down.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>When it last changed.</summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>The entity version, for If-Match on an update.</summary>
    public required long Version { get; init; }
}
