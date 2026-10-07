using Domain.Shared;

namespace Domain.Cooking;

/// <summary>What one person thinks about a recipe or one of its steps.</summary>
/// <remarks>Person-owned, never household-owned: a note is not an edit, and the recipe stays canonical. A product decision; do not turn it into a recipe column.</remarks>
public sealed class PersonalNote
{
    /// <summary>The longest note the database column accepts.</summary>
    public const int MaxLength = 2000;

    private PersonalNote(
        Guid id,
        Guid recipeId,
        Guid userId,
        Guid? stepId,
        string body,
        DateTimeOffset updatedAt)
    {
        Id = id;
        RecipeId = recipeId;
        UserId = userId;
        StepId = stepId;
        Body = body;
        UpdatedAt = updatedAt;
    }

    /// <summary>The note's id.</summary>
    public Guid Id { get; }

    /// <summary>Which recipe it is about.</summary>
    public Guid RecipeId { get; }

    /// <summary>Whose note it is.</summary>
    public Guid UserId { get; }

    /// <summary>Which step it is about, or null for the recipe as a whole.</summary>
    public Guid? StepId { get; }

    /// <summary>What it says.</summary>
    public string Body { get; }

    /// <summary>When it was last written.</summary>
    public DateTimeOffset UpdatedAt { get; }

    /// <summary>Writes a note.</summary>
    public static Result<PersonalNote> Write(
        Guid recipeId,
        Guid userId,
        Guid? stepId,
        string? body,
        DateTimeOffset now)
    {
        var trimmed = body?.Trim();

        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxLength)
        {
            return CookingErrors.InvalidNote;
        }

        return new PersonalNote(CulinaId.New(), recipeId, userId, stepId, trimmed, now);
    }

    /// <summary>Rebuilds a note from storage.</summary>
    public static PersonalNote Restore(
        Guid id,
        Guid recipeId,
        Guid userId,
        Guid? stepId,
        string body,
        DateTimeOffset updatedAt) =>
        new(id, recipeId, userId, stepId, body, updatedAt);
}
