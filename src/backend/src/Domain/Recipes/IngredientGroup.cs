using Domain.Shared;

namespace Domain.Recipes;

/// <summary>
/// A named part of the ingredient list: "For the dough", "For the sauce".
/// </summary>
/// <remarks>
/// Every recipe has one group whose name is null. That is what makes grouping
/// invisible until it is used: a recipe with one unnamed group renders as a
/// plain list, and the UI shows no grouping affordance until a second group
/// exists.
/// </remarks>
public sealed class IngredientGroup
{
    /// <summary>The longest group name the database column accepts.</summary>
    public const int MaxNameLength = 80;

    private IngredientGroup(Guid id, string? name, int sortOrder, IReadOnlyList<RecipeIngredient> ingredients)
    {
        Id = id;
        Name = name;
        SortOrder = sortOrder;
        Ingredients = ingredients;
    }

    /// <summary>The group's id.</summary>
    public Guid Id { get; }

    /// <summary>Its heading, or null for the implicit first group.</summary>
    public string? Name { get; }

    /// <summary>Where it appears.</summary>
    public int SortOrder { get; }

    /// <summary>Its ingredient lines, in order.</summary>
    public IReadOnlyList<RecipeIngredient> Ingredients { get; }

    /// <summary>Creates a group.</summary>
    /// <param name="id">Its id, or null for a new one.</param>
    /// <param name="name">Its heading, or null.</param>
    /// <param name="sortOrder">Where it appears.</param>
    /// <param name="ingredients">Its ingredient lines.</param>
    public static Result<IngredientGroup> Create(
        Guid? id,
        string? name,
        int sortOrder,
        IReadOnlyList<RecipeIngredient> ingredients)
    {
        ArgumentNullException.ThrowIfNull(ingredients);

        var trimmed = name?.Trim();

        if (trimmed?.Length > MaxNameLength)
        {
            return RecipeErrors.InvalidIngredientName;
        }

        return new IngredientGroup(
            id ?? CulinaId.New(),
            string.IsNullOrEmpty(trimmed) ? null : trimmed,
            sortOrder,
            ingredients);
    }

    /// <summary>The empty group every new recipe starts with.</summary>
    public static IngredientGroup Implicit() =>
        new(CulinaId.New(), name: null, sortOrder: 0, ingredients: []);
}
