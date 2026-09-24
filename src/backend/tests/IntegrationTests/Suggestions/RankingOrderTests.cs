using Domain.Suggestions;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Suggestions;

/// <summary>
/// The ordering rules in <see cref="RankingRules"/>, put to the weights that ship.
/// </summary>
/// <remarks>
/// One case per rule, named after it, so a broken promise reads as the promise
/// rather than as a line number.
/// </remarks>
[Collection(RequiresDatabase.Name)]
public class RankingOrderTests(PostgresFixture postgres)
{
    public static TheoryData<string> Rules => [.. RankingRules.All.Select(rule => rule.Name)];

    [Theory]
    [MemberData(nameof(Rules))]
    public async Task Rule_ShouldHold_ForTheShippedWeights(string rule)
    {
        // Arrange
        var hosts = new RankingHosts(Steady: postgres.SteadyRanking, Jittered: postgres.Api);

        // Act
        var broken = await RankingRules.Named(rule).CheckAsync(postgres, hosts);

        // Assert
        Assert.Null(broken);
    }

    [Fact]
    public async Task Rule_ShouldBreak_WhenAWeightNoLongerKeepsItsPromise()
    {
        // Arrange
        // The rules are what a calibration trusts to reject a vector, so a rule
        // that holds for every vector would wave anything through. With
        // repetition worth nothing, yesterday's dinner is as good as ever.
        using var careless = new CulinaApiFactory(
            postgres,
            weights: RankingWeights.Default with { Repetition = 0m, Exploration = 0m });

        var rule = RankingRules.Named("Rule1_ARecipeCookedYesterday_ShouldNotOutrankTheSameOneCookedThreeWeeksAgo");

        // Act
        var broken = await rule.CheckAsync(postgres, new RankingHosts(careless, careless));

        // Assert
        Assert.NotNull(broken);
    }
}
