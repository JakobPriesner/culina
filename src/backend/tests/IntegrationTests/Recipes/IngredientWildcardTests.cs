using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Recipes;

/// <summary>
/// An ingredient chip is matched by LIKE; built from what was typed, "%" and "_" were wildcards that held every recipe.
/// The free-text lanes are held to the same in <c>RecipeSearchRelevanceTests</c>.
/// </summary>
[Collection(RequiresDatabase.Name)]
public class IngredientWildcardTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    public async Task AnIngredientChip_ShouldHoldNothing_WhenItIsOnlyAWildcard(string chip)
    {
        // Arrange
        var (client, householdId) = await SignedInAsync();
        await RecipeAsync(client, householdId, "Omelette", "Eier");
        await RecipeAsync(client, householdId, "Butterbrot", "Butter");

        // Act
        var response = await client.GetAsync(
            $"/api/v1/recipes?householdId={householdId}&ingredient={Uri.EscapeDataString(chip)}&sort=relevance",
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var matches = response.Json!.Value.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("ingredientMatch"))
            .ToList();
        Assert.Equal(2, matches.Count);
        Assert.All(matches, match =>
        {
            Assert.Equal(1, match.GetProperty("requested").GetInt32());
            Assert.Equal(0, match.GetProperty("matched").GetInt32());
        });
    }

    private async Task<(ApiClient Client, Guid HouseholdId)> SignedInAsync()
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

        var householdId = (await client.GetAsync("/api/v1/households", Token))
            .Json!.Value.GetProperty("items")[0].GetProperty("householdId").GetGuid();

        return (client, householdId);
    }

    private static async Task RecipeAsync(ApiClient client, Guid householdId, string title, string ingredient)
    {
        var created = await client.PostAsync("/api/v1/recipes", new { householdId, title }, Token);
        var recipeId = created.Json!.Value.GetProperty("recipeId").GetGuid();
        var read = await client.GetAsync($"/api/v1/recipes/{recipeId}", Token);

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/recipes/{recipeId}")
        {
            Content = JsonContent.Create(new
            {
                title,
                language = "de",
                yieldAmount = 2,
                yieldKind = "servings",
                groups = new[] { new { ingredients = new[] { new { quantity = 2, unit = (string?)null, name = ingredient } } } },
                steps = Array.Empty<object>(),
                tags = Array.Empty<string>()
            })
        };

        request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(read.ETag!));

        var saved = await client.SendAsync(request, Token);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
    }
}
