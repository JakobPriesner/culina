using System.Globalization;

namespace IntegrationTests.Suggestions.Replay;

/// <summary>How well one set of replayed shortlists predicted what was cooked.</summary>
/// <param name="Points">How many cook-log entries were predicted.</param>
/// <param name="RecallAt5">Share of entries whose recipe was in the first five.</param>
/// <param name="RecallAt10">Share of entries whose recipe was in the first ten.</param>
/// <param name="ReciprocalRank">
/// Mean of one over the recipe's position, zero when it was not in the list:
/// recall that also cares whether the answer was first or fifth.
/// </param>
/// <param name="ChanceAt5">
/// What recall@5 a shuffled library would have scored, for scale. A ranker is
/// only worth its SQL by the distance it keeps from this.
/// </param>
internal sealed record ReplayScore(
    int Points,
    double RecallAt5,
    double RecallAt10,
    double ReciprocalRank,
    double ChanceAt5)
{
    internal static ReplayScore Of(IReadOnlyList<ReplayOutcome> outcomes)
    {
        if (outcomes.Count == 0)
        {
            return new ReplayScore(0, 0, 0, 0, 0);
        }

        return new ReplayScore(
            outcomes.Count,
            outcomes.Average(outcome => outcome.Rank <= 5 ? 1.0 : 0.0),
            outcomes.Average(outcome => outcome.Rank <= 10 ? 1.0 : 0.0),
            outcomes.Average(outcome => outcome.Rank is { } rank ? 1.0 / rank : 0.0),
            outcomes.Average(outcome => Math.Min(1.0, 5.0 / outcome.LibrarySize)));
    }
}

/// <summary>
/// A replay scored twice: on the history the app could not have influenced, and
/// on the recent past.
/// </summary>
/// <remarks>
/// <para>
/// Both, because of the caveat in <c>docs/suggestions-research.md</c> §M.1: the
/// objective is "predict what they cooked", and once the app suggests things,
/// part of what they cook is the ranker's own output. A weight vector can then
/// score well by agreeing with an older version of itself.
/// </para>
/// <para>
/// The frozen slice ends the day suggestions shipped, so it stays a permanent
/// reference point that no later ranking can have shaped. The rolling window is
/// what the household is like now. A change worth making moves the first without
/// hurting the second.
/// </para>
/// </remarks>
internal sealed record ReplayReport(ReplayScore Frozen, ReplayScore Rolling)
{
    /// <summary>The day suggestions first reached anybody's front page, in commit 9ccc063.</summary>
    internal static readonly DateTimeOffset SuggestionsLaunched = new(2026, 9, 18, 0, 0, 0, TimeSpan.Zero);

    /// <summary>How far back "now" reaches, from the newest entry predicted.</summary>
    internal const int RollingDays = 180;

    internal static ReplayReport Of(IReadOnlyList<ReplayOutcome> outcomes)
    {
        if (outcomes.Count == 0)
        {
            return new ReplayReport(ReplayScore.Of([]), ReplayScore.Of([]));
        }

        var newest = outcomes.Max(outcome => outcome.Point.MadeAt);
        var rollingFrom = newest.AddDays(-RollingDays);

        return new ReplayReport(
            ReplayScore.Of([.. outcomes.Where(outcome => outcome.Point.MadeAt < SuggestionsLaunched)]),
            ReplayScore.Of([.. outcomes.Where(outcome => outcome.Point.MadeAt > rollingFrom)]));
    }

    /// <summary>A small table, for a test's output and a commit message.</summary>
    public override string ToString() =>
        string.Join(
            Environment.NewLine,
            "slice    points  recall@5  recall@10  MRR    chance@5",
            Row("frozen", Frozen),
            Row("rolling", Rolling));

    private static string Row(string name, ReplayScore score) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{name,-8} {score.Points,6}  {score.RecallAt5,8:0.000}  {score.RecallAt10,9:0.000}  {score.ReciprocalRank,5:0.000}  {score.ChanceAt5,8:0.000}");
}
