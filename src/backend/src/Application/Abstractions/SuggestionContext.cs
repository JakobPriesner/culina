using Domain.Planning;
using Domain.Suggestions;

namespace Application.Abstractions;

/// <summary>
/// Why we are asking, and what we already know about the occasion.
/// </summary>
/// <remarks>
/// <para>
/// Context is supplied per request and never stored on a recipe. A recipe has
/// no <c>mealType</c> column and should not get one: whether something is
/// breakfast is a fact about the occasion and about how this household has
/// planned it, not a property of the food. The same goes for the clock, the
/// month and the time available.
/// </para>
/// <para>
/// Everything here is optional except who is asking and whose kitchen it is.
/// A context that says nothing still produces a ranking — it simply leans on
/// history and freshness instead of on the occasion.
/// </para>
/// </remarks>
/// <param name="HouseholdId">Whose recipes.</param>
/// <param name="UserId">Who is asking. Affinity, dismissals and content taste are all theirs.</param>
/// <param name="Purpose">Which preset: how many, how varied, and what the ranking leans on.</param>
/// <param name="AsOf">
/// The day being ranked for, already truncated to a date.
/// <para>
/// Truncated deliberately, and it is what makes the whole feature feel settled:
/// every decayed term is a function of this, so two requests on the same day
/// score identically. The list is the same all evening, on both devices and
/// after a refresh, and different tomorrow — without a cache, and without a
/// shuffle button teaching people that the first answer was arbitrary.
/// </para>
/// </param>
/// <param name="Slot">Which meal, when the caller knows. The plan always does.</param>
/// <param name="MaxMinutes">A ceiling on total time. A hard filter, never a preference.</param>
/// <param name="Tags">Tag slugs a recipe must all carry.</param>
/// <param name="Ingredients">Ingredients to rank by, as the recipe search means it.</param>
/// <param name="LikeRecipeId">The recipe to resemble, for <see cref="SuggestionPurpose.Like"/>.</param>
/// <param name="Exclude">
/// Recipes the caller already has on screen or already planned. The caller's
/// own business: the ranker does not guess what is visible.
/// </param>
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
    /// <summary>
    /// The households whose recipes this one inherits, which are ranked with
    /// its own. Empty for a household that inherits nothing.
    /// </summary>
    /// <remarks>
    /// Only the candidates widen. What anybody did — cooked, planned,
    /// shelved — is still read from this household alone, because the
    /// history being ranked on is this kitchen's.
    /// </remarks>
    public IReadOnlyList<Guid> InheritedFrom { get; init; } = [];

    /// <summary>The most any one screen may ask for.</summary>
    /// <remarks>
    /// Twelve is already more than a person chooses between. Past that the
    /// honest answer is the recipe list, ranked — which is what
    /// <c>GET /recipes?sort=suggested</c> is for.
    /// </remarks>
    public const int MaxCount = 12;

    /// <summary>What a screen gets when it does not say.</summary>
    public const int DefaultCount = 5;

    /// <summary>
    /// How many candidates to score before choosing, so the diversity pass has
    /// something to choose between.
    /// </summary>
    /// <remarks>
    /// Eight times what was asked for, floored at a pool that is worth having.
    /// A greedy pass over a pool the size of the answer cannot push anything
    /// apart, because there is nothing left to swap in.
    /// </remarks>
    public int PoolSize => Purpose == SuggestionPurpose.Browse ? Count : Math.Max(Count * 8, 40);

    /// <summary>Whether neighbouring results get pushed apart.</summary>
    /// <remarks>
    /// Never while paging. A diversity pass reorders the whole set, and a
    /// reordered set cannot be resumed from a cursor without repeating a row or
    /// losing one.
    /// </remarks>
    public bool Diversify => Purpose != SuggestionPurpose.Browse;
}
