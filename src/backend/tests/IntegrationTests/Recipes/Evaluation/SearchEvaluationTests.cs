using System.Globalization;
using System.Text;
using System.Text.Json;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Recipes.Evaluation;

/// <summary>Scores search quality over a deliberately tricky mixed German/English golden library.</summary>
/// <remarks>
/// Queries carry graded judgments (2 intended, 1 fair, 0 otherwise); data is <c>golden-library.json</c>.
/// <see cref="RecipeSearchRelevanceTests"/> covers what must never happen; this covers how well the rest goes.
/// </remarks>
[Collection(RequiresDatabase.Name)]
public class SearchEvaluationTests(PostgresFixture postgres)
{
    // Precision over the first three (what fits above the fold on a phone) and NDCG over the first ten.
    private const double PrecisionAtThreeTarget = 0.85;

    private const double NdcgAtTenTarget = 0.80;

    [Fact]
    public async Task Search_ShouldMeetItsQualityTargets_OverTheGoldenLibrary()
    {
        // Arrange
        var golden = GoldenLibrary.Load();
        var kitchen = await Kitchen.OpenAsync(postgres);
        await golden.SeedAsync(kitchen);

        // Act
        var outcomes = new List<Outcome>();

        foreach (var query in golden.Queries)
        {
            outcomes.Add(Judge(query, await kitchen.SearchAsync(query.Query)));
        }

        // Assert
        var report = Report(outcomes, golden.Recipes.Count);
        TestContext.Current.SendDiagnosticMessage(report);

        var ranked = outcomes.Where(one => !one.Query.Empty).ToList();
        var precision = ranked.Average(one => one.PrecisionAtThree);
        var ndcg = ranked.Average(one => one.NdcgAtTen);

        // Invariants rather than scores: a diet is never broken, a lexicon match never outranks a real one.
        Assert.True(outcomes.All(one => one.Violations.Count == 0), report);
        Assert.True(outcomes.All(one => one.Disciplined), report);
        Assert.True(outcomes.All(one => one.EmptyAsExpected), report);
        Assert.True(outcomes.All(one => one.ChipsAsExpected), report);
        Assert.True(precision >= PrecisionAtThreeTarget, report);
        Assert.True(ndcg >= NdcgAtTenTarget, report);
    }

    private static Outcome Judge(GoldenQuery query, ApiResponse response)
    {
        var body = response.Json!.Value;
        var items = body.GetProperty("items").EnumerateArray().ToList();
        var titles = items.Select(item => item.GetProperty("title").GetString()!).ToList();
        var grades = titles.Select(title => query.Grades.GetValueOrDefault(title)).ToList();

        // Out of at most as many as are relevant, so a known-item query with one answer can score 1.
        var relevant = query.Grades.Count(pair => pair.Value > 0);
        var precision = relevant == 0
            ? 1.0
            : grades.Take(3).Count(grade => grade > 0) / (double)Math.Min(3, relevant);

        var ndcg = Ndcg(query, grades, 10);

        // A concept match never above any other kind.
        var concept = items.Select(item =>
            item.TryGetProperty("matchReason", out var reason)
            && reason.ValueKind == JsonValueKind.Object
            && reason.GetProperty("kind").GetString() == "concept").ToList();
        var firstConcept = concept.IndexOf(true);
        var disciplined = firstConcept < 0 || concept.Skip(firstConcept).All(one => one);

        var chips = body.TryGetProperty("interpretation", out var interpretation)
                    && interpretation.ValueKind == JsonValueKind.Object
            ? interpretation.GetProperty("applied").EnumerateArray()
                .Select(chip => $"{chip.GetProperty("kind").GetString()}:{chip.GetProperty("value").GetString()}")
                .ToList()
            : [];

        return new Outcome(
            query,
            titles,
            precision,
            Ndcg(query, grades, 5),
            ndcg,
            [.. titles.Intersect(query.Never, StringComparer.Ordinal)],
            disciplined,
            query.Empty ? titles.Count == 0 : titles.Count > 0,
            query.Chips is null || query.Chips.SequenceEqual(chips, StringComparer.Ordinal));
    }

    private static double Ndcg(GoldenQuery query, List<int> grades, int depth)
    {
        var ideal = Dcg(query.Grades.Values.OrderDescending().Take(depth));

        return ideal == 0 ? 1.0 : Dcg(grades.Take(depth)) / ideal;
    }

    private static double Dcg(IEnumerable<int> grades) =>
        grades.Select((grade, rank) => (Math.Pow(2, grade) - 1) / Math.Log2(rank + 2)).Sum();

    private static string Report(List<Outcome> outcomes, int recipes)
    {
        var report = new StringBuilder(
            $"\nCulina search evaluation · {recipes} recipes · {outcomes.Count} queries\n\n");
        report.AppendLine(CultureInfo.InvariantCulture,
            $"  {"class",-20} {"P@3",6} {"NDCG@5",7} {"NDCG@10",8} {"zero",6}");

        foreach (var group in outcomes.GroupBy(one => one.Query.Class))
        {
            var ranked = group.Where(one => !one.Query.Empty).ToList();

            report.AppendLine(CultureInfo.InvariantCulture,
                $"  {group.Key,-20} {Score(ranked, one => one.PrecisionAtThree),6} "
                + $"{Score(ranked, one => one.NdcgAtFive),7} {Score(ranked, one => one.NdcgAtTen),8} "
                + $"{group.Count(one => one.EmptyAsExpected)}/{group.Count(),-4}");
        }

        var all = outcomes.Where(one => !one.Query.Empty).ToList();
        report.AppendLine(CultureInfo.InvariantCulture,
            $"  {"overall",-20} {Score(all, one => one.PrecisionAtThree),6} "
            + $"{Score(all, one => one.NdcgAtFive),7} {Score(all, one => one.NdcgAtTen),8}");
        report.AppendLine(CultureInfo.InvariantCulture,
            $"  tier discipline {outcomes.Count(one => one.Disciplined)}/{outcomes.Count}");

        foreach (var one in outcomes.Where(one =>
                     one.Violations.Count > 0 || !one.Disciplined || !one.EmptyAsExpected || !one.ChipsAsExpected
                     || (!one.Query.Empty && (one.PrecisionAtThree < 1 || one.NdcgAtTen < 0.8))))
        {
            var problems = string.Concat(
                one.Violations.Count > 0 ? $" RULE BROKEN by {string.Join(", ", one.Violations)}" : string.Empty,
                one.Disciplined ? string.Empty : " TIER ORDER BROKEN",
                one.EmptyAsExpected ? string.Empty : " WRONG EMPTINESS",
                one.ChipsAsExpected ? string.Empty : " WRONG CHIPS");

            report.AppendLine(CultureInfo.InvariantCulture,
                $"\n  “{one.Query.Query}” P@3 {one.PrecisionAtThree:F2} NDCG@10 {one.NdcgAtTen:F2}{problems}");
            report.AppendLine(CultureInfo.InvariantCulture, $"      got: {string.Join(" · ", one.Titles.Take(10))}");
        }

        return report.ToString();
    }

    private static string Score(List<Outcome> outcomes, Func<Outcome, double> measure) =>
        outcomes.Count == 0 ? "—" : outcomes.Average(measure).ToString("F2", CultureInfo.InvariantCulture);

    private sealed record Outcome(
        GoldenQuery Query,
        List<string> Titles,
        double PrecisionAtThree,
        double NdcgAtFive,
        double NdcgAtTen,
        List<string> Violations,
        bool Disciplined,
        bool EmptyAsExpected,
        bool ChipsAsExpected);
}
