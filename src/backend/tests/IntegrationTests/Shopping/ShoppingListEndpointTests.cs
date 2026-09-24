using System.Globalization;
using System.Net;
using System.Text.Json;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Shopping;

/// <summary>
/// The merge rule, end to end.
/// </summary>
/// <remarks>
/// Adding three recipes and getting three lines of butter is how a shopping
/// list stops being worth carrying into a shop.
/// </remarks>
[Collection(RequiresDatabase.Name)]
public class ShoppingListEndpointTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_ShouldCreateTheListOnFirstLook()
    {
        // Arrange
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        // Act
        var response = await client.GetAsync(
            $"/api/v1/households/{householdId}/shopping-list",
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(response.Json!.Value.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Get_ShouldNotExistForSomebodyElsesHousehold()
    {
        // Arrange
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        using var stranger = await SecondAccountAsync();

        // Act
        var response = await stranger.GetAsync(
            $"/api/v1/households/{householdId}/shopping-list",
            Token);

        // Assert
        // 404, not 403: a stranger learns nothing about which households exist.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddRecipe_ShouldMergeTheSameIngredientAcrossRecipes()
    {
        // Arrange
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        var first = await RecipeWithButterAsync(client, householdId, "Cake", 200);
        var second = await RecipeWithButterAsync(client, householdId, "Biscuits", 50);

        // Act
        await client.PostAsync(
            $"/api/v1/households/{householdId}/shopping-list/recipes",
            new { recipeId = first, servings = 4 },
            Token);

        var response = await client.PostAsync(
            $"/api/v1/households/{householdId}/shopping-list/recipes",
            new { recipeId = second, servings = 4 },
            Token);

        // Assert
        var items = response.Json!.Value.GetProperty("items").EnumerateArray().ToList();
        var butter = Assert.Single(items, item => item.GetProperty("name").GetString() == "Butter");

        Assert.Equal(250m, butter.GetProperty("quantity").GetDecimal());
        Assert.Equal("g", butter.GetProperty("unit").GetString());
    }

    [Fact]
    public async Task AddRecipe_Twice_ShouldContributeTwice()
    {
        // Arrange
        // A recipe put on the list by itself twice is somebody making it twice
        // — a double batch, or a second shelf of the same cookbook. Two asks is
        // twice the shopping; a list that answered with one would send somebody
        // home short. (A planned week is not repeated this way: it knows which
        // of its meals are already here.)
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var recipeId = await RecipeWithButterAsync(client, householdId, "Cake", 200);

        // Act
        await client.PostAsync(
            $"/api/v1/households/{householdId}/shopping-list/recipes",
            new { recipeId, servings = 4 },
            Token);

        var response = await client.PostAsync(
            $"/api/v1/households/{householdId}/shopping-list/recipes",
            new { recipeId, servings = 4 },
            Token);

        // Assert
        var items = response.Json!.Value.GetProperty("items").EnumerateArray().ToList();
        var butter = Assert.Single(items, item => item.GetProperty("name").GetString() == "Butter");

        Assert.Equal(400m, butter.GetProperty("quantity").GetDecimal());
    }

    [Fact]
    public async Task AddRecipe_Twice_AtDifferentServings_ShouldSumBoth()
    {
        // Arrange
        // Monday for four, Thursday for six.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var recipeId = await RecipeWithButterAsync(client, householdId, "Cake", 200);

        // Act
        await client.PostAsync(
            $"/api/v1/households/{householdId}/shopping-list/recipes",
            new { recipeId, servings = 4 },
            Token);

        var response = await client.PostAsync(
            $"/api/v1/households/{householdId}/shopping-list/recipes",
            new { recipeId, servings = 6 },
            Token);

        // Assert
        var items = response.Json!.Value.GetProperty("items").EnumerateArray().ToList();
        var butter = Assert.Single(items, item => item.GetProperty("name").GetString() == "Butter");

        // 200 for four, 300 for six.
        Assert.Equal(500m, butter.GetProperty("quantity").GetDecimal());
    }

    [Fact]
    public async Task AddRecipe_ShouldNotTouchALineAlreadyInTheTrolley()
    {
        // Arrange
        // A ticked line is bought. Adding to it would change an amount somebody
        // has already picked up, so the second add starts a new line instead.
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

        // Act
        var response = await client.PostAsync(
            $"/api/v1/households/{householdId}/shopping-list/recipes",
            new { recipeId, servings = 4 },
            Token);

        // Assert
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
        // Arrange
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var recipeId = await RecipeWithButterAsync(client, householdId, "Cake", 200);

        // Act
        // The recipe is for four; this is being made for six.
        var response = await client.PostAsync(
            $"/api/v1/households/{householdId}/shopping-list/recipes",
            new { recipeId, servings = 6 },
            Token);

        // Assert
        var butter = response.Json!.Value.GetProperty("items")[0];

        // Unrounded on purpose: rounding here and summing later compounds error.
        Assert.Equal(300m, butter.GetProperty("quantity").GetDecimal());
    }

    [Fact]
    public async Task AddItem_ShouldGuessWhereItIsFound()
    {
        // Arrange
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        // Act
        var response = await client.PostAsync(
            $"/api/v1/households/{householdId}/shopping-list/items",
            new { name = "Tomaten" },
            Token);

        // Assert
        Assert.Equal("produce", response.Json!.Value.GetProperty("items")[0].GetProperty("section").GetString());
    }

    [Fact]
    public async Task UpdateItem_ShouldRememberACorrectedSection()
    {
        // Arrange
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

        // Act
        // The same thing, added again after the correction.
        var again = await client.PostAsync(
            $"/api/v1/households/{householdId}/shopping-list/items",
            new { name = "wunderpulver" },
            Token);

        // Assert
        // Moving an item once teaches the household where it lives, which is
        // what replaces a configuration screen.
        var sections = again.Json!.Value.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("section").GetString())
            .ToList();

        Assert.All(sections, section => Assert.Equal("spices_baking", section));
    }

    [Fact]
    public async Task RemoveItems_ShouldClearOnlyWhatIsBought()
    {
        // Arrange
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

        // Act
        var response = await client.DeleteAsync(
            $"/api/v1/households/{householdId}/shopping-list/items",
            Token);

        // Assert
        var remaining = response.Json!.Value.GetProperty("items").EnumerateArray().ToList();

        Assert.Single(remaining);
        Assert.False(remaining[0].GetProperty("isChecked").GetBoolean());
    }

    [Fact]
    public async Task AddRecipe_ShouldKeepTheAmountSomebodyScaledTo()
    {
        // Arrange
        // Scaling to an amount you have produces a yield that is not a round
        // number — 370 g of flour in a recipe built on 200 g for four is 7.4
        // servings — and the whole point of that control is that 370 is what
        // comes out. A yield rounded on the way here would put 380 on the list.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var recipeId = await RecipeWithButterAsync(client, householdId, "Bread", 200);

        // Act
        var response = await client.PostAsync(
            $"/api/v1/households/{householdId}/shopping-list/recipes",
            new { recipeId, servings = 7.4m },
            Token);

        // Assert
        var butter = response.Json!.Value.GetProperty("items")[0];

        Assert.Equal(370m, butter.GetProperty("quantity").GetDecimal());
    }

    [Fact]
    public async Task AddItem_ShouldNotRejectSomebodyElseAddingAtTheSameMoment()
    {
        // Arrange
        // Two people in the same kitchen, one list, one row, one version. This
        // is the ordinary case — one is at the fridge and the other in the
        // cupboard — and it used to mean the second one got a 412 and their
        // item was simply not on the list.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        var names = new[] { "Butter", "Mehl", "Zucker", "Eier", "Milch" };

        // Act
        var responses = await Task.WhenAll(names.Select(name => client.PostAsync(
            $"/api/v1/households/{householdId}/shopping-list/items",
            new { name },
            Token)));

        // Assert
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));

        var stored = await client.GetAsync(
            $"/api/v1/households/{householdId}/shopping-list",
            Token);

        var onTheList = stored.Json!.Value.GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("name").GetString())
            .ToList();

        // Every one of them, not four out of five.
        Assert.Equal(names.Length, onTheList.Count);
        Assert.All(names, name => Assert.Contains(name, onTheList));
    }

    [Fact]
    public async Task AddPlannedMeals_Twice_ShouldShopForTheWeekOnce()
    {
        // Arrange
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var recipeId = await RecipeWithButterAsync(client, householdId, "Cake", 200);

        await PlanAsync(client, householdId, recipeId, Monday);

        // Act
        await AddWeekAsync(client, householdId);
        var response = await AddWeekAsync(client, householdId);

        // Assert
        // Pressing the button again is checking, not shopping twice.
        Assert.Equal(200m, Butter(response).GetProperty("quantity").GetDecimal());
    }

    [Fact]
    public async Task AddPlannedMeals_ShouldCountARecipeAlreadyAddedFromItsPage()
    {
        // Arrange
        // The waffles went on the list from their recipe, then onto Monday.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var recipeId = await RecipeWithButterAsync(client, householdId, "Waffles", 200);

        await client.PostAsync(
            $"/api/v1/households/{householdId}/shopping-list/recipes",
            new { recipeId, servings = 4 },
            Token);
        await PlanAsync(client, householdId, recipeId, Monday);

        // Act
        var response = await AddWeekAsync(client, householdId);

        // Assert
        // Monday is shopped for once. Doubling every ingredient here was the
        // bug: silent, and only found out at the till.
        Assert.Equal(200m, Butter(response).GetProperty("quantity").GetDecimal());
        Assert.True(Assert.Single(await WeekAsync(client, householdId)).GetProperty("isOnShoppingList").GetBoolean());
    }

    [Fact]
    public async Task AddPlannedMeals_ShouldShopForEveryMealOfARecipePlannedTwice()
    {
        // Arrange
        // Monday for four, Thursday for six.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var recipeId = await RecipeWithButterAsync(client, householdId, "Cake", 200);

        await PlanAsync(client, householdId, recipeId, Monday);
        await PlanAsync(client, householdId, recipeId, Monday.AddDays(3), servings: 6);

        // Act
        var response = await AddWeekAsync(client, householdId);

        // Assert
        Assert.Equal(500m, Butter(response).GetProperty("quantity").GetDecimal());
    }

    [Fact]
    public async Task AddPlannedMeals_ShouldSayWhichMealsAreOnTheList()
    {
        // Arrange
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var recipeId = await RecipeWithButterAsync(client, householdId, "Cake", 200);

        await PlanAsync(client, householdId, recipeId, Monday);

        // Act
        var before = Assert.Single(await WeekAsync(client, householdId));

        await AddWeekAsync(client, householdId);

        var after = Assert.Single(await WeekAsync(client, householdId));

        // Assert
        Assert.False(before.GetProperty("isOnShoppingList").GetBoolean());
        Assert.True(after.GetProperty("isOnShoppingList").GetBoolean());
    }

    [Fact]
    public async Task WithdrawPlannedMeal_ShouldTakeBackExactlyWhatThatMealAdded()
    {
        // Arrange
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var cake = await RecipeWithButterAsync(client, householdId, "Cake", 200);
        var biscuits = await RecipeWithButterAsync(client, householdId, "Biscuits", 50);

        var monday = await PlanAsync(client, householdId, cake, Monday);
        await PlanAsync(client, householdId, biscuits, Monday.AddDays(1));
        await AddWeekAsync(client, householdId);

        await client.DeleteAsync($"/api/v1/households/{householdId}/meal-plan/{monday}", Token);

        // Act
        var response = await client.DeleteAsync(
            $"/api/v1/households/{householdId}/shopping-list/meals/{monday}",
            Token);

        // Assert
        // The biscuits still need their 50 g; the cake no longer needs its 200.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(50m, Butter(response).GetProperty("quantity").GetDecimal());
    }

    [Fact]
    public async Task WithdrawPlannedMeal_ShouldKeepWhatSomebodyTyped()
    {
        // Arrange
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var recipeId = await RecipeWithButterAsync(client, householdId, "Cake", 200);

        await client.PostAsync(
            $"/api/v1/households/{householdId}/shopping-list/items",
            new { name = "Limes", quantity = 2 },
            Token);

        var entryId = await PlanAsync(client, householdId, recipeId, Monday);
        await AddWeekAsync(client, householdId);

        // Act
        var response = await client.DeleteAsync(
            $"/api/v1/households/{householdId}/shopping-list/meals/{entryId}",
            Token);

        // Assert
        var names = response.Json!.Value.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("name").GetString());

        Assert.Equal(["Limes"], names);
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

        return recipeId;
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
