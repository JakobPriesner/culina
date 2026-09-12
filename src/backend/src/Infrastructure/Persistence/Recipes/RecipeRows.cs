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

    public string Body { get; init; } = string.Empty;

    public int? DurationSeconds { get; init; }
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

    internal static string? Of(Unit? unit) => unit switch
    {
        null => null,
        Unit.Gram => "g",
        Unit.Kilogram => "kg",
        Unit.Millilitre => "ml",
        Unit.Litre => "l",
        Unit.Teaspoon => "tsp",
        Unit.Tablespoon => "tbsp",
        Unit.Piece => "piece",
        Unit.Clove => "clove",
        Unit.Bunch => "bunch",
        Unit.Slice => "slice",
        Unit.Can => "can",
        Unit.Pack => "pack",
        Unit.Pinch => "pinch",
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown unit.")
    };

    internal static Unit? ToUnit(string? stored) => stored switch
    {
        null or "" => null,
        "g" => Unit.Gram,
        "kg" => Unit.Kilogram,
        "ml" => Unit.Millilitre,
        "l" => Unit.Litre,
        "tsp" => Unit.Teaspoon,
        "tbsp" => Unit.Tablespoon,
        "piece" => Unit.Piece,
        "clove" => Unit.Clove,
        "bunch" => Unit.Bunch,
        "slice" => Unit.Slice,
        "can" => Unit.Can,
        "pack" => Unit.Pack,
        "pinch" => Unit.Pinch,
        // A row written by a newer version: treated as unmeasured rather than
        // crashing a read, because a recipe that mostly renders beats an error.
        _ => null
    };
}
