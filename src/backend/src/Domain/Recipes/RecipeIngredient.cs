using Domain.Shared;

namespace Domain.Recipes;

/// <summary>
/// One line of a recipe's ingredient list.
/// </summary>
/// <remarks>
/// <see cref="Note"/> is separate from <see cref="Name"/> on purpose. "butter,
/// finely chopped" and "butter" have to merge into one shopping-list line, and
/// they never will if the preparation lives in the name.
/// </remarks>
public sealed class RecipeIngredient
{
    /// <summary>The longest ingredient name the database column accepts.</summary>
    public const int MaxNameLength = 120;

    /// <summary>The longest preparation note the database column accepts.</summary>
    public const int MaxNoteLength = 200;

    private RecipeIngredient(Guid id, int sortOrder, Quantity quantity, string name, string? note)
    {
        Id = id;
        SortOrder = sortOrder;
        Quantity = quantity;
        Name = name;
        Note = note;
    }

    /// <summary>The ingredient's id, which steps refer to.</summary>
    public Guid Id { get; }

    /// <summary>Where it appears in its group.</summary>
    public int SortOrder { get; }

    /// <summary>How much, which may be unstated.</summary>
    public Quantity Quantity { get; }

    /// <summary>The shoppable noun: "butter".</summary>
    public string Name { get; }

    /// <summary>The preparation: "finely chopped", "at room temperature".</summary>
    public string? Note { get; }

    /// <summary>Creates an ingredient line.</summary>
    /// <param name="id">Its id, or null for a new one.</param>
    /// <param name="sortOrder">Where it appears.</param>
    /// <param name="quantity">How much.</param>
    /// <param name="name">The shoppable noun.</param>
    /// <param name="note">The preparation.</param>
    public static Result<RecipeIngredient> Create(
        Guid? id,
        int sortOrder,
        Quantity quantity,
        string? name,
        string? note)
    {
        ArgumentNullException.ThrowIfNull(quantity);

        var trimmedName = name?.Trim();
        var trimmedNote = note?.Trim();

        if (string.IsNullOrEmpty(trimmedName) || trimmedName.Length > MaxNameLength)
        {
            return RecipeErrors.InvalidIngredientName;
        }

        if (trimmedNote?.Length > MaxNoteLength)
        {
            return RecipeErrors.InvalidIngredientName;
        }

        return new RecipeIngredient(
            id ?? CulinaId.New(),
            sortOrder,
            quantity,
            trimmedName,
            string.IsNullOrEmpty(trimmedNote) ? null : trimmedNote);
    }
}
