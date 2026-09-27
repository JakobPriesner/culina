using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using IntegrationTests.Fixtures;
using IntegrationTests.Recipes.Evaluation;

namespace IntegrationTests.Recipes;

/// <summary>
/// "Recipes like this one", measured against the golden library.
/// </summary>
/// <remarks>
/// A related recipe with no reason is a slot machine; one with a reason is a
/// suggestion somebody can disagree with. So every one of them is checked for
/// having something to say, and the report beside the assertion is what to
/// read when changing the weights.
/// </remarks>
[Collection(RequiresDatabase.Name)]
public class RelatedRecipeTests(PostgresFixture postgres)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Related_ShouldOfferAtLeastThreeWithAReason_ForEveryRecipeOfTheGoldenLibrary()
    {
        // Arrange
        var golden = GoldenLibrary.Load();
        var kitchen = await Kitchen.OpenAsync(postgres);
        var ids = await golden.SeedAsync(kitchen);

        // Act
        var answers = new Dictionary<string, List<JsonElement>>(StringComparer.Ordinal);

        foreach (var (title, recipeId) in ids)
        {
            answers[title] = await RelatedAsync(kitchen.Client, recipeId);
        }

        // Assert
        var report = Report(answers);
        TestContext.Current.SendDiagnosticMessage(report);

        Assert.All(answers, answer =>
        {
            Assert.True(answer.Value.Count >= 3, $"{answer.Key} has {answer.Value.Count} related.\n{report}");
            Assert.DoesNotContain(answer.Value, one => Title(one) == answer.Key);
            Assert.All(answer.Value, one => Assert.NotEmpty(Shared(one)));
        });
    }

    /// <summary>
    /// What a recipe is counts for more than what it happens to share.
    /// </summary>
    /// <remarks>
    /// The example the design was written around: somebody reading a
    /// Bolognese wants the Lasagne, not the Chili that has three of the same
    /// tins in it.
    /// </remarks>
    [Fact]
    public async Task Related_ShouldRankWhatARecipeIsAboveWhatItIsMadeFrom()
    {
        // Arrange
        var golden = GoldenLibrary.Load();
        var kitchen = await Kitchen.OpenAsync(postgres);
        var ids = await golden.SeedAsync(kitchen);

        // Act
        var related = (await RelatedAsync(kitchen.Client, ids["Spaghetti Bolognese"])).Select(Title).ToList();

        // Assert
        Assert.Contains("Lasagne Bolognese", related);
        var chili = related.IndexOf("Chili con Carne");
        Assert.True(
            chili < 0 || related.IndexOf("Lasagne Bolognese") < chili,
            string.Join(" · ", related));
    }

    [Fact]
    public async Task Related_ShouldWalkEveryPageOnceAndInOrder_WhenFollowingTheCursor()
    {
        // Arrange
        var golden = GoldenLibrary.Load();
        var kitchen = await Kitchen.OpenAsync(postgres);
        var ids = await golden.SeedAsync(kitchen);
        var recipeId = ids["Spaghetti Bolognese"];

        // Act
        List<string> walked = [];
        string? cursor = null;

        do
        {
            var page = await PageAsync(kitchen.Client, $"/api/v1/recipes/{recipeId}/related?limit=3{(cursor is null ? string.Empty : $"&cursor={cursor}")}");
            var items = page.GetProperty("items").EnumerateArray().Select(Title).ToList();

            // A page is only ever short at the end of the shelf.
            cursor = page.TryGetProperty("nextCursor", out var next) && next.ValueKind == JsonValueKind.String
                ? next.GetString()
                : null;
            Assert.True(cursor is null || items.Count == 3, string.Join(" · ", items));
            walked.AddRange(items);
        }
        while (cursor is not null);

        var once = (await PageAsync(kitchen.Client, $"/api/v1/recipes/{recipeId}/related?limit=12"))
            .GetProperty("items").EnumerateArray().Select(Title).ToList();

        // Assert
        Assert.True(walked.Count > 3, string.Join(" · ", walked));
        Assert.Equal(walked.Count, walked.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(once, walked.Take(once.Count));
    }

    [Fact]
    public async Task Related_ShouldSayNotFound_ForARecipeInAnotherHousehold()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var recipeId = await kitchen.SaveAsync(
            "Spaghetti Bolognese", "de", 15, 45, [("Hackfleisch", "g")], ["pasta"], "Anbraten.");
        using var stranger = await Kitchen.StrangerAsync(postgres);

        // Act
        var response = await stranger.GetAsync($"/api/v1/recipes/{recipeId}/related", Token);

        // Assert
        // Never a 403, and never the neighbour's recipes: that would confirm
        // the recipe exists and say what else is in that kitchen.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("recipes.not_found", response.ProblemCode);
    }

    private static async Task<List<JsonElement>> RelatedAsync(ApiClient client, Guid recipeId) =>
        [.. (await PageAsync(client, $"/api/v1/recipes/{recipeId}/related")).GetProperty("items").EnumerateArray()];

    private static async Task<JsonElement> PageAsync(ApiClient client, string path)
    {
        var response = await client.GetAsync(path, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return response.Json!.Value;
    }

    private static string Title(JsonElement related) => related.GetProperty("title").GetString()!;

    private static List<string> Shared(JsonElement related) =>
        [.. related.GetProperty("reason").GetProperty("shared").EnumerateArray().Select(one => one.GetString()!)];

    private static string Report(Dictionary<string, List<JsonElement>> answers)
    {
        var report = new StringBuilder("Related recipes over the golden library:\n");

        foreach (var (title, related) in answers)
        {
            report.AppendLine(CultureInfo.InvariantCulture, $"\n  {title}");

            foreach (var one in related)
            {
                var reason = one.GetProperty("reason");
                report.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"    → {Title(one)}  [{reason.GetProperty("kind").GetString()}: {string.Join(", ", Shared(one))}]");
            }
        }

        return report.ToString();
    }
}
