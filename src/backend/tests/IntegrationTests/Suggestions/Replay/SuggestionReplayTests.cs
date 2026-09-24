using Domain.Suggestions;
using Infrastructure.Persistence;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Suggestions.Replay;

/// <summary>
/// The offline replay: how well the ranker predicts what a household actually
/// cooked. See <c>docs/suggestions-research.md</c> §M.1.
/// </summary>
[Collection(RequiresDatabase.Name)]
public class SuggestionReplayTests(PostgresFixture postgres)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Run_ShouldPredictWhatTheKitchenCooked_AtLeastTwiceAsWellAsChance()
    {
        // Arrange
        var householdId = await ReplayKitchen.BuildAsync(postgres, Token);

        await using var session = postgres.NewSession();
        var replay = new SuggestionReplay(session, householdId);
        var points = await replay.PointsAsync(Token);

        // Act
        var report = ReplayReport.Of(await replay.RunAsync(points, RankingWeights.Default, Token));

        // Assert
        // Not a target for the weights — the kitchen is simulated, and a ranker
        // tuned to it is tuned to a file. It is the floor under the harness and
        // the ranking together: a shuffled library scores chance, and a ranking
        // that cannot keep twice its distance from a shuffle on a household with
        // habits this plain is not ranking. A sign flipped in the scoring, or a
        // replay that leaks the answer or hides it, lands on one side of this.
        TestContext.Current.TestOutputHelper?.WriteLine(report.ToString());

        Assert.All(
            new[] { report.Frozen, report.Rolling },
            score =>
            {
                Assert.True(score.Points > 0, "Both slices must have something in them to predict.");
                Assert.True(score.RecallAt5 >= 2 * score.ChanceAt5, report.ToString());
            });
    }

    [Fact]
    public async Task Run_ShouldLeaveTheHouseholdExactlyAsItFoundIt()
    {
        // Arrange
        var householdId = await ReplayKitchen.BuildAsync(postgres, Token);

        await using var session = postgres.NewSession();
        var replay = new SuggestionReplay(session, householdId);
        var points = await replay.PointsAsync(Token);
        var before = await CountHistoryAsync(session);

        // Act
        await replay.RunAsync(points, RankingWeights.Default, Token);

        // Assert
        // The replay deletes a household's future once per entry. If any of that
        // survived, the second weight vector a calibration tried would be scored
        // against a kitchen the first one had already emptied.
        Assert.Equal(before, await CountHistoryAsync(session));
    }

    /// <summary>
    /// The number a weight change is actually defended with: a replay of a real
    /// cook log. Explicit, because it needs a <see cref="RestoredDatabase"/>.
    /// </summary>
    [Fact(Explicit = true)]
    public async Task Run_ShouldReportEveryHousehold_InARestoredDatabase()
    {
        // Arrange
        var connectionString = RestoredDatabase.ConnectionString;

        Assert.SkipWhen(connectionString is null, $"Set {RestoredDatabase.Variable} to a restored copy of a Culina database.");

        await using var dataSource = RestoredDatabase.Open(connectionString!);
        await using var session = new DbSession(dataSource);

        foreach (var householdId in await RestoredDatabase.HouseholdsAsync(session, Token))
        {
            var replay = new SuggestionReplay(session, householdId);

            // Act
            var outcomes = await replay.RunAsync(await replay.PointsAsync(Token), RankingWeights.Default, Token);

            // Assert
            TestContext.Current.TestOutputHelper?.WriteLine($"household {householdId}");
            TestContext.Current.TestOutputHelper?.WriteLine(ReplayReport.Of(outcomes).ToString());
        }
    }

    private static Task<long> CountHistoryAsync(DbSession session) =>
        new DbExecutor(session).ExecuteScalarAsync<long>(
            """
            select (select count(*) from cook_log_entries)
                 + (select count(*) from recipes)
                 + (select count(*) from recipe_ingredients);
            """,
            null,
            Token);
}
