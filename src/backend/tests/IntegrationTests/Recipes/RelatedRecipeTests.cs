using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using IntegrationTests.Fixtures;
using IntegrationTests.Recipes.Evaluation;

namespace IntegrationTests.Recipes;

/// <summary>"Recipes like this one" against the golden library; every result must carry a reason.</summary>
[Collection(RequiresDatabase.Name)]
public class RelatedRecipeTests(PostgresFixture postgres)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Related_ShouldOfferAtLeastThreeWithAReason_ForEveryRecipeOfTheGoldenLibrary()
    {
        var golden = GoldenLibrary.Load();
        var kitchen = await Kitchen.OpenAsync(postgres);
        var ids = await golden.SeedAsync(kitchen);

        var answers = new Dictionary<string, List<JsonElement>>(StringComparer.Ordinal);

        foreach (var (title, recipeId) in ids)
        {
            answers[title] = await RelatedAsync(kitchen.Client, recipeId);
        }

        var report = Report(answers);
        TestContext.Current.SendDiagnosticMessage(report);

        Assert.All(answers, answer =>
        {
            Assert.True(answer.Value.Count >= 3, $"{answer.Key} has {answer.Value.Count} related.\n{report}");
            Assert.DoesNotContain(answer.Value, one => Title(one) == answer.Key);
            Assert.All(answer.Value, one => Assert.NotEmpty(Shared(one)));
        });
    }

    /// <summary>What a recipe is counts for more than what it shares: Bolognese gets Lasagne, not the Chili with the same tins.</summary>
    [Fact]
    public async Task Related_ShouldRankWhatARecipeIsAboveWhatItIsMadeFrom()
    {
        var golden = GoldenLibrary.Load();
        var kitchen = await Kitchen.OpenAsync(postgres);
        var ids = await golden.SeedAsync(kitchen);

        var related = (await RelatedAsync(kitchen.Client, ids["Spaghetti Bolognese"])).Select(Title).ToList();

        Assert.Contains("Lasagne Bolognese", related);
        var chili = related.IndexOf("Chili con Carne");
        Assert.True(
            chili < 0 || related.IndexOf("Lasagne Bolognese") < chili,
            string.Join(" · ", related));
    }

    [Fact]
    public async Task Related_ShouldWalkEveryPageOnceAndInOrder_WhenFollowingTheCursor()
    {
        var golden = GoldenLibrary.Load();
        var kitchen = await Kitchen.OpenAsync(postgres);
        var ids = await golden.SeedAsync(kitchen);
        var recipeId = ids["Spaghetti Bolognese"];

        List<string> walked = [];
        string? cursor = null;

        do
        {
            var page = await PageAsync(kitchen.Client, $"/api/v1/recipes/{recipeId}/related?limit=3{(cursor is null ? string.Empty : $"&cursor={cursor}")}");
            var items = page.GetProperty("items").EnumerateArray().Select(Title).ToList();

            cursor = page.TryGetProperty("nextCursor", out var next) && next.ValueKind == JsonValueKind.String
                ? next.GetString()
                : null;
            Assert.True(cursor is null || items.Count == 3, string.Join(" · ", items));
            walked.AddRange(items);
        }
        while (cursor is not null);

        var once = (await PageAsync(kitchen.Client, $"/api/v1/recipes/{recipeId}/related?limit=12"))
            .GetProperty("items").EnumerateArray().Select(Title).ToList();

        Assert.True(walked.Count > 3, string.Join(" · ", walked));
        Assert.Equal(walked.Count, walked.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(once, walked.Take(once.Count));
    }

    [Fact]
    public async Task Related_ShouldSayNotFound_ForARecipeInAnotherHousehold()
    {
        var kitchen = await Kitchen.OpenAsync(postgres);
        var recipeId = await kitchen.SaveAsync(
            "Spaghetti Bolognese", "de", 15, 45, [("Hackfleisch", "g")], ["pasta"], "Anbraten.");
        using var stranger = await Kitchen.StrangerAsync(postgres);

        var response = await stranger.GetAsync($"/api/v1/recipes/{recipeId}/related", Token);

        // Never a 403, which would confirm the recipe exists.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("recipes.not_found", response.ProblemCode);
    }

    [Fact]
    public async Task Related_ShouldAnswer_WhenACursorsTimeCarriesAnOffset()
    {
        // Cursors are UTC and the driver refuses non-UTC timestamptz; a +02:00 one is the same instant.
        var kitchen = await Kitchen.OpenAsync(postgres);
        var recipeId = await kitchen.SaveAsync(
            "Spaghetti Bolognese", "de", 15, 45, [("Hackfleisch", "g")], ["pasta"], "Anbraten.");
        await kitchen.SaveAsync(
            "Lasagne", "de", 30, 60, [("Hackfleisch", "g")], ["pasta"], "Schichten.");
        var cursor = System.Buffers.Text.Base64Url.EncodeToString(Encoding.UTF8.GetBytes(
            $$"""{"Score":1,"UpdatedAt":"2999-01-01T00:00:00+02:00","Id":"{{Guid.Empty}}"}"""));

        var response = await kitchen.Client.GetAsync(
            $"/api/v1/recipes/{recipeId}/related?cursor={cursor}",
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
