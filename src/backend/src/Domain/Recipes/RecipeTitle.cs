using Domain.Shared;

namespace Domain.Recipes;

/// <summary>
/// What a recipe is called.
/// </summary>
/// <remarks>
/// The only thing a recipe actually requires. A recipe with nothing but a title
/// is valid, because it is the placeholder for "I want to write this down
/// later" — and a create form that demands more than this is a form people
/// abandon.
/// </remarks>
public sealed record RecipeTitle
{
    /// <summary>The longest title the database column accepts.</summary>
    public const int MaxLength = 200;

    private RecipeTitle(string value) => Value = value;

    /// <summary>The trimmed title.</summary>
    public string Value { get; }

    /// <summary>Parses a title, returning a failure rather than throwing.</summary>
    /// <param name="value">What the caller supplied.</param>
    public static Result<RecipeTitle> Create(string? value)
    {
        var trimmed = value?.Trim();

        return string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxLength
            ? RecipeErrors.InvalidTitle
            : new RecipeTitle(trimmed);
    }

    /// <inheritdoc/>
    public override string ToString() => Value;
}
