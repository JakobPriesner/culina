using System.Globalization;
using System.Reflection;
using System.Text;
using Domain.Nutrition;
using Domain.Recipes;

namespace IntegrationTests.Nutrition.Evaluation;

/// <summary>
/// Scores the name table and the gram rules over real ingredient lines, judged by hand without
/// sight of the table.
/// </summary>
/// <remarks>
/// <c>nutrition-lines.tsv</c> holds 970 lines of 125 recipes (recipe, amount, unit, name, and the
/// BLS codes that would be right, or NONE when no food fits), each judged per line with its unit in
/// view. The food compared is the one counted for that unit (<see cref="NutritionGrams.Resolve"/>), so
/// a broth line is right as the powder or as the liquid, whichever its unit says. A wrong food is what
/// the README refuses, so precision is the one hard bar; recognition and coverage are only reported.
/// </remarks>
public class NutritionEvaluationTests
{
    private const double PrecisionBar = 0.98;

    [Fact]
    public void FoodNames_ShouldRecogniseAFoodRightAtLeastNinetyEightTimesInAHundred()
    {
        // Arrange
        var lines = Load();

        // Act
        var outcomes = lines.Select(Judge).ToList();

        // Assert
        var report = Report(outcomes);
        TestContext.Current.SendDiagnosticMessage(report);

        var recognised = outcomes.Where(one => one.Food is not null).ToList();
        var precision = recognised.Count(one => one.Right) / (double)recognised.Count;

        Assert.True(precision >= PrecisionBar, report);
    }

    private static Outcome Judge(Line line)
    {
        var entry = FoodNames.Match(line.Name);

        if (entry is null)
        {
            return new Outcome(line, null, Right: false, Counted: false, WithoutEggs: false, WithoutDensity: false, WithNeither: false, CountedWithTypical: false);
        }

        // The food the calculator counts for this line's unit: a broth by the spoon or by weight is the
        // powder, by volume or more than 50 g the liquid.
        var food = NutritionGrams.Resolve(line.Quantity, entry);
        var basis = NutritionGrams.Read(line.Quantity, food);
        var counted = basis.Grams is not null;
        var withTypical = NutritionGrams.Read(line.Quantity, food, householdWeights: null, useTypicalWeights: true).Grams is not null;

        return new Outcome(
            line,
            food,
            line.Expected.Contains(food.Code),
            counted,
            counted && basis.Basis != GramsBasis.EggSize,
            counted && basis.Basis != GramsBasis.Density,
            counted && basis.Basis == GramsBasis.Mass,
            withTypical);
    }

    private static string Report(List<Outcome> outcomes)
    {
        var recognised = outcomes.Where(one => one.Food is not null).ToList();
        var wrong = recognised.Where(one => !one.Right).ToList();
        var recipes = outcomes.GroupBy(one => one.Line.Recipe).ToList();

        var shares = recipes
            .Select(group => group.Count(one => one.Counted) / (double)group.Count())
            .Order()
            .ToList();
        var median = Median(shares);

        var sharesWithTypical = recipes
            .Select(group => group.Count(one => one.CountedWithTypical) / (double)group.Count())
            .Order()
            .ToList();
        var medianWithTypical = Median(sharesWithTypical);

        var report = new StringBuilder();
        report.AppendLine(CultureInfo.InvariantCulture, $"Nutrition evaluation over {outcomes.Count} lines of {recipes.Count} recipes");
        report.AppendLine(CultureInfo.InvariantCulture, $"  recognised:               {recognised.Count}/{outcomes.Count}");
        report.AppendLine(CultureInfo.InvariantCulture, $"  precision:                {recognised.Count - wrong.Count}/{recognised.Count} = {(recognised.Count - wrong.Count) / (double)recognised.Count:P1}");
        report.AppendLine(CultureInfo.InvariantCulture, $"  counted:                  {outcomes.Count(one => one.Counted)}/{outcomes.Count}");
        report.AppendLine(CultureInfo.InvariantCulture, $"  counted without eggs:     {outcomes.Count(one => one.WithoutEggs)}/{outcomes.Count}");
        report.AppendLine(CultureInfo.InvariantCulture, $"  counted without density:  {outcomes.Count(one => one.WithoutDensity)}/{outcomes.Count}");
        report.AppendLine(CultureInfo.InvariantCulture, $"  counted with neither:     {outcomes.Count(one => one.WithNeither)}/{outcomes.Count}");
        report.AppendLine(CultureInfo.InvariantCulture, $"  recipes counted fully:    {recipes.Count(group => group.All(one => one.Counted))}/{recipes.Count}");
        report.AppendLine(CultureInfo.InvariantCulture, $"  median share counted:     {median:P0}");
        report.AppendLine(CultureInfo.InvariantCulture, $"  counted with typical weights:          {outcomes.Count(one => one.CountedWithTypical)}/{outcomes.Count}");
        report.AppendLine(CultureInfo.InvariantCulture, $"  recipes counted fully with typical:    {recipes.Count(group => group.All(one => one.CountedWithTypical))}/{recipes.Count}");
        report.AppendLine(CultureInfo.InvariantCulture, $"  median share counted with typical:     {medianWithTypical:P0}");

        foreach (var one in wrong)
        {
            report.AppendLine(CultureInfo.InvariantCulture, $"  WRONG: recipe {one.Line.Recipe} '{one.Line.Name}' -> {one.Food!.Code} (expected {string.Join('|', one.Line.Expected)})");
        }

        return report.ToString();
    }

    private static double Median(List<double> sorted) =>
        sorted.Count % 2 == 1
            ? sorted[sorted.Count / 2]
            : (sorted[(sorted.Count / 2) - 1] + sorted[sorted.Count / 2]) / 2;

    private static List<Line> Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("nutrition-lines.tsv")!;
        using var reader = new StreamReader(stream);

        _ = reader.ReadLine();

        var lines = new List<Line>();

        while (reader.ReadLine() is { Length: > 0 } text)
        {
            var cells = text.Split('\t');
            var unit = cells[2].Length == 0 ? null : Unit.Create(cells[2]).Match<Unit?>(one => one, _ => null);
            decimal? amount = cells[1].Length == 0 ? null : decimal.Parse(cells[1], CultureInfo.InvariantCulture);
            var quantity = Quantity.Create(amount, unit).Match(one => one, _ => Quantity.Unmeasured);

            lines.Add(new Line(cells[0], quantity, cells[3], [.. cells[4].Split('|')]));
        }

        return lines;
    }

    private sealed record Line(string Recipe, Quantity Quantity, string Name, HashSet<string> Expected);

    private sealed record Outcome(
        Line Line,
        FoodName? Food,
        bool Right,
        bool Counted,
        bool WithoutEggs,
        bool WithoutDensity,
        bool WithNeither,
        bool CountedWithTypical);
}
