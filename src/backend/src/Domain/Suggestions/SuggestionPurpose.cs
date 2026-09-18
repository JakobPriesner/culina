namespace Domain.Suggestions;

/// <summary>
/// Why we are being asked, which decides how many, how varied and how
/// adventurous the answer is.
/// </summary>
/// <remarks>
/// <para>
/// Three, and there is a standing reason not to add a fourth. Every
/// screen-specific behaviour — breakfast, a quick meal, a Thursday, a shelf —
/// is expressed by the other fields of the context, never by a member here. A
/// purpose per screen is how a suggestion API turns into a dozen hard-coded
/// modes that each have to be maintained separately and drift apart.
/// </para>
/// <para>
/// A purpose selects a preset and nothing else: how big a pool to score, whether
/// neighbouring results are pushed apart, and whether the ranking leans on who
/// is asking or on what they are looking at.
/// </para>
/// </remarks>
public enum SuggestionPurpose
{
    /// <summary>
    /// Rank the whole library. Paged, so nothing may reorder between pages:
    /// no diversity pass and no exploration.
    /// </summary>
    Browse = 0,

    /// <summary>
    /// A small set to choose dinner from. Bounded, pushed apart, explained.
    /// </summary>
    Decide = 1,

    /// <summary>
    /// Recipes like one particular recipe. Personal taste still applies, but
    /// quietly: the question is about the recipe on screen, not about you.
    /// </summary>
    Like = 2
}
