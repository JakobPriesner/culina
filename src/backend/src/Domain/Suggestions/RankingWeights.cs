namespace Domain.Suggestions;

/// <summary>How much each term of the score is worth.</summary>
/// <remarks>
/// The numbers solve a stated set of ordering rules (each a rule in <c>RankingRules</c>); the rules
/// are the specification, so a changed weight makes a test name the promise it broke. Only the
/// ordering means anything: never show a score to a user.
/// </remarks>
public sealed record RankingWeights
{
    /// <summary>What the weights are when nobody has said otherwise.</summary>
    public static readonly RankingWeights Default = new();

    /// <summary>
    /// How much this person's own history with this exact recipe counts; the unit every other
    /// weight is measured against.
    /// </summary>
    public decimal Affinity { get; init; } = 1.00m;

    /// <summary>
    /// How much liking recipes <i>like</i> this one counts. Below <see cref="Affinity"/> on
    /// purpose; it carries recipes nobody has touched yet.
    /// </summary>
    public decimal Content { get; init; } = 0.80m;

    /// <summary>How hard to push down something the household ate recently.</summary>
    /// <remarks>
    /// Must beat the largest affinity a recipe reaches (about 2.1 for a weekly staple), or a
    /// favourite leads the list the morning after; 2.2 is what <c>Rule2b_…</c> requires. About −2.2
    /// the same day, −1.1 after a week, nothing after a month.
    /// </remarks>
    public decimal Repetition { get; init; } = 2.20m;

    /// <summary>How hard to lift something they liked and have not made for months.</summary>
    public decimal Rediscovery { get; init; } = 0.60m;

    /// <summary>
    /// How much being a breakfast or a dinner recipe counts, when a slot was named.
    /// </summary>
    public decimal Slot { get; init; } = 0.50m;

    /// <summary>How much a weekday dislikes a project. Reversed, gently, at the weekend.</summary>
    public decimal Effort { get; init; } = 0.30m;

    /// <summary>
    /// How much the rest of the household counts. Small, so two people's tastes are not flattened
    /// into one household average.
    /// </summary>
    public decimal Household { get; init; } = 0.35m;

    /// <summary>How much never having been cooked counts.</summary>
    public decimal Novelty { get; init; } = 0.25m;

    /// <summary>
    /// How much having arrived recently counts, so a bulk import is not buried under recipes with
    /// history.
    /// </summary>
    public decimal Freshness { get; init; } = 0.30m;

    /// <summary>
    /// How much this household's own cooking calendar counts; zero until there is evidence
    /// (<see cref="SeasonMinimumObservations"/>).
    /// </summary>
    public decimal Season { get; init; } = 0.40m;

    /// <summary>
    /// How far a recipe may drift for variety: about the gap between neighbours, so it reshuffles
    /// near-equals and never promotes a loser.
    /// </summary>
    public decimal Exploration { get; init; } = 0.15m;

    /// <summary>
    /// How much similarity to the recipe on screen counts, for
    /// <see cref="SuggestionPurpose.Like"/>.
    /// </summary>
    /// <remarks>
    /// Must beat a household favourite's affinity (near 1.8), not match it, or exploration reorders
    /// the pair: at 1.5 <c>Rule11_CloseToThisOne</c> failed about four runs in ten.
    /// </remarks>
    public decimal Similarity { get; init; } = 2.20m;

    /// <summary>Days for a signal to be worth half of what it was.</summary>
    public int HalfLifeDays { get; init; } = 365;

    /// <summary>How fast the repetition penalty fades, in days.</summary>
    public int RepetitionDecayDays { get; init; } = 10;

    /// <summary>
    /// When rediscovery starts to count, in days since the household last cooked it.
    /// </summary>
    public int RediscoveryFromDays { get; init; } = 90;

    /// <summary>When rediscovery reaches its full value, in days.</summary>
    public int RediscoveryFullDays { get; init; } = 180;

    /// <summary>How long a new recipe stays new, in days. Its freshness halves over this.</summary>
    public int FreshnessHalfLifeDays { get; init; } = 30;

    /// <summary>
    /// How long "not tonight" lasts, in days, so hiding something once does not become hiding it
    /// forever.
    /// </summary>
    public int DismissalDays { get; init; } = 90;

    /// <summary>
    /// How many cook-log entries a tag needs before this household may claim a season for it.
    /// </summary>
    /// <remarks>
    /// Below it the term is zero and no seasonal reason is shown, so a fresh installation says
    /// nothing rather than something confidently wrong.
    /// </remarks>
    public int SeasonMinimumObservations { get; init; } = 12;

    /// <summary>
    /// How much of a tag's cooking must fall in this month to count as seasonal, as a multiple of
    /// an even spread (1.5 of one twelfth).
    /// </summary>
    public decimal SeasonMinimumShare { get; init; } = 1.5m / 12m;

    /// <summary>
    /// How far a candidate is pushed down for resembling one already chosen. Modest: variety that
    /// overrides preference is worse than similar things.
    /// </summary>
    public decimal DiversityPenalty { get; init; } = 0.35m;

    /// <summary>
    /// What share of a suggestion's positive score one term must carry before it may be shown as
    /// the reason.
    /// </summary>
    public decimal ReasonDominance { get; init; } = 0.35m;
}
