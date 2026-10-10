using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Nutrition;

[Collection(RequiresDatabase.Name)]
public class SharedNutritionTests(PostgresFixture postgres)
{
    private const string Buttermilk = "M150000";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_ShouldReturnTheFigureWithAnETag_WithNoSessionAtAll()
    {
        // Arrange
        var (kitchen, recipeId) = await SharedRecipeAsync();
        var token = await ShareAsync(kitchen.Client, recipeId);
        using var visitor = postgres.Api.NewApiClient();

        // Act
        var response = await visitor.GetAsync($"/api/v1/shared-recipes/{token}/nutrition", Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.ETag);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.Ordinal);

        var body = response.Json!.Value;
        Assert.Equal("serving", body.GetProperty("per").GetString());
        Assert.Equal(4m, body.GetProperty("yield").GetDecimal());
        Assert.Equal(1, body.GetProperty("counted").GetInt32());
        Assert.Equal(2, body.GetProperty("lines").GetInt32());
        Assert.Equal("Bundeslebensmittelschlüssel", body.GetProperty("source").GetProperty("name").GetString());

        var food = body.GetProperty("ingredients")[1].GetProperty("food");
        Assert.Equal("Q611000", food.GetProperty("code").GetString());
        Assert.Equal("Butter", food.GetProperty("labelDe").GetString());
        Assert.False(string.IsNullOrWhiteSpace(food.GetProperty("labelEn").GetString()));
    }

    [Fact]
    public async Task Get_ShouldAnswerNotModified_WhenTheReaderHoldsTheTag()
    {
        // Arrange
        var (kitchen, recipeId) = await SharedRecipeAsync();
        var token = await ShareAsync(kitchen.Client, recipeId);
        using var visitor = postgres.Api.NewApiClient();
        var first = await visitor.GetAsync($"/api/v1/shared-recipes/{token}/nutrition", Token);

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/shared-recipes/{token}/nutrition");
        request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(first.ETag!));

        // Act
        var again = await visitor.SendAsync(request, Token);

        // Assert
        Assert.Equal(HttpStatusCode.NotModified, again.StatusCode);
    }

    [Fact]
    public async Task Get_ShouldApplyNoCorrectionOfTheOwningHousehold()
    {
        // Arrange
        var (kitchen, recipeId) = await SharedRecipeAsync();
        var token = await ShareAsync(kitchen.Client, recipeId);
        await kitchen.Client.PutAsync(
            $"/api/v1/households/{kitchen.HouseholdId}/ingredients/Butter",
            new { food = Buttermilk },
            Token);
        using var visitor = postgres.Api.NewApiClient();

        // Act
        var shared = await visitor.GetAsync($"/api/v1/shared-recipes/{token}/nutrition", Token);
        var own = await kitchen.Client.GetAsync($"/api/v1/recipes/{recipeId}/nutrition", Token);

        // Assert: the household sees its correction, the link holder the name table's butter.
        var ownButter = own.Json!.Value.GetProperty("ingredients")[1];
        var sharedButter = shared.Json!.Value.GetProperty("ingredients")[1];
        Assert.True(ownButter.GetProperty("corrected").GetBoolean());
        Assert.Equal(Buttermilk, ownButter.GetProperty("food").GetProperty("code").GetString());
        Assert.False(sharedButter.GetProperty("corrected").GetBoolean());
        Assert.Equal("Q611000", sharedButter.GetProperty("food").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Get_ShouldAnswerNotFound_ForAnUnknownOrRevokedToken()
    {
        // Arrange
        var (kitchen, recipeId) = await SharedRecipeAsync();
        var token = await ShareAsync(kitchen.Client, recipeId);
        await kitchen.Client.DeleteAsync($"/api/v1/recipes/{recipeId}/share", Token);
        using var visitor = postgres.Api.NewApiClient();

        // Act
        var revoked = await visitor.GetAsync($"/api/v1/shared-recipes/{token}/nutrition", Token);
        var invented = await visitor.GetAsync("/api/v1/shared-recipes/not-a-real-token/nutrition", Token);
        var sharedRead = await visitor.GetAsync($"/api/v1/shared-recipes/{token}", Token);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, revoked.StatusCode);
        Assert.Equal(sharedRead.ProblemCode, revoked.ProblemCode);
        Assert.Equal("recipes.share_not_found", invented.ProblemCode);
    }

    private async Task<(Kitchen Kitchen, Guid RecipeId)> SharedRecipeAsync()
    {
        var kitchen = await Kitchen.OpenAsync(postgres);
        var recipeId = (await kitchen.Client.PostAsync(
            "/api/v1/recipes",
            new { householdId = kitchen.HouseholdId, title = "Brot" },
            Token)).Json!.Value.GetProperty("recipeId").GetGuid();
        var read = await kitchen.Client.GetAsync($"/api/v1/recipes/{recipeId}", Token);

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/recipes/{recipeId}")
        {
            Content = JsonContent.Create(new
            {
                title = "Brot",
                language = "de",
                yieldAmount = 4,
                yieldKind = "servings",
                groups = new[]
                {
                    new
                    {
                        name = (string?)null,
                        ingredients = new[]
                        {
                            new { name = "Mehl", quantity = (decimal?)200m, unit = "g" },
                            new { name = "Butter", quantity = (decimal?)null, unit = "g" }
                        }
                    }
                },
                steps = Array.Empty<object>(),
                tags = Array.Empty<string>()
            })
        };
        request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(read.ETag!));

        Assert.Equal(HttpStatusCode.OK, (await kitchen.Client.SendAsync(request, Token)).StatusCode);

        return (kitchen, recipeId);
    }

    private static async Task<string> ShareAsync(ApiClient client, Guid recipeId)
    {
        var shared = await client.PutAsync($"/api/v1/recipes/{recipeId}/share", new { }, Token);

        return shared.Json!.Value.GetProperty("token").GetString()!;
    }
}
