using Domain.Recipes;
using Domain.Shared;

namespace Infrastructure.Persistence.Recipes;

/// <summary>The <c>recipes</c> row as PostgreSQL returns it.</summary>
internal sealed record RecipeRow
{
    public Guid Id { get; init; }

    public Guid HouseholdId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string RecipeLanguage { get; init; } = "en";

    public decimal YieldAmount { get; init; }

    public string YieldKind { get; init; } = "servings";

    public string? YieldLabel { get; init; }

    public int? PrepMinutes { get; init; }

    public int? CookMinutes { get; init; }

    public Guid? ImageId { get; init; }

    public Guid CreatedBy { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public long Version { get; init; }
}

/// <summary>The <c>ingredient_groups</c> row.</summary>
internal sealed record IngredientGroupRow
{
    public Guid Id { get; init; }

    public Guid RecipeId { get; init; }

    public string? Name { get; init; }

    public int SortOrder { get; init; }
}

/// <summary>The <c>recipe_ingredients</c> row, joined to its recipe.</summary>
internal sealed record RecipeIngredientRow
{
    public Guid Id { get; init; }

    public Guid GroupId { get; init; }

    public int SortOrder { get; init; }

    public decimal? Quantity { get; init; }

    public string? Unit { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Note { get; init; }
}

/// <summary>The <c>steps</c> row.</summary>
internal sealed record StepRow
{
    public Guid Id { get; init; }

    public Guid RecipeId { get; init; }

    public int SortOrder { get; init; }

    public string? Title { get; init; }

    public string Body { get; init; } = string.Empty;

    public int? DurationSeconds { get; init; }
}

/// <summary>The <c>step_ingredient_refs</c> row: what one step needs.</summary>
internal sealed record StepIngredientRefRow
{
    public Guid StepId { get; init; }

    public Guid RecipeIngredientId { get; init; }
}

/// <summary>
/// The words each enum is stored as.
/// </summary>
/// <remarks>
/// Text rather than integers, so a database dump is readable and inserting an
/// enum member later cannot renumber existing rows.
/// </remarks>
internal static class RecipeCodes
{
    internal static string Of(Language language) => language == Language.De ? "de" : "en";

    internal static Language ToLanguage(string stored) => stored == "de" ? Language.De : Language.En;

    internal static string Of(YieldKind kind) => kind == YieldKind.Pieces ? "pieces" : "servings";

    internal static YieldKind ToYieldKind(string stored) =>
        stored == "pieces" ? YieldKind.Pieces : YieldKind.Servings;

    /// <summary>
    /// A unit is stored as the code it carries, so this is a passthrough. The
    /// table that used to be here existed only because the unit was an enum.
    /// </summary>
    internal static string? Of(Unit? unit) => unit?.Code;

    /// <summary>
    /// A stored unit, or null when the column is empty.
    /// </summary>
    /// <remarks>
    /// A unit that no longer parses is read as unmeasured rather than crashing
    /// the read, because a recipe that mostly renders beats an error.
    /// </remarks>
    internal static Unit? ToUnit(string? stored) =>
        string.IsNullOrEmpty(stored) ? null : Unit.Create(stored).Match<Unit?>(one => one, _ => null);
}
