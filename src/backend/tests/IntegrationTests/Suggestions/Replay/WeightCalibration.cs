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
/// Coordinate descent over the weights, against replay recall@5. See
/// <c>docs/suggestions-research.md</c> §M.3.
/// </summary>
/// <remarks>
/// <para>
/// Hold every weight but one, try it at a handful of multiples of where it
/// started, keep the best, move on; then do the whole pass once more. Ten
/// weights, one number to improve and a few hundred evaluation points is not a
/// fitting problem, and anything cleverer than this would find structure in
/// noise.
/// </para>
/// <para>
/// <b>It proposes; it never applies.</b> Weights are code. An installation whose
/// ranking silently tuned itself would be unsupportable — two people comparing
/// notes could not reproduce each other, and a bug report could not be triaged.
/// A proposal becomes a weight when somebody edits <see cref="RankingWeights"/>
/// in a commit that carries the before-and-after numbers this prints.
/// </para>
/// <para>
/// <b>The ordering rules are a guard, not a tie-breaker.</b> A step is taken
/// only if every rule in <see cref="RankingRules"/> still holds for it. A vector
/// that wins on recall by suggesting Tuesday's dinner again on Wednesday has
/// found out that households repeat themselves, which everybody knew, and has
/// broken a promise the metric cannot see.
/// </para>
/// </remarks>
/// <param name="households">The cook logs to replay. Their outcomes are pooled: the weights are one set for everybody.</param>
/// <param name="brokenRules">Which ordering rules a vector breaks. Expensive — only asked of a vector about to be kept.</param>
/// <param name="log">Where the steps are written as they happen.</param>
internal sealed class WeightCalibration(
    IReadOnlyList<HouseholdReplay> households,
    Func<RankingWeights, Task<IReadOnlyList<string>>> brokenRules,
    Action<string> log)
{
    /// <summary>
    /// How many points each candidate is scored on.
    /// </summary>
    /// <remarks>
    /// A shortlist costs about 20 ms to replay, so a candidate over four hundred
    /// points is eight seconds and the whole descent the better part of an hour.
    /// Thinned evenly to this — the stride rounds down, so the simulated
    /// kitchen's 413 frozen points become 207 — the descent and its rule checks
    /// took 11 minutes. The before-and-after report is still worked out over
    /// every point.
    /// </remarks>
    private const int SamplePoints = 150;

    private const int Rounds = 2;

    /// <summary>Where each weight is tried, as multiples of its starting value.</summary>
    private static readonly decimal[] Grid = [0m, 0.5m, 0.75m, 1m, 1.25m, 1.5m, 2m, 3m];

    /// <summary>
    /// The weights that can move the front page's answer.
    /// </summary>
    /// <remarks>
    /// The replay asks the question the front page asks: no slot, nothing to
    /// resemble. So <see cref="RankingWeights.Slot"/> and
    /// <see cref="RankingWeights.Similarity"/> cannot change a single replayed
    /// list, and sweeping them would spend minutes measuring the absence of an
    /// effect.
    /// </remarks>
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

    private sealed record Candidate(RankingWeights Weights, double RecallAt5);

    internal async Task<CalibrationResult> RunAsync(RankingWeights start, CancellationToken cancellationToken)
    {
        var sample = Sample();
        var best = new Candidate(start, await RecallAt5Async(start, sample, cancellationToken));

        log(Line($"start: recall@5 {best.RecallAt5:0.000} over {sample.Sum(household => household.Points.Count)} points"));

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

    /// <summary>
    /// Tries one weight across the grid and keeps the best value that beats the
    /// current recall and still keeps every rule.
    /// </summary>
    /// <remarks>
    /// Scored first and guarded second, best first: the rules take seconds to
    /// check and most candidates never beat the incumbent, so only the ones
    /// that would be kept are asked.
    /// </remarks>
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

            var recall = await RecallAt5Async(weights, sample, cancellationToken);

            if (recall > best.RecallAt5)
            {
                better.Add(new Candidate(weights, recall));
            }
        }

        foreach (var candidate in better.OrderByDescending(candidate => candidate.RecallAt5))
        {
            var broken = await brokenRules(candidate.Weights);
            var value = coordinate.Get(candidate.Weights);

            if (broken.Count == 0)
            {
                log(Line($"{coordinate.Name} {value:0.00}: recall@5 {best.RecallAt5:0.000} -> {candidate.RecallAt5:0.000}, kept"));

                return candidate;
            }

            log(Line($"{coordinate.Name} {value:0.00}: recall@5 {candidate.RecallAt5:0.000}, rejected — {string.Join("; ", broken)}"));
        }

        return best;
    }

    /// <summary>
    /// The points candidates are compared on: the frozen slice only, evenly
    /// thinned to <see cref="SamplePoints"/>.
    /// </summary>
    /// <remarks>
    /// Frozen, because it is the one slice no earlier ranking can have shaped —
    /// optimising against what people cooked after the app started suggesting
    /// things would reward agreeing with the app.
    /// </remarks>
    private List<HouseholdReplay> Sample()
    {
        var frozen = households
            .Select(household => household with
            {
                Points = [.. household.Points.Where(point => point.MadeAt < ReplayReport.SuggestionsLaunched)]
            })
            .ToList();

        var stride = Math.Max(1, frozen.Sum(household => household.Points.Count) / SamplePoints);

        return [.. frozen.Select(household => household with
        {
            Points = [.. household.Points.Where((_, index) => index % stride == 0)]
        })];
    }

    private static async Task<double> RecallAt5Async(
        RankingWeights weights,
        IReadOnlyList<HouseholdReplay> replays,
        CancellationToken cancellationToken) =>
        ReplayScore.Of(await OutcomesAsync(weights, replays, cancellationToken)).RecallAt5;

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
