using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Nutrition;

[Collection(RequiresDatabase.Name)]
public class CalorieLibraryTests(PostgresFixture postgres)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CardsAndFilter_ShouldUseTheDetailCalculation_AcrossPagesAndUncertainRecipes()
    {
        var kitchen = await Kitchen.OpenAsync(postgres);
        var low = await RecipeAsync(kitchen, "A", 100m);
        var medium = await RecipeAsync(kitchen, "B", 300m);
        var high = await RecipeAsync(kitchen, "C", 900m);
        var partial = await RecipeAsync(kitchen, "D", 100m, unknown: true);
        var unknown = (await kitchen.Client.PostAsync("/api/v1/recipes",
            new { householdId = kitchen.HouseholdId, title = "E" }, Token)).Json!.Value.GetProperty("recipeId").GetGuid();

        var browse = await kitchen.Client.GetAsync($"/api/v1/recipes?householdId={kitchen.HouseholdId}", Token);
        Assert.Equal(HttpStatusCode.OK, browse.StatusCode);
        foreach (var id in new[] { low, medium, high, partial })
        {
            var detail = await kitchen.Client.GetAsync($"/api/v1/recipes/{id}/nutrition", Token);
            var card = Items(browse).Single(item => item.GetProperty("recipeId").GetGuid() == id);
            Assert.Equal(detail.Json!.Value.GetProperty("values").GetProperty("energyKcal").GetRawText(),
                card.GetProperty("calories").GetRawText());
        }
        var suggestions = await kitchen.Client.GetAsync($"/api/v1/suggestions?householdId={kitchen.HouseholdId}", Token);
        Assert.Equal(HttpStatusCode.OK, suggestions.StatusCode);
        Assert.NotEmpty(Items(suggestions));
        foreach (var card in Items(suggestions))
        {
            var original = Items(browse).Single(item => item.GetProperty("recipeId").GetGuid() == card.GetProperty("recipeId").GetGuid());
            var known = original.TryGetProperty("calories", out var expected);
            Assert.Equal(known, card.TryGetProperty("calories", out var actual));
            if (known)
            {
                Assert.Equal(expected.GetRawText(), actual.GetRawText());
            }
        }
        var empty = Items(browse).Single(item => item.GetProperty("recipeId").GetGuid() == unknown);
        Assert.True(!empty.TryGetProperty("calories", out var calories) || calories.ValueKind == JsonValueKind.Null);

        var path = $"/api/v1/recipes?householdId={kitchen.HouseholdId}&maxKcal=300&sort=title&limit=1";
        var first = await kitchen.Client.GetAsync(path, Token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(2, first.Json!.Value.GetProperty("total").GetInt32());
        Assert.Equal(low, Assert.Single(Items(first)).GetProperty("recipeId").GetGuid());
        var cursor = first.Json.Value.GetProperty("nextCursor").GetString();
        Assert.False(string.IsNullOrEmpty(cursor));
        var second = await kitchen.Client.GetAsync($"{path}&cursor={Uri.EscapeDataString(cursor!)}", Token);
        Assert.Equal(medium, Assert.Single(Items(second)).GetProperty("recipeId").GetGuid());
        Assert.Equal(2, second.Json!.Value.GetProperty("total").GetInt32());
        var combined = await kitchen.Client.GetAsync($"/api/v1/recipes?householdId={kitchen.HouseholdId}&maxKcal=300&maxMinutes=30&query=Mehl&sort=relevance", Token);
        Assert.Equal(HttpStatusCode.OK, combined.StatusCode);
        Assert.Equal(2, combined.Json!.Value.GetProperty("total").GetInt32());

    }

    [Fact]
    public async Task Filter_ShouldFollowTheReadingHouseholdsCorrections_AndRecipeEdits()
    {
        var kitchen = await Kitchen.OpenAsync(postgres);
        var id = await RecipeAsync(kitchen, "Flour", 100m);
        var inherited = (await kitchen.Client.PostAsync("/api/v1/households",
            new { name = "Other kitchen", inheritsFrom = kitchen.HouseholdId }, Token))
            .Json!.Value.GetProperty("householdId").GetGuid();
        var correction = await kitchen.Client.PutAsync($"/api/v1/households/{inherited}/ingredients/Mehl",
            new { food = "Q120000" }, Token);
        Assert.Equal(HttpStatusCode.NoContent, correction.StatusCode);

        var own = await kitchen.Client.GetAsync($"/api/v1/recipes?householdId={kitchen.HouseholdId}&maxKcal=100", Token);
        var heir = await kitchen.Client.GetAsync($"/api/v1/recipes?householdId={inherited}&maxKcal=100", Token);
        Assert.Equal(id, Assert.Single(Items(own)).GetProperty("recipeId").GetGuid());
        Assert.Empty(Items(heir));

        await SaveAsync(kitchen, id, "Flour", 900m, false);
        var edited = await kitchen.Client.GetAsync($"/api/v1/recipes?householdId={kitchen.HouseholdId}&maxKcal=100", Token);
        Assert.Empty(Items(edited));
    }

    [Fact]
    public async Task SavedSearch_ShouldRoundTripACalorieOnlyFilter_AndRejectInvalidLimits()
    {
        var kitchen = await Kitchen.OpenAsync(postgres);
        var saved = await kitchen.Client.PostAsync("/api/v1/searches",
            new { householdId = kitchen.HouseholdId, name = "Light meals", criteria = new { maxKcal = 500 } }, Token);
        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        Assert.Equal(500, saved.Json!.Value.GetProperty("criteria").GetProperty("maxKcal").GetInt32());
        var read = await kitchen.Client.GetAsync($"/api/v1/searches?householdId={kitchen.HouseholdId}", Token);
        Assert.Equal(500, read.Json!.Value.GetProperty("items")[0].GetProperty("criteria").GetProperty("maxKcal").GetInt32());
        foreach (var invalid in new[] { "0", "-1", "100001", "abc" })
        {
            var response = await kitchen.Client.GetAsync($"/api/v1/recipes?householdId={kitchen.HouseholdId}&maxKcal={invalid}", Token);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    private static JsonElement[] Items(ApiResponse response) => [.. response.Json!.Value.GetProperty("items").EnumerateArray()];

    private static async Task<Guid> RecipeAsync(Kitchen kitchen, string title, decimal grams, bool unknown = false)
    {
        var created = await kitchen.Client.PostAsync("/api/v1/recipes",
            new { householdId = kitchen.HouseholdId, title }, Token);
        var id = created.Json!.Value.GetProperty("recipeId").GetGuid();
        await SaveAsync(kitchen, id, title, grams, unknown);
        return id;
    }

    private static async Task SaveAsync(Kitchen kitchen, Guid id, string title, decimal grams, bool unknown)
    {
        var read = await kitchen.Client.GetAsync($"/api/v1/recipes/{id}", Token);
        var ingredients = new List<object> { new { name = "Mehl", quantity = grams, unit = "g" } };
        if (unknown)
        {
            ingredients.Add(new { name = "Unbekannte Mischung", quantity = 100, unit = "g" });
        }
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/recipes/{id}")
        {
            Content = JsonContent.Create(new
            {
                title,
                prepMinutes = 10,
                cookMinutes = 20,
                language = "de",
                yieldAmount = 4,
                yieldKind = "servings",
                groups = new[] { new { ingredients } },
                steps = Array.Empty<object>(),
                tags = Array.Empty<string>()
            })
        };
        request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(read.ETag!));
        Assert.Equal(HttpStatusCode.OK, (await kitchen.Client.SendAsync(request, Token)).StatusCode);
    }
}
