using System.Globalization;
using System.Net;
using System.Text.Json;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Shopping;

/// <summary>
/// The merge rule, end to end: three recipes must not give three lines of butter.
/// </summary>
[Collection(RequiresDatabase.Name)]
public class ShoppingListEndpointTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_ShouldCreateTheListOnFirstLook()
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        var response = await client.GetAsync(
            $"/api/v1/households/{householdId}/shopping-list",
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(response.Json!.Value.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Get_ShouldNotExistForSomebodyElsesHousehold()
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        using var stranger = await SecondAccountAsync();

        var response = await stranger.GetAsync(
            $"/api/v1/households/{householdId}/shopping-list",
            Token);

        // 404, not 403: a stranger learns nothing about which households exist.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddRecipe_ShouldMergeTheSameIngredientAcrossRecipes()
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        var first = await RecipeWithButterAsync(client, householdId, "Cake", 200);
        var second = await RecipeWithButterAsync(client, householdId, "Biscuits", 50);

        await client.PostAsync(
            $"/api/v1/households/{householdId}/shopping-list/recipes",
            new { recipeId = first, servings = 4 },
            Token);

        var response = await client.PostAsync(
            $"/api/v1/households/{householdId}/shopping-list/recipes",
            new { recipeId = second, servings = 4 },
            Token);

        var items = response.Json!.Value.GetProperty("items").EnumerateArray().ToList();
        var butter = Assert.Single(items, item => item.GetProperty("name").GetString() == "Butter");

        Assert.Equal(250m, butter.GetProperty("quantity").GetDecimal());
        Assert.Equal("g", butter.GetProperty("unit").GetString());
    }

    [Fact]
    public async Task AddPlannedMeals_ShouldExposeEveryRecipesAmountAndDay()
    {
        // One line, two reasons: the response keeps both so a merged amount can say what it is for.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var cake = await RecipeWithButterAsync(client, householdId, "Cake", 200);
        var biscuits = await RecipeWithButterAsync(client, householdId, "Biscuits", 50);

        await PlanAsync(client, householdId, cake, Monday);
        await PlanAsync(client, householdId, biscuits, Monday.AddDays(1));

        await AddWeekAsync(client, householdId);
        var response = await client.GetAsync(
            $"/api/v1/households/{householdId}/shopping-list",
            Token);

        var sources = Butter(response).GetProperty("sources").EnumerateArray()
            .OrderBy(source => source.GetProperty("recipeTitle").GetString())
            .ToList();

        Assert.Collection(
            sources,
            source =>
            {
                Assert.Equal("Biscuits", source.GetProperty("recipeTitle").GetString());
                Assert.Equal(50m, source.GetProperty("quantity").GetDecimal());
                Assert.Equal("g", source.GetProperty("unit").GetString());
                Assert.Equal("2026-09-15", source.GetProperty("plannedDate").GetString());
                Assert.Equal("dinner", source.GetProperty("plannedSlot").GetString());
            },
            source =>
            {
                Assert.Equal("Cake", source.GetProperty("recipeTitle").GetString());
                Assert.Equal(200m, source.GetProperty("quantity").GetDecimal());
                Assert.Equal("g", source.GetProperty("unit").GetString());
                Assert.Equal("2026-09-14", source.GetProperty("plannedDate").GetString());
                Assert.Equal("dinner", source.GetProperty("plannedSlot").GetString());
            });
    }

    [Fact]
    public async Task AddRecipe_Twice_ShouldContributeTwice()
    {
        // A recipe added twice is made twice (double batch), so two asks mean twice the shopping. A
        // planned week is different: it knows which meals are already on the list.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var recipeId = await RecipeWithButterAsync(client, householdId, "Cake", 200);

        await client.PostAsync(
            $"/api/v1/households/{householdId}/shopping-list/recipes",
            new { recipeId, servings = 4 },
            Token);

        var response = await client.PostAsync(
            $"/api/v1/households/{householdId}/shopping-list/recipes",
            new { recipeId, servings = 4 },
            Token);

        var items = response.Json!.Value.GetProperty("items").EnumerateArray().ToList();
        var butter = Assert.Single(items, item => item.GetProperty("name").GetString() == "Butter");

        Assert.Equal(400m, butter.GetProperty("quantity").GetDecimal());
    }

    [Fact]
    public async Task AddRecipe_Twice_AtDifferentServings_ShouldSumBoth()
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var recipeId = await RecipeWithButterAsync(client, householdId, "Cake", 200);

        await client.PostAsync(
            $"/api/v1/households/{householdId}/shopping-list/recipes",
            new { recipeId, servings = 4 },
            Token);

        var response = await client.PostAsync(
            $"/api/v1/households/{householdId}/shopping-list/recipes",
            new { recipeId, servings = 6 },
            Token);

        var items = response.Json!.Value.GetProperty("items").EnumerateArray().ToList();
        var butter = Assert.Single(items, item => item.GetProperty("name").GetString() == "Butter");

        Assert.Equal(500m, butter.GetProperty("quantity").GetDecimal());
    }

    [Fact]
    public async Task AddRecipe_ShouldNotTouchALineAlreadyInTheTrolley()
    {
        // A ticked line is bought: a second add starts a new line rather than change a picked-up
        // amount.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var recipeId = await RecipeWithButterAsync(client, householdId, "Cake", 200);

        var first = await client.PostAsync(
            $"/api/v1/households/{householdId}/shopping-list/recipes",
            new { recipeId, servings = 4 },
            Token);

        var itemId = first.Json!.Value.GetProperty("items")[0].GetProperty("itemId").GetGuid();

        await client.PatchAsync(
            $"/api/v1/households/{householdId}/shopping-list/items/{itemId}",
            new { isChecked = true },
            Token);

        var response = await client.PostAsync(
            $"/api/v1/households/{householdId}/shopping-list/recipes",
            new { recipeId, servings = 4 },
            Token);

        var butter = response.Json!.Value.GetProperty("items").EnumerateArray()
            .Where(item => item.GetProperty("name").GetString() == "Butter")
            .ToList();

        Assert.Equal(2, butter.Count);
        Assert.Equal(200m, butter.Single(i => i.GetProperty("isChecked").GetBoolean())
            .GetProperty("quantity").GetDecimal());
        Assert.Equal(200m, butter.Single(i => !i.GetProperty("isChecked").GetBoolean())
            .GetProperty("quantity").GetDecimal());
    }

    [Fact]
    public async Task AddRecipe_ShouldScaleToWhatIsBeingCooked()
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var recipeId = await RecipeWithButterAsync(client, householdId, "Cake", 200);

        var response = await client.PostAsync(
            $"/api/v1/households/{householdId}/shopping-list/recipes",
            new { recipeId, servings = 6 },
            Token);

        var butter = response.Json!.Value.GetProperty("items")[0];

        // Unrounded on purpose: rounding here and summing later compounds error.
        Assert.Equal(300m, butter.GetProperty("quantity").GetDecimal());
    }

    [Fact]
    public async Task AddItem_ShouldGuessWhereItIsFound()
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        var response = await client.PostAsync(
            $"/api/v1/households/{householdId}/shopping-list/items",
            new { name = "Tomaten" },
            Token);

        Assert.Equal("produce", response.Json!.Value.GetProperty("items")[0].GetProperty("section").GetString());
    }

    [Fact]
    public async Task UpdateItem_ShouldRememberACorrectedSection()
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        var added = await client.PostAsync(
            $"/api/v1/households/{householdId}/shopping-list/items",
            new { name = "Wunderpulver" },
            Token);

        var itemId = added.Json!.Value.GetProperty("items")[0].GetProperty("itemId").GetGuid();

        await client.PatchAsync(
            $"/api/v1/households/{householdId}/shopping-list/items/{itemId}",
            new { section = "spices_baking" },
            Token);

        var again = await client.PostAsync(
            $"/api/v1/households/{householdId}/shopping-list/items",
            new { name = "wunderpulver" },
            Token);

        // Moving an item once teaches the household where it lives.
        var sections = again.Json!.Value.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("section").GetString())
            .ToList();

        Assert.All(sections, section => Assert.Equal("spices_baking", section));
    }

    [Fact]
    public async Task RemoveItems_ShouldClearOnlyWhatIsBought()
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        await client.PostAsync(
            $"/api/v1/households/{householdId}/shopping-list/items",
            new { name = "Tomaten" },
            Token);

        var second = await client.PostAsync(
            $"/api/v1/households/{householdId}/shopping-list/items",
            new { name = "Mehl" },
            Token);

        var itemId = second.Json!.Value.GetProperty("items")[0].GetProperty("itemId").GetGuid();

        await client.PatchAsync(
            $"/api/v1/households/{householdId}/shopping-list/items/{itemId}",
            new { isChecked = true },
            Token);

        var response = await client.DeleteAsync(
            $"/api/v1/households/{householdId}/shopping-list/items",
            Token);

        var remaining = response.Json!.Value.GetProperty("items").EnumerateArray().ToList();

        Assert.Single(remaining);
        Assert.False(remaining[0].GetProperty("isChecked").GetBoolean());
    }

    [Theory]
    [InlineData("nope")]
    [InlineData("")]
    public async Task RemoveItems_ShouldRefuseAMalformedItemId_AndKeepWhatIsBought(string itemId)
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        var added = await client.PostAsync(
            $"/api/v1/households/{householdId}/shopping-list/items",
            new { name = "Mehl" },
            Token);

        var checkedId = added.Json!.Value.GetProperty("items")[0].GetProperty("itemId").GetGuid();

        await client.PatchAsync(
            $"/api/v1/households/{householdId}/shopping-list/items/{checkedId}",
            new { isChecked = true },
            Token);

        var response = await client.DeleteAsync(
            $"/api/v1/households/{householdId}/shopping-list/items?itemId={itemId}",
            Token);

        // Read as "no id", a bad one cleared every ticked line instead of one.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var list = await client.GetAsync(
            $"/api/v1/households/{householdId}/shopping-list",
            Token);

        Assert.Single(list.Json!.Value.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task AddRecipe_ShouldKeepTheAmountSomebodyScaledTo()
    {
        // Scaling to 370 g gives a non-round yield (7.4 servings); rounding it would put 380 on the
        // list.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var recipeId = await RecipeWithButterAsync(client, householdId, "Bread", 200);

        var response = await client.PostAsync(
            $"/api/v1/households/{householdId}/shopping-list/recipes",
            new { recipeId, servings = 7.4m },
            Token);

        var butter = response.Json!.Value.GetProperty("items")[0];

        Assert.Equal(370m, butter.GetProperty("quantity").GetDecimal());
    }

    [Fact]
    public async Task AddItem_ShouldNotRejectSomebodyElseAddingAtTheSameMoment()
    {
        // Concurrent adds to one list row used to 412 the second writer and silently lose their
        // item.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        var names = new[] { "Butter", "Mehl", "Zucker", "Eier", "Milch" };

        var responses = await Task.WhenAll(names.Select(name => client.PostAsync(
            $"/api/v1/households/{householdId}/shopping-list/items",
            new { name },
            Token)));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));

        var stored = await client.GetAsync(
            $"/api/v1/households/{householdId}/shopping-list",
            Token);

        var onTheList = stored.Json!.Value.GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("name").GetString())
            .ToList();

        Assert.Equal(names.Length, onTheList.Count);
        Assert.All(names, name => Assert.Contains(name, onTheList));
    }

    [Fact]
    public async Task AddPlannedMeals_Twice_ShouldShopForTheWeekOnce()
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var recipeId = await RecipeWithButterAsync(client, householdId, "Cake", 200);

        await PlanAsync(client, householdId, recipeId, Monday);

        await AddWeekAsync(client, householdId);
        var response = await AddWeekAsync(client, householdId);

        // Pressing the button again is checking, not shopping twice.
        Assert.Equal(200m, Butter(response).GetProperty("quantity").GetDecimal());
    }

    [Fact]
    public async Task AddPlannedMeals_ShouldCountARecipeAlreadyAddedFromItsPage()
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var recipeId = await RecipeWithButterAsync(client, householdId, "Waffles", 200);

        await client.PostAsync(
            $"/api/v1/households/{householdId}/shopping-list/recipes",
            new { recipeId, servings = 4 },
            Token);
        await PlanAsync(client, householdId, recipeId, Monday);

        var response = await AddWeekAsync(client, householdId);

        // Monday is shopped for once; doubling every ingredient was a silent bug.
        Assert.Equal(200m, Butter(response).GetProperty("quantity").GetDecimal());
        Assert.True(Assert.Single(await WeekAsync(client, householdId)).GetProperty("isOnShoppingList").GetBoolean());
    }

    [Fact]
    public async Task AddPlannedMeals_ShouldShopForEveryMealOfARecipePlannedTwice()
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var recipeId = await RecipeWithButterAsync(client, householdId, "Cake", 200);

        await PlanAsync(client, householdId, recipeId, Monday);
        await PlanAsync(client, householdId, recipeId, Monday.AddDays(3), servings: 6);

        var response = await AddWeekAsync(client, householdId);

        Assert.Equal(500m, Butter(response).GetProperty("quantity").GetDecimal());
    }

    [Fact]
    public async Task AddPlannedMeals_ShouldSayWhichMealsAreOnTheList()
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var recipeId = await RecipeWithButterAsync(client, householdId, "Cake", 200);

        await PlanAsync(client, householdId, recipeId, Monday);

        var before = Assert.Single(await WeekAsync(client, householdId));

        await AddWeekAsync(client, householdId);

        var after = Assert.Single(await WeekAsync(client, householdId));

        Assert.False(before.GetProperty("isOnShoppingList").GetBoolean());
        Assert.True(after.GetProperty("isOnShoppingList").GetBoolean());
    }

    [Fact]
    public async Task WithdrawPlannedMeal_ShouldTakeBackExactlyWhatThatMealAdded()
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var cake = await RecipeWithButterAsync(client, householdId, "Cake", 200);
        var biscuits = await RecipeWithButterAsync(client, householdId, "Biscuits", 50);

        var monday = await PlanAsync(client, householdId, cake, Monday);
        await PlanAsync(client, householdId, biscuits, Monday.AddDays(1));
        await AddWeekAsync(client, householdId);

        await client.DeleteAsync($"/api/v1/households/{householdId}/meal-plan/{monday}", Token);

        var response = await client.DeleteAsync(
            $"/api/v1/households/{householdId}/shopping-list/meals/{monday}",
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(50m, Butter(response).GetProperty("quantity").GetDecimal());
    }

    [Fact]
    public async Task WithdrawPlannedMeal_ShouldKeepWhatSomebodyTyped()
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var recipeId = await RecipeWithButterAsync(client, householdId, "Cake", 200);

        await client.PostAsync(
            $"/api/v1/households/{householdId}/shopping-list/items",
            new { name = "Limes", quantity = 2 },
            Token);

        var entryId = await PlanAsync(client, householdId, recipeId, Monday);
        await AddWeekAsync(client, householdId);

        var response = await client.DeleteAsync(
            $"/api/v1/households/{householdId}/shopping-list/meals/{entryId}",
            Token);

        var names = response.Json!.Value.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("name").GetString());

        Assert.Equal(["Limes"], names);
    }

    [Fact]
    public async Task Get_ShouldNotAnswerNotModifiedOverARenamedRecipe()
    {
        // Renaming a recipe does not write to the list, so the list's version alone cannot show the
        // change.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var recipeId = await RecipeWithButterAsync(client, householdId, "Cake", 200);

        await client.PostAsync(
            $"/api/v1/households/{householdId}/shopping-list/recipes",
            new { recipeId, servings = 4 },
            Token);
        var before = await client.GetAsync($"/api/v1/households/{householdId}/shopping-list", Token);

        await SaveWithButterAsync(client, recipeId, "Birthday cake", 200);
        var after = await RevalidateAsync(client, householdId, before);

        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        Assert.Equal(
            "Birthday cake",
            Butter(after).GetProperty("sources")[0].GetProperty("recipeTitle").GetString());
    }

    [Fact]
    public async Task Get_ShouldNotAnswerNotModifiedOverAMovedMeal()
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var recipeId = await RecipeWithButterAsync(client, householdId, "Cake", 200);
        var entryId = await PlanAsync(client, householdId, recipeId, Monday);

        await AddWeekAsync(client, householdId);
        var before = await client.GetAsync($"/api/v1/households/{householdId}/shopping-list", Token);

        await client.PatchAsync(
            $"/api/v1/households/{householdId}/meal-plan/{entryId}",
            new { date = Monday.AddDays(2) },
            Token);
        var after = await RevalidateAsync(client, householdId, before);

        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        Assert.Equal(
            "2026-09-16",
            Butter(after).GetProperty("sources")[0].GetProperty("plannedDate").GetString());
    }

    [Fact]
    public async Task Get_ShouldAnswerNotModifiedWhenNothingChanged()
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var recipeId = await RecipeWithButterAsync(client, householdId, "Cake", 200);

        await client.PostAsync(
            $"/api/v1/households/{householdId}/shopping-list/recipes",
            new { recipeId, servings = 4 },
            Token);
        var before = await client.GetAsync($"/api/v1/households/{householdId}/shopping-list", Token);

        var after = await RevalidateAsync(client, householdId, before);

        Assert.Equal(HttpStatusCode.NotModified, after.StatusCode);
    }

    private static readonly DateOnly Monday = new(2026, 9, 14);

    private static JsonElement Butter(ApiResponse response) =>
        Assert.Single(
            response.Json!.Value.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("name").GetString() == "Butter");

    private static async Task<Guid> PlanAsync(
        ApiClient client,
        Guid householdId,
        Guid recipeId,
        DateOnly date,
        decimal? servings = null)
    {
        var planned = await client.PostAsync(
            $"/api/v1/households/{householdId}/meal-plan",
            new { date, recipeId, servings },
            Token);

        return planned.Json!.Value.GetProperty("days").EnumerateArray()
            .Single(day => day.GetProperty("date").GetString() == date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            .GetProperty("meals").EnumerateArray()
            .Last()
            .GetProperty("entryId").GetGuid();
    }

    private static Task<ApiResponse> AddWeekAsync(ApiClient client, Guid householdId) =>
        client.PostAsync(
            $"/api/v1/households/{householdId}/shopping-list/meals",
            new { from = Monday },
            Token);

    private static async Task<IReadOnlyList<JsonElement>> WeekAsync(ApiClient client, Guid householdId)
    {
        var week = await client.GetAsync(
            $"/api/v1/households/{householdId}/meal-plan?from={Monday:yyyy-MM-dd}",
            Token);

        return [.. week.Json!.Value.GetProperty("days").EnumerateArray()
            .SelectMany(day => day.GetProperty("meals").EnumerateArray())];
    }

    private static async Task<Guid> HouseholdAsync(ApiClient client)
    {
        var me = await client.GetAsync("/api/v1/users/me", Token);

        return me.Json!.Value.GetProperty("households")[0].GetProperty("householdId").GetGuid();
    }

    private static Task<ApiResponse> RevalidateAsync(ApiClient client, Guid householdId, ApiResponse before)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/households/{householdId}/shopping-list");
        request.Headers.IfNoneMatch.Add(
            System.Net.Http.Headers.EntityTagHeaderValue.Parse(before.ETag!));

        return client.SendAsync(request, Token);
    }

    private static async Task<Guid> RecipeWithButterAsync(
        ApiClient client,
        Guid householdId,
        string title,
        decimal grams)
    {
        var created = await client.PostAsync(
            "/api/v1/recipes",
            new { householdId, title },
            Token);

        var recipeId = created.Json!.Value.GetProperty("recipeId").GetGuid();
        await SaveWithButterAsync(client, recipeId, title, grams);

        return recipeId;
    }

    private static async Task SaveWithButterAsync(
        ApiClient client,
        Guid recipeId,
        string title,
        decimal grams)
    {
        var read = await client.GetAsync($"/api/v1/recipes/{recipeId}", Token);

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/recipes/{recipeId}")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new
            {
                title,
                language = "en",
                yieldAmount = 4,
                yieldKind = "servings",
                groups = new[]
                {
                    new
                    {
                        ingredients = new[] { new { quantity = grams, unit = "g", name = "Butter" } }
                    }
                },
                steps = Array.Empty<object>(),
                tags = Array.Empty<string>()
            })
        };

        request.Headers.IfMatch.Add(
            System.Net.Http.Headers.EntityTagHeaderValue.Parse(read.ETag!));

        await client.SendAsync(request, Token);
    }

    private async Task<ApiClient> SignedInAsync()
    {
        await postgres.ResetAsync(Token);

        var client = postgres.Api.NewApiClient();

        await client.PostAsync(
            "/api/v1/users",
            new { email = "ada@example.com", displayName = "Ada", password = Password },
            Token);
        await client.PostAsync(
            "/api/v1/sessions",
            new { email = "ada@example.com", password = Password },
            Token);

        return client;
    }

    private async Task<ApiClient> SecondAccountAsync()
    {
        using var admin = postgres.Api.NewApiClient();

        await admin.PostAsync(
            "/api/v1/sessions",
            new { email = "ada@example.com", password = Password },
            Token);
        await admin.PutAsync(
            "/api/v1/settings/registration",
            new { openRegistration = true, requireInvitation = false, maxUsers = 100 },
            Token);

        var client = postgres.Api.NewApiClient();

        await client.PostAsync(
            "/api/v1/users",
            new { email = "grace@example.com", displayName = "Grace", password = Password },
            Token);
        await client.PostAsync(
            "/api/v1/sessions",
            new { email = "grace@example.com", password = Password },
            Token);

        return client;
    }
}
