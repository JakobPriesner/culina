using Domain.Suggestions;
using Infrastructure.Persistence;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Suggestions.Replay;

/// <summary>
/// The offline replay: how well the ranker predicts what a household cooked
/// (<c>docs/suggestions-research.md</c> §M.1).
/// </summary>
[Collection(RequiresDatabase.Name)]
public class SuggestionReplayTests(PostgresFixture postgres)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Run_ShouldPredictWhatTheKitchenCooked_AtLeastTwiceAsWellAsChance()
    {
        var householdId = await ReplayKitchen.BuildAsync(postgres, Token);

        await using var session = postgres.NewSession();
        var replay = new SuggestionReplay(session, householdId);
        var points = await replay.PointsAsync(Token);

        var report = ReplayReport.Of(await replay.RunAsync(points, RankingWeights.Default, Token));

        // Not a weight target (the kitchen is simulated) but a floor under harness and ranking: a
        // ranking that cannot stay twice as far from a shuffle on a household this plain is not
        // ranking.
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
        var householdId = await ReplayKitchen.BuildAsync(postgres, Token);

        await using var session = postgres.NewSession();
        var replay = new SuggestionReplay(session, householdId);
        var points = await replay.PointsAsync(Token);
        var before = await CountHistoryAsync(session);

        await replay.RunAsync(points, RankingWeights.Default, Token);

        // The replay deletes a household's future once per entry; leftovers would skew the next
        // weight vector's score.
        Assert.Equal(before, await CountHistoryAsync(session));
    }

    /// <summary>
    /// The number a weight change is defended with: a replay of a real cook log; explicit, as it
    /// needs a <see cref="RestoredDatabase"/>.
    /// </summary>
    [Fact(Explicit = true)]
    public async Task Run_ShouldReportEveryHousehold_InARestoredDatabase()
    {
        var connectionString = RestoredDatabase.ConnectionString;

        Assert.SkipWhen(connectionString is null, $"Set {RestoredDatabase.Variable} to a restored copy of a Culina database.");

        await using var dataSource = RestoredDatabase.Open(connectionString!);
        await using var session = PostgresFixture.SessionOn(dataSource);

        foreach (var householdId in await RestoredDatabase.HouseholdsAsync(session, Token))
        {
            var replay = new SuggestionReplay(session, householdId);

            var outcomes = await replay.RunAsync(await replay.PointsAsync(Token), RankingWeights.Default, Token);

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
