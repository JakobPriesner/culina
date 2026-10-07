using Domain.Planning;
using Domain.Suggestions;

namespace Application.Abstractions;

/// <summary>Why we are asking, and what we already know about the occasion.</summary>
/// <remarks>
/// Supplied per request, never stored on a recipe: meal type is a fact about the occasion, not the food.
/// Everything except who is asking and whose kitchen is optional; an empty context leans on history and freshness.
/// </remarks>
/// <param name="HouseholdId">Whose recipes.</param>
/// <param name="UserId">Who is asking. Affinity, dismissals and content taste are all theirs.</param>
/// <param name="Purpose">Which preset: how many, how varied, and what the ranking leans on.</param>
/// <param name="AsOf">The day being ranked for, truncated to a date so every decayed term, and so the list, is stable all day without a cache.</param>
/// <param name="Slot">Which meal, when the caller knows. The plan always does.</param>
/// <param name="MaxMinutes">A ceiling on total time. A hard filter, never a preference.</param>
/// <param name="Tags">Tag slugs a recipe must all carry.</param>
/// <param name="Ingredients">Ingredients to rank by, as the recipe search means it.</param>
/// <param name="LikeRecipeId">The recipe to resemble, for <see cref="SuggestionPurpose.Like"/>.</param>
/// <param name="Exclude">Recipes already on screen or planned. The caller's business: the ranker does not guess what is visible.</param>
/// <param name="Count">How many to return.</param>
public sealed record SuggestionContext(
    Guid HouseholdId,
    Guid UserId,
    SuggestionPurpose Purpose,
    DateTimeOffset AsOf,
    MealSlot? Slot,
    int? MaxMinutes,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Ingredients,
    Guid? LikeRecipeId,
    IReadOnlyList<Guid> Exclude,
    int Count)
{
    /// <summary>The households whose recipes this one inherits, ranked with its own. Only candidates widen; history is this kitchen's alone.</summary>
    public IReadOnlyList<Guid> InheritedFrom { get; init; } = [];

    /// <summary>The most any one screen may ask for; beyond it use <c>GET /recipes?sort=suggested</c>.</summary>
    public const int MaxCount = 12;

    /// <summary>What a screen gets when it does not say.</summary>
    public const int DefaultCount = 5;

    /// <summary>How many candidates to score before choosing: eight times the count, floored, so the diversity pass has something to swap in.</summary>
    public int PoolSize => Purpose == SuggestionPurpose.Browse ? Count : Math.Max(Count * 8, 40);

    /// <summary>Whether neighbouring results get pushed apart. Never while paging: a reordered set cannot be resumed from a cursor.</summary>
    public bool Diversify => Purpose != SuggestionPurpose.Browse;
}
