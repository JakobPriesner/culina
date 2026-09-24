using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Recipes.Evaluation;

/// <summary>
/// How good search is, as numbers, over a library built to be difficult.
/// </summary>
/// <remarks>
/// <para>
/// Sixty recipes — forty German, twenty English, because the corpus is mixed
/// and a monolingual fixture hides the interesting bugs — and forty queries
/// across every kind people type, each with graded judgments: 2 for what the
/// query is for, 1 for a fair answer, nothing for the rest. The library holds
/// the traps on purpose: three Bolognese and a Ragù that is not one, compounds
/// no stemmer splits, Müsli spelt three ways, recipes that look vegetarian and
/// have fish sauce or chicken stock in them, recipes with no stated time, and
/// English recipes with German ingredient names.
/// </para>
/// <para>
/// The rule-based cases in <see cref="RecipeSearchRelevanceTests"/> say what
/// must never happen; this says how well the rest goes. A change that improves
/// the average and breaks one query is reported by name, per class, so the
/// person making it is told before it merges. The data is
/// <c>golden-library.json</c>, and adding a case is editing a list.
/// </para>
/// </remarks>
[Collection(RequiresDatabase.Name)]
public class SearchEvaluationTests(PostgresFixture postgres)
{
    /// <summary>
    /// The bar the whole set has to clear. Precision over the first three —
    /// what fits above the fold on a phone — and graded ranking quality over
    /// the first ten.
    /// </summary>
    private const double PrecisionAtThreeTarget = 0.85;

    private const double NdcgAtTenTarget = 0.80;

    [Fact]
    public async Task Search_ShouldMeetItsQualityTargets_OverTheGoldenLibrary()
    {
        // Arrange
        var golden = Golden.Load();
        var kitchen = await Kitchen.OpenAsync(postgres);

        foreach (var recipe in golden.Recipes)
        {
            await kitchen.SaveAsync(
                recipe.Title,
                recipe.Language,
                recipe.Prep,
                recipe.Cook,
                [.. recipe.Ingredients.Select(name => (name, (string?)null))],
                [.. recipe.Tags],
                recipe.Step);
        }

        // Act
        var outcomes = new List<Outcome>();

        foreach (var query in golden.Queries)
        {
            outcomes.Add(Judge(query, await kitchen.SearchAsync(query.Query)));
        }

        // Assert
        var report = Report(outcomes);
        TestContext.Current.SendDiagnosticMessage(report);

        var ranked = outcomes.Where(one => !one.Query.Empty).ToList();
        var precision = ranked.Average(one => one.PrecisionAtThree);
        var ndcg = ranked.Average(one => one.NdcgAtTen);

        // The two that are invariants rather than scores: a diet is never
        // broken, and a match through the lexicon is never above a real one.
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

        // Precision over the first three, out of as many relevant recipes as
        // there are: a known-item query with one right answer can score 1.
        var relevant = query.Grades.Count(pair => pair.Value > 0);
        var precision = relevant == 0
            ? 1.0
            : grades.Take(3).Count(grade => grade > 0) / (double)Math.Min(3, relevant);

        var ideal = Dcg(query.Grades.Values.OrderDescending().Take(10));
        var ndcg = ideal == 0 ? 1.0 : Dcg(grades.Take(10)) / ideal;

        // A concept match never above a match of any other kind.
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
            ndcg,
            [.. titles.Intersect(query.Never, StringComparer.Ordinal)],
            disciplined,
            query.Empty ? titles.Count == 0 : titles.Count > 0,
            query.Chips is null || query.Chips.SequenceEqual(chips, StringComparer.Ordinal));
    }

    /// <summary>Discounted cumulative gain, with gains of 2^grade − 1.</summary>
    private static double Dcg(IEnumerable<int> grades) =>
        grades.Select((grade, rank) => (Math.Pow(2, grade) - 1) / Math.Log2(rank + 2)).Sum();

    private static string Report(List<Outcome> outcomes)
    {
        var report = new StringBuilder("\nCulina search evaluation · 60 recipes · 40 queries\n\n");
        report.AppendLine(CultureInfo.InvariantCulture, $"  {"class",-20} {"P@3",6} {"NDCG@10",8} {"zero",6}");

        foreach (var group in outcomes.GroupBy(one => one.Query.Class))
        {
            var ranked = group.Where(one => !one.Query.Empty).ToList();

            report.AppendLine(CultureInfo.InvariantCulture,
                $"  {group.Key,-20} {Score(ranked, one => one.PrecisionAtThree),6} "
                + $"{Score(ranked, one => one.NdcgAtTen),8} "
                + $"{group.Count(one => one.EmptyAsExpected)}/{group.Count(),-4}");
        }

        var all = outcomes.Where(one => !one.Query.Empty).ToList();
        report.AppendLine(CultureInfo.InvariantCulture,
            $"  {"overall",-20} {Score(all, one => one.PrecisionAtThree),6} {Score(all, one => one.NdcgAtTen),8}");
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
        double NdcgAtTen,
        List<string> Violations,
        bool Disciplined,
        bool EmptyAsExpected,
        bool ChipsAsExpected);

    private sealed record GoldenRecipe(
        string Title,
        string Language,
        int? Prep,
        int? Cook,
        List<string> Tags,
        List<string> Ingredients,
        string Step);

    private sealed record GoldenQuery(
        string Query,
        string Class,
        Dictionary<string, int> Grades,
        bool Empty,
        List<string> Never,
        List<string>? Chips);

    private sealed record Golden(List<GoldenRecipe> Recipes, List<GoldenQuery> Queries)
    {
        private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

        internal static Golden Load()
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("golden-library.json")!;

            return JsonSerializer.Deserialize<Golden>(stream, Options)!;
        }
    }
}
