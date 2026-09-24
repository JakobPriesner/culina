using Domain.Suggestions;
using Infrastructure.Persistence;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Suggestions.Replay;

/// <summary>
/// A database of its own for the kitchen a calibration replays.
/// </summary>
/// <remarks>
/// A calibration checks every vector it is about to keep against the ordering
/// rules, and every rule starts by emptying the database it runs in. Sharing one
/// would have the first check wipe the history the next replay needs.
/// </remarks>
public sealed class CalibrationDatabase : IAsyncLifetime
{
    internal PostgresFixture Postgres { get; } = new();

    public ValueTask InitializeAsync() => Postgres.InitializeAsync();

    public ValueTask DisposeAsync() => Postgres.DisposeAsync();
}

/// <summary>
/// Proposes a weight vector from a replay, guarded by the ordering rules. See
/// <see cref="WeightCalibration"/>.
/// </summary>
/// <remarks>
/// Both explicit: a descent is minutes of replay, and what it prints is an
/// argument for a commit, not a pass or a fail for a build.
/// </remarks>
[Collection(RequiresDatabase.Name)]
public class WeightCalibrationTests(PostgresFixture postgres, CalibrationDatabase kitchen)
    : IClassFixture<CalibrationDatabase>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// Calibrates against the simulated kitchen. It says what the harness can
    /// find, never what the weights should be: a vector tuned here is tuned to
    /// <see cref="ReplayKitchen"/>.
    /// </summary>
    [Fact(Explicit = true)]
    public async Task Calibrate_ShouldOnlyProposeWeightsThatKeepEveryRule_OnTheSimulatedKitchen()
    {
        // Arrange
        var householdId = await ReplayKitchen.BuildAsync(kitchen.Postgres, Token);

        await using var session = kitchen.Postgres.NewSession();

        // Act
        var result = await CalibrateAsync(session, [householdId]);

        // Assert
        await AssertProposalKeepsEveryRuleAsync(result);
    }

    /// <summary>Calibrates against a real cook log: the run whose numbers a weight change ships with.</summary>
    [Fact(Explicit = true)]
    public async Task Calibrate_ShouldOnlyProposeWeightsThatKeepEveryRule_OnARestoredDatabase()
    {
        // Arrange
        var connectionString = RestoredDatabase.ConnectionString;

        Assert.SkipWhen(connectionString is null, $"Set {RestoredDatabase.Variable} to a restored copy of a Culina database.");

        await using var dataSource = RestoredDatabase.Open(connectionString!);
        await using var session = new DbSession(dataSource);

        // Act
        var result = await CalibrateAsync(session, await RestoredDatabase.HouseholdsAsync(session, Token));

        // Assert
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
    /// Asked once more at the end, of the vector actually proposed, so the claim
    /// the proposal makes is checked rather than inherited from the steps.
    /// </summary>
    private async Task AssertProposalKeepsEveryRuleAsync(CalibrationResult result) =>
        Assert.Empty(await BrokenRulesAsync(result.After));

    /// <summary>Which ordering rules these weights break, checked through hosts built with them.</summary>
    private async Task<IReadOnlyList<string>> BrokenRulesAsync(RankingWeights weights)
    {
        using var steady = new CulinaApiFactory(postgres, weights: weights with { Exploration = 0m });
        using var jittered = new CulinaApiFactory(postgres, weights: weights);

        return await RankingRules.BrokenAsync(postgres, new RankingHosts(steady, jittered));
    }

    private static void Write(string line) => TestContext.Current.TestOutputHelper?.WriteLine(line);
}
