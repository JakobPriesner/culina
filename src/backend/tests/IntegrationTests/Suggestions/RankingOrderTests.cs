using Domain.Suggestions;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Suggestions;

/// <summary>
/// The ordering rules in <see cref="RankingRules"/> against the weights that ship, one case per
/// rule, named after it.
/// </summary>
[Collection(RequiresDatabase.Name)]
public class RankingOrderTests(PostgresFixture postgres)
{
    public static TheoryData<string> Rules => [.. RankingRules.All.Select(rule => rule.Name)];

    [Theory]
    [MemberData(nameof(Rules))]
    public async Task Rule_ShouldHold_ForTheShippedWeights(string rule)
    {
        var hosts = new RankingHosts(Steady: postgres.SteadyRanking, Jittered: postgres.Api);

        var broken = await RankingRules.Named(rule).CheckAsync(postgres, hosts);

        Assert.Null(broken);
    }

    [Fact]
    public async Task Rule_ShouldBreak_WhenAWeightNoLongerKeepsItsPromise()
    {
        // A calibration trusts the rules to reject a vector, so a rule that holds for every vector
        // would wave anything through.
        using var careless = new CulinaApiFactory(
            postgres,
            weights: RankingWeights.Default with { Repetition = 0m, Exploration = 0m });

        var rule = RankingRules.Named("Rule1_ARecipeCookedYesterday_ShouldNotOutrankTheSameOneCookedThreeWeeksAgo");

        var broken = await rule.CheckAsync(postgres, new RankingHosts(careless, careless));

        Assert.NotNull(broken);
    }
}
