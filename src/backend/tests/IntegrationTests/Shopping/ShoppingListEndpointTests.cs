using System.Net;
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
