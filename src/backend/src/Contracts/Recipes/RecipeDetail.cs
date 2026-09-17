namespace Contracts.Recipes;

/// <summary>
/// A recipe in full.
/// </summary>
/// <remarks>
/// <para>
/// One type, returned by reading, creating and updating a recipe alike, because
/// all three answer the identical question: what does this recipe look like
/// now. Splitting them pre-emptively would be three copies of a twenty-field
/// projection with nothing to distinguish them.
/// </para>
/// <para>
/// The moment one of them needs a field the others must not have, it gets its
/// own type — the operation folders already exist for exactly that.
/// </para>
/// </remarks>
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
    /// <remarks>
    /// Null for most recipes, which is the ordinary case. It is part of the
    /// recipe rather than a second request because it is one line of text under
    /// a title, and a page that had to ask twice to draw one line would ask
    /// once and skip it.
    /// </remarks>
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
