using Domain.Shared;

namespace Domain.Cooking;

/// <summary>
/// A record that someone cooked this.
/// </summary>
/// <remarks>
/// One tap, with a date. Later it becomes "you've made this 7 times, last in
/// March", and it is why Culina has no star ratings: what someone actually
/// cooked is a better signal than what they once claimed to like, and a rating
/// nobody maintains is worse than none.
/// </remarks>
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
        string? note)
    {
        Id = id;
        RecipeId = recipeId;
        UserId = userId;
        HouseholdId = householdId;
        MadeAt = madeAt;
        Servings = servings;
        Note = note;
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
            string.IsNullOrEmpty(trimmed) ? null : trimmed);
    }

    /// <summary>Rebuilds an entry from storage.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="recipeId">Which recipe.</param>
    /// <param name="userId">Who cooked it.</param>
    /// <param name="householdId">Which household.</param>
    /// <param name="madeAt">When.</param>
    /// <param name="servings">How much.</param>
    /// <param name="note">What they wrote.</param>
    public static CookLogEntry Restore(
        Guid id,
        Guid recipeId,
        Guid userId,
        Guid householdId,
        DateTimeOffset madeAt,
        decimal? servings,
        string? note) =>
        new(id, recipeId, userId, householdId, madeAt, servings, note);
}
