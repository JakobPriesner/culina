namespace Domain.Suggestions;

/// <summary>
/// How much each term of the score is worth.
/// </summary>
/// <remarks>
/// <para>
/// <b>These numbers are not taste.</b> They are the solution to a stated set of
/// ordering rules, and the rules — not the numbers — are the specification.
/// Each one is an assertion in <c>RankingOrderTests</c>: a recipe cooked
/// yesterday never outranks the same recipe three weeks later; a never-cooked
/// recipe matching somebody's top tags beats a twice-cooked recipe matching
/// none; another member's weekly favourite reaches the top twenty and never the
/// top three. Change a weight and a test tells you which promise broke.
/// </para>
/// <para>
/// A record rather than constants so the calibration in
/// <c>docs/suggestions-research.md</c> §M.3 has something to search, and so a
/// test can hold nine weights still and sweep the tenth.
/// </para>
/// <para>
/// Every term is bounded, and the sum is in no unit at all — only the ordering
/// means anything. Do not show a score to a user.
/// </para>
/// </remarks>
public sealed record RankingWeights
{
    /// <summary>What the weights are when nobody has said otherwise.</summary>
    public static readonly RankingWeights Default = new();

    /// <summary>
    /// How much this person's own history with this exact recipe counts. The
    /// unit every other weight is measured against.
    /// </summary>
    public decimal Affinity { get; init; } = 1.00m;

    /// <summary>
    /// How much liking recipes <i>like</i> this one counts. Below
    /// <see cref="Affinity"/> on purpose: having cooked this beats having cooked
    /// things resembling it. This is the term that carries a recipe nobody has
    /// touched yet, and therefore the one that makes week one useful.
    /// </summary>
    public decimal Content { get; init; } = 0.80m;

    /// <summary>
    /// How hard to push down something the household ate recently.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It has to beat the largest affinity a recipe can reach, not merely equal
    /// it. Affinity is <c>log1p</c> of the decayed history, which passes 1.3
    /// after three cooks and settles around 2.1 for a weekly staple, so a weight
    /// of 1.2 — the obvious "a bit more than affinity" choice — leaves a
    /// favourite leading the list the morning after it was eaten.
    /// </para>
    /// <para>
    /// 2.2 was not guessed: it is what
    /// <c>Rule2b_AnUntouchedRecipeMatchingTheirTaste_ShouldBeatOneTheyAteYesterday</c>
    /// requires, and that test exists because it is the promise the product
    /// makes. Roughly −2.2 the same day, −1.1 after a week, −0.5 after a
    /// fortnight and nothing after a month: off the table tonight, itself again
    /// by the weekend.
    /// </para>
    /// </remarks>
    public decimal Repetition { get; init; } = 2.20m;

    /// <summary>
    /// How hard to lift something they liked and have not made for months. Big
    /// enough to beat a mediocre familiar recipe, small enough that the list is
    /// not archaeology.
    /// </summary>
    public decimal Rediscovery { get; init; } = 0.60m;

    /// <summary>How much being a breakfast or a dinner recipe counts, when a slot was named.</summary>
    public decimal Slot { get; init; } = 0.50m;

    /// <summary>How much a weekday dislikes a project. Reversed, gently, at the weekend.</summary>
    public decimal Effort { get; init; } = 0.30m;

    /// <summary>
    /// How much the rest of the household counts. Small: it is the honest
    /// residue of collaborative filtering at this scale — "your partner cooks
    /// this" — and a larger value would flatten two people's different tastes
    /// into one household average, which the person/household split in the
    /// schema exists to prevent.
    /// </summary>
    public decimal Household { get; init; } = 0.35m;

    /// <summary>How much never having been cooked counts.</summary>
    public decimal Novelty { get; init; } = 0.25m;

    /// <summary>
    /// How much having arrived recently counts. Culina's version of reserving
    /// slots for new items: a household that imports two hundred recipes has
    /// two hundred with no history, and a pure-relevance ranking would never
    /// show any of them.
    /// </summary>
    public decimal Freshness { get; init; } = 0.30m;

    /// <summary>
    /// How much this household's own cooking calendar counts. Zero until there
    /// is evidence — see <see cref="SeasonMinimumObservations"/>.
    /// </summary>
    public decimal Season { get; init; } = 0.40m;

    /// <summary>
    /// How far a recipe may drift for the sake of variety. About the gap
    /// between neighbours in the middle of a list, so exploration reshuffles
    /// near-equals and never promotes something that lost.
    /// </summary>
    public decimal Exploration { get; init; } = 0.15m;

    /// <summary>
    /// How much similarity to the recipe on screen counts, for
    /// <see cref="SuggestionPurpose.Like"/>.
    /// </summary>
    public decimal Similarity { get; init; } = 1.50m;

    /// <summary>
    /// Days for a signal to be worth half of what it was. A year: a recipe
    /// somebody loved two years ago still counts, and counts less.
    /// </summary>
    public int HalfLifeDays { get; init; } = 365;

    /// <summary>
    /// How fast the repetition penalty fades, in days. Ten gives roughly
    /// −1 the same day, −0.5 after a week, −0.25 after a fortnight and nothing
    /// after two months.
    /// </summary>
    public int RepetitionDecayDays { get; init; } = 10;

    /// <summary>When rediscovery starts to count, in days since the household last cooked it.</summary>
    public int RediscoveryFromDays { get; init; } = 90;

    /// <summary>When rediscovery reaches its full value, in days.</summary>
    public int RediscoveryFullDays { get; init; } = 180;

    /// <summary>How long a new recipe stays new, in days. Its freshness halves over this.</summary>
    public int FreshnessHalfLifeDays { get; init; } = 30;

    /// <summary>
    /// How long "not tonight" lasts, in days. Dismissals expire so that hiding
    /// something once does not quietly become hiding it forever.
    /// </summary>
    public int DismissalDays { get; init; } = 90;

    /// <summary>
    /// How many cook-log entries a tag needs before this household is allowed
    /// to claim a season for it.
    /// </summary>
    /// <remarks>
    /// The gate is the whole difference between observed seasonality and the
    /// curated ingredient-to-season table this project rejected. Below it the
    /// term is exactly zero and no seasonal reason is shown, so a fresh
    /// installation says nothing about seasons rather than saying something
    /// confident and wrong.
    /// </remarks>
    public int SeasonMinimumObservations { get; init; } = 12;

    /// <summary>
    /// How much of a tag's cooking must fall in this month before it counts as
    /// seasonal, as a multiple of an even spread. 1.5 of one twelfth.
    /// </summary>
    public decimal SeasonMinimumShare { get; init; } = 1.5m / 12m;

    /// <summary>
    /// How far a candidate is pushed down for resembling one already chosen.
    /// Deliberately modest: variety that overrides preference produces a list of
    /// things nobody wants, which is worse than a list of similar things they do.
    /// </summary>
    public decimal DiversityPenalty { get; init; } = 0.35m;

    /// <summary>
    /// What share of a suggestion's positive score one term must carry before it
    /// may be shown as the reason.
    /// </summary>
    public decimal ReasonDominance { get; init; } = 0.35m;
}
