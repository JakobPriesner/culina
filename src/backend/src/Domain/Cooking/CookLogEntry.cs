using Domain.Shared;

namespace Domain.Cooking;

/// <summary>A record that someone cooked this: one tap with a date. Culina has no star ratings; what was cooked is the better signal.</summary>
public sealed class CookLogEntry
{
    /// <summary>The longest note an entry may carry.</summary>
    public const int MaxNoteLength = 500;

    private CookLogEntry(
        Guid id,
        Guid recipeId,
        Guid userId,
        Guid householdId,
        DateTimeOffset madeAt,
        decimal? servings,
        string? note,
        CookPhoto? photo)
    {
        Id = id;
        RecipeId = recipeId;
        UserId = userId;
        HouseholdId = householdId;
        MadeAt = madeAt;
        Servings = servings;
        Note = note;
        Photo = photo;
    }

    /// <summary>The entry's id.</summary>
    public Guid Id { get; }

    /// <summary>Which recipe was cooked.</summary>
    public Guid RecipeId { get; }

    /// <summary>Who cooked it.</summary>
    public Guid UserId { get; }

    /// <summary>Which household it belonged to at the time.</summary>
    public Guid HouseholdId { get; }

    /// <summary>When it was cooked.</summary>
    public DateTimeOffset MadeAt { get; }

    /// <summary>How much was made, if the cook said.</summary>
    public decimal? Servings { get; }

    /// <summary>Anything they wanted to remember.</summary>
    public string? Note { get; }

    /// <summary>A picture of how it turned out, if they took one. Theirs, not the recipe's own photograph.</summary>
    public CookPhoto? Photo { get; private set; }

    /// <summary>Hangs a photo on this attempt, replacing any it had.</summary>
    /// <param name="photo">What was stored.</param>
    public void Illustrate(CookPhoto? photo) => Photo = photo;

    /// <summary>Records that someone cooked this.</summary>
    /// <param name="recipeId">Which recipe.</param>
    /// <param name="userId">Who cooked it.</param>
    /// <param name="householdId">Which household it belongs to.</param>
    /// <param name="madeAt">When, defaulting to now.</param>
    /// <param name="servings">How much was made.</param>
    /// <param name="note">Anything worth remembering.</param>
    public static Result<CookLogEntry> Record(
        Guid recipeId,
        Guid userId,
        Guid householdId,
        DateTimeOffset madeAt,
        decimal? servings,
        string? note)
    {
        var trimmed = note?.Trim();

        if (trimmed?.Length > MaxNoteLength)
        {
            return CookingErrors.InvalidNote;
        }

        if (servings is <= 0 or > 1000)
        {
            return CookingErrors.InvalidServings;
        }

        return new CookLogEntry(
            CulinaId.New(),
            recipeId,
            userId,
            householdId,
            madeAt,
            servings,
            string.IsNullOrEmpty(trimmed) ? null : trimmed,
            photo: null);
    }

    /// <summary>Rebuilds an entry from storage.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="recipeId">Which recipe.</param>
    /// <param name="userId">Who cooked it.</param>
    /// <param name="householdId">Which household.</param>
    /// <param name="madeAt">When.</param>
    /// <param name="servings">How much.</param>
    /// <param name="note">What they wrote.</param>
    /// <param name="photo">The picture they took, if any.</param>
    public static CookLogEntry Restore(
        Guid id,
        Guid recipeId,
        Guid userId,
        Guid householdId,
        DateTimeOffset madeAt,
        decimal? servings,
        string? note,
        CookPhoto? photo = null) =>
        new(id, recipeId, userId, householdId, madeAt, servings, note, photo);
}

/// <summary>A picture of one attempt.</summary>
/// <param name="ContentHash">Where the bytes are, in the image store.</param>
/// <param name="Width">The stored width, for reserving the space it will take.</param>
/// <param name="Height">The stored height.</param>
public sealed record CookPhoto(string ContentHash, int Width, int Height);
