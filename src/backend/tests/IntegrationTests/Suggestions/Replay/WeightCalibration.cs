using System.Globalization;
using Domain.Suggestions;

namespace IntegrationTests.Suggestions.Replay;

/// <summary>One household's cook log, ready to replay.</summary>
internal sealed record HouseholdReplay(SuggestionReplay Replay, IReadOnlyList<ReplayPoint> Points);

/// <summary>What a calibration proposes, and the replay that argues for it.</summary>
/// <param name="Changes">One line per weight that moved, for the commit message that ships it.</param>
internal sealed record CalibrationResult(
    RankingWeights Before,
    RankingWeights After,
    ReplayReport BeforeReport,
    ReplayReport AfterReport,
    IReadOnlyList<string> Changes);

/// <summary>
/// Coordinate descent over the weights against replay recall@5, held to a ceiling on how often a shortlist repeats itself;
/// see <c>docs/suggestions-research.md</c> §M.3.
/// </summary>
/// <remarks>
/// It proposes, never applies: weights are code, changed in a commit that carries the before-and-after numbers.
/// A step is kept only if every rule in <see cref="RankingRules"/> still holds, so a vector cannot win on recall by breaking an ordering promise.
/// </remarks>
/// <param name="households">The cook logs to replay, pooled: the weights are one set for everybody.</param>
/// <param name="brokenRules">Which ordering rules a vector breaks. Expensive, so only asked of a vector about to be kept.</param>
/// <param name="log">Where the steps are written as they happen.</param>
internal sealed class WeightCalibration(
    IReadOnlyList<HouseholdReplay> households,
    Func<RankingWeights, Task<IReadOnlyList<string>>> brokenRules,
    Action<string> log)
{
    // Points each candidate is scored on. A shortlist replays in ~20 ms, so the full set would take most of an hour;
    // the before-and-after report still uses every point.
    private const int SamplePoints = 150;

    private const int Rounds = 2;

    /// <summary>
    /// The share of shown recipes that may already have been shown within the week before variety starts to cost recall.
    /// Below it recall alone decides; above it every point of repetition takes a point of recall away, so a vector
    /// that predicts dinner by showing the same five recipes all week does not win.
    /// </summary>
    /// <remarks>
    /// A choice, not a measurement: the first readings come from a simulated kitchen. Revisit it against a real cook log.
    /// It is compared with repetition on the thinned sample, which reads lower than the full report (0.52 against 0.78 at the
    /// shipped weights, because the first days of every kept week have no list a week back to repeat).
    /// </remarks>
    internal const double RepetitionCeiling = 0.5;

    // Where each weight is tried, as multiples of its starting value.
    private static readonly decimal[] Grid = [0m, 0.5m, 0.75m, 1m, 1.25m, 1.5m, 2m, 3m];

    // Only the weights that can move the front page's answer: the replay has no slot and nothing to resemble,
    // so Slot and Similarity cannot change a list.
    private static readonly Coordinate[] Coordinates =
    [
        new(nameof(RankingWeights.Affinity), weights => weights.Affinity, (weights, value) => weights with { Affinity = value }),
        new(nameof(RankingWeights.Content), weights => weights.Content, (weights, value) => weights with { Content = value }),
        new(nameof(RankingWeights.Repetition), weights => weights.Repetition, (weights, value) => weights with { Repetition = value }),
        new(nameof(RankingWeights.Rediscovery), weights => weights.Rediscovery, (weights, value) => weights with { Rediscovery = value }),
        new(nameof(RankingWeights.Effort), weights => weights.Effort, (weights, value) => weights with { Effort = value }),
        new(nameof(RankingWeights.Household), weights => weights.Household, (weights, value) => weights with { Household = value }),
        new(nameof(RankingWeights.Novelty), weights => weights.Novelty, (weights, value) => weights with { Novelty = value }),
        new(nameof(RankingWeights.Freshness), weights => weights.Freshness, (weights, value) => weights with { Freshness = value }),
        new(nameof(RankingWeights.Season), weights => weights.Season, (weights, value) => weights with { Season = value }),
        new(nameof(RankingWeights.Exploration), weights => weights.Exploration, (weights, value) => weights with { Exploration = value })
    ];

    private sealed record Coordinate(
        string Name,
        Func<RankingWeights, decimal> Get,
        Func<RankingWeights, decimal, RankingWeights> With);

    private sealed record Candidate(RankingWeights Weights, ReplayScore Score)
    {
        internal double Objective => ObjectiveOf(Score);
    }

    /// <summary>What a descent step must raise: recall@5, less whatever repetition exceeds <see cref="RepetitionCeiling"/>.</summary>
    internal static double ObjectiveOf(ReplayScore score) =>
        score.RecallAt5 - Math.Max(0, score.Quality.RepetitionRate - RepetitionCeiling);

    internal async Task<CalibrationResult> RunAsync(RankingWeights start, CancellationToken cancellationToken)
    {
        var sample = Sample();
        var best = new Candidate(start, await ScoreAsync(start, sample, cancellationToken));

        log(Line($"start: {Describe(best)} over {sample.Sum(household => household.Points.Count)} points"));

        for (var round = 1; round <= Rounds; round++)
        {
            foreach (var coordinate in Coordinates)
            {
                best = await SweepAsync(coordinate, start, best, sample, cancellationToken);
            }
        }

        return new CalibrationResult(
            start,
            best.Weights,
            await ReportAsync(start, cancellationToken),
            await ReportAsync(best.Weights, cancellationToken),
            [.. Coordinates
                .Where(coordinate => coordinate.Get(start) != coordinate.Get(best.Weights))
                .Select(coordinate => Line($"{coordinate.Name}: {coordinate.Get(start):0.00} -> {coordinate.Get(best.Weights):0.00}"))]);
    }

    // Tries one weight across the grid and keeps the best value that beats the current objective and keeps every rule.
    // Scored first, guarded second: the rules are slow, so only would-be-kept candidates are asked.
    private async Task<Candidate> SweepAsync(
        Coordinate coordinate,
        RankingWeights start,
        Candidate best,
        IReadOnlyList<HouseholdReplay> sample,
        CancellationToken cancellationToken)
    {
        List<Candidate> better = [];

        foreach (var factor in Grid)
        {
            var weights = coordinate.With(best.Weights, decimal.Round(coordinate.Get(start) * factor, 2));

            if (weights == best.Weights)
            {
                continue;
            }

            var candidate = new Candidate(weights, await ScoreAsync(weights, sample, cancellationToken));

            if (candidate.Objective > best.Objective)
            {
                better.Add(candidate);
            }
        }

        foreach (var candidate in better.OrderByDescending(candidate => candidate.Objective))
        {
            var broken = await brokenRules(candidate.Weights);
            var value = coordinate.Get(candidate.Weights);

            if (broken.Count == 0)
            {
                log(Line($"{coordinate.Name} {value:0.00}: {Describe(best)} -> {Describe(candidate)}, kept"));

                return candidate;
            }

            log(Line($"{coordinate.Name} {value:0.00}: {Describe(candidate)}, rejected — {string.Join("; ", broken)}"));
        }

        return best;
    }

    // The frozen slice only, thinned to about SamplePoints: no earlier ranking can have shaped it,
    // whereas later cooks would reward agreeing with the app.
    // Thinned by whole weeks, not by every n-th evening: repetition looks a week back, and a list whose
    // neighbours were skipped would look new every time.
    private List<HouseholdReplay> Sample()
    {
        var frozen = households
            .Select(household => household with
            {
                Points = [.. household.Points.Where(point => point.MadeAt < ReplayReport.SuggestionsLaunched)]
            })
            .ToList();

        var every = Math.Max(1, frozen.Sum(household => household.Points.Count) / SamplePoints);

        return [.. frozen.Select(household => household with
        {
            Points = [.. household.Points.Where(point => WeekOf(point, household.Points) % every == 0)]
        })];
    }

    private static int WeekOf(ReplayPoint point, IReadOnlyList<ReplayPoint> points) =>
        (int)((point.MadeAt - points.Min(one => one.MadeAt)).TotalDays / 7);

    private static async Task<ReplayScore> ScoreAsync(
        RankingWeights weights,
        IReadOnlyList<HouseholdReplay> replays,
        CancellationToken cancellationToken) =>
        ReplayScore.Of(await OutcomesAsync(weights, replays, cancellationToken));

    private static string Describe(Candidate candidate) =>
        Line($"objective {candidate.Objective:0.000} (recall@5 {candidate.Score.RecallAt5:0.000}, repeats {candidate.Score.Quality.RepetitionRate:0.000})");

    private async Task<ReplayReport> ReportAsync(RankingWeights weights, CancellationToken cancellationToken) =>
        ReplayReport.Of(await OutcomesAsync(weights, households, cancellationToken));

    private static async Task<List<ReplayOutcome>> OutcomesAsync(
        RankingWeights weights,
        IReadOnlyList<HouseholdReplay> replays,
        CancellationToken cancellationToken)
    {
        List<ReplayOutcome> outcomes = [];

        foreach (var household in replays)
        {
            outcomes.AddRange(await household.Replay.RunAsync(household.Points, weights, cancellationToken));
        }

        return outcomes;
    }

    private static string Line(FormattableString line) => line.ToString(CultureInfo.InvariantCulture);
}
