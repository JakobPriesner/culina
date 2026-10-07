using Domain.Suggestions;
using Infrastructure.Persistence;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Suggestions.Replay;

/// <summary>
/// A database of its own for a calibration's kitchen: every ordering rule empties its database,
/// which would wipe the history the next replay needs.
/// </summary>
public sealed class CalibrationDatabase : IAsyncLifetime
{
    internal PostgresFixture Postgres { get; } = new();

    public ValueTask InitializeAsync() => Postgres.InitializeAsync();

    public ValueTask DisposeAsync() => Postgres.DisposeAsync();
}

/// <summary>
/// Proposes a weight vector from a replay, guarded by the ordering rules (see
/// <see cref="WeightCalibration"/>); explicit because a descent takes minutes.
/// </summary>
[Collection(RequiresDatabase.Name)]
public class WeightCalibrationTests(PostgresFixture postgres, CalibrationDatabase kitchen)
    : IClassFixture<CalibrationDatabase>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// Calibrates against the simulated kitchen: shows what the harness can find, never what the
    /// weights should be.
    /// </summary>
    [Fact(Explicit = true)]
    public async Task Calibrate_ShouldOnlyProposeWeightsThatKeepEveryRule_OnTheSimulatedKitchen()
    {
        var householdId = await ReplayKitchen.BuildAsync(kitchen.Postgres, Token);

        await using var session = kitchen.Postgres.NewSession();

        var result = await CalibrateAsync(session, [householdId]);

        await AssertProposalKeepsEveryRuleAsync(result);
    }

    /// <summary>
    /// Calibrates against a real cook log: the run whose numbers a weight change ships with.
    /// </summary>
    [Fact(Explicit = true)]
    public async Task Calibrate_ShouldOnlyProposeWeightsThatKeepEveryRule_OnARestoredDatabase()
    {
        var connectionString = RestoredDatabase.ConnectionString;

        Assert.SkipWhen(connectionString is null, $"Set {RestoredDatabase.Variable} to a restored copy of a Culina database.");

        await using var dataSource = RestoredDatabase.Open(connectionString!);
        await using var session = PostgresFixture.SessionOn(dataSource);

        var result = await CalibrateAsync(session, await RestoredDatabase.HouseholdsAsync(session, Token));

        await AssertProposalKeepsEveryRuleAsync(result);
    }

    private async Task<CalibrationResult> CalibrateAsync(DbSession session, IReadOnlyList<Guid> householdIds)
    {
        List<HouseholdReplay> households = [];

        foreach (var householdId in householdIds)
        {
            var replay = new SuggestionReplay(session, householdId);

            households.Add(new HouseholdReplay(replay, await replay.PointsAsync(Token)));
        }

        var calibration = new WeightCalibration(households, BrokenRulesAsync, Write);
        var result = await calibration.RunAsync(RankingWeights.Default, Token);

        Write("before");
        Write(result.BeforeReport.ToString());
        Write("after");
        Write(result.AfterReport.ToString());
        Write(result.Changes.Count == 0 ? "no change proposed" : string.Join(Environment.NewLine, result.Changes));

        return result;
    }

    /// <summary>
    /// Asked again of the vector actually proposed, so the claim is checked rather than inherited
    /// from the steps.
    /// </summary>
    private async Task AssertProposalKeepsEveryRuleAsync(CalibrationResult result) =>
        Assert.Empty(await BrokenRulesAsync(result.After));

    private async Task<IReadOnlyList<string>> BrokenRulesAsync(RankingWeights weights)
    {
        using var steady = new CulinaApiFactory(postgres, weights: weights with { Exploration = 0m });
        using var jittered = new CulinaApiFactory(postgres, weights: weights);

        return await RankingRules.BrokenAsync(postgres, new RankingHosts(steady, jittered));
    }

    private static void Write(string line) => TestContext.Current.TestOutputHelper?.WriteLine(line);
}
