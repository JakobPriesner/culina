using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Recipes;

[Collection(RequiresDatabase.Name)]
public class RecipeEndpointTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task Create_ShouldNeedOnlyAHouseholdAndATitle()
    {
        // Arrange
        using var client = await SignedInAsync();
        var householdId = await FirstHouseholdIdAsync(client);

        // Act
        var response = await client.PostAsync(
            "/api/v1/recipes",
            new { householdId, title = "Bolognese" },
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = response.Json!.Value;
        Assert.Equal("Bolognese", created.GetProperty("title").GetString());
        // The defaults a recipe starts with, so the editor has something to show.
        Assert.Equal(4, created.GetProperty("yieldAmount").GetDecimal());
        Assert.Equal("servings", created.GetProperty("yieldKind").GetString());
        Assert.Single(created.GetProperty("groups").EnumerateArray().ToList());
    }

    /// <summary>
    /// A recipe starts in the language the person who started it reads.
    /// </summary>
    /// <remarks>
    /// Every recipe used to start in English, and nothing on any screen ever
    /// overwrote it — so a German kitchen's whole library said "en", the search
    /// index stemmed it with the English stemmer, and the assistant translated
    /// it when asked to tidy it up.
    /// </remarks>
    [Fact]
    public async Task Create_ShouldStartTheRecipeInTheLanguageItsAuthorReads()
    {
        // Arrange
        using var client = await SignedInAsync();
        await ReadInGermanAsync(client);
        var householdId = await FirstHouseholdIdAsync(client);

        // Act
        var response = await client.PostAsync(
            "/api/v1/recipes",
            new { householdId, title = "Linsensuppe" },
            Token);

        // Assert
        Assert.Equal("de", response.Json!.Value.GetProperty("language").GetString());
    }

    [Fact]
    public async Task Create_ShouldStartTheRecipeInEnglish_ForAnAccountThatNeverChoseALanguage()
    {
        // Arrange
        using var client = await SignedInAsync();
        var householdId = await FirstHouseholdIdAsync(client);

        // Act
        var response = await client.PostAsync(
            "/api/v1/recipes",
            new { householdId, title = "Bolognese" },
            Token);

        // Assert
        // The default preference, arrived at honestly rather than hard-coded in
        // the domain: an account that never opened the settings screen reads in
        // English, so its recipes are written in English.
        Assert.Equal("en", response.Json!.Value.GetProperty("language").GetString());
    }

    [Fact]
    public async Task Create_ShouldBeRefused_ForAHouseholdTheCallerIsNotIn()
    {
        // Arrange
        using var client = await SignedInAsync();

        // Act
        var response = await client.PostAsync(
            "/api/v1/recipes",
            new { householdId = Guid.CreateVersion7(), title = "Bolognese" },
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ShouldStoreIngredientsAndSteps_WithTheirReferences()
    {
        // Arrange
        using var client = await SignedInAsync();
        var recipe = await CreateRecipeAsync(client);
        var butterId = Guid.CreateVersion7();

        // Act
        var saved = await PutAsync(client, recipe.Id, recipe.ETag, FullRecipe(butterId));

        // Assert
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var body = saved.Json!.Value;
        var ingredients = body.GetProperty("groups")[0].GetProperty("ingredients");
        Assert.Equal(2, ingredients.GetArrayLength());
        Assert.Equal(200.5m, ingredients[0].GetProperty("quantity").GetDecimal());
        Assert.Equal("g", ingredients[0].GetProperty("unit").GetString());
        Assert.Equal(60, body.GetProperty("totalMinutes").GetInt32());
    }

    [Fact]
    public async Task Read_ShouldInlineTheIngredientNameAndBaseAmount_InTheStepSegments()
    {
        // Arrange
        using var client = await SignedInAsync();
        var recipe = await CreateRecipeAsync(client);
        var butterId = Guid.CreateVersion7();
        await PutAsync(client, recipe.Id, recipe.ETag, FullRecipe(butterId));

        // Act
        var read = await client.GetAsync($"/api/v1/recipes/{recipe.Id}", Token);

        // Assert
        // This is what lets the client render "melt 200.5 g butter" and rescale
        // it without another request.
        var segments = read.Json!.Value.GetProperty("steps")[1].GetProperty("segments");
        var reference = segments.EnumerateArray().Single(s => s.GetProperty("type").GetString() == "ingredient");
        Assert.Equal("butter", reference.GetProperty("name").GetString());
        Assert.Equal(200.5m, reference.GetProperty("quantity").GetDecimal());
        Assert.Equal(butterId, reference.GetProperty("recipeIngredientId").GetGuid());
    }

    [Fact]
    public async Task Update_ShouldKeepAnIngredientAStepNeedsButDoesNotName()
    {
        // Arrange
        using var client = await SignedInAsync();
        var recipe = await CreateRecipeAsync(client);
        var butterId = Guid.CreateVersion7();
        var saltId = Guid.CreateVersion7();

        // Act
        // The first step's words name nothing; salt is what you get out for it.
        var saved = await PutAsync(client, recipe.Id, recipe.ETag, WithNeeds(butterId, saltId));

        // Assert
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var steps = saved.Json!.Value.GetProperty("steps");
        Assert.Equal([saltId], Uses(steps[0]));
        // The second step lists salt and names butter, and gets both back in
        // the recipe's own ingredient order rather than the order it sent.
        Assert.Equal([butterId, saltId], Uses(steps[1]));
    }

    [Fact]
    public async Task Update_ShouldNeedWhatTheStepNames_WhenTheClientSaysNothingAboutNeeds()
    {
        // Arrange
        using var client = await SignedInAsync();
        var recipe = await CreateRecipeAsync(client);
        var butterId = Guid.CreateVersion7();

        // Act
        // FullRecipe has never heard of per-step ingredients.
        var saved = await PutAsync(client, recipe.Id, recipe.ETag, FullRecipe(butterId));

        // Assert
        // A client that omits the field gets exactly what it always got: the
        // words, and nothing else.
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var steps = saved.Json!.Value.GetProperty("steps");
        Assert.Empty(Uses(steps[0]));
        Assert.Equal([butterId], Uses(steps[1]));
    }

    [Fact]
    public async Task Update_ShouldRejectAStepNeedingAnIngredientTheRecipeDoesNotHave()
    {
        // Arrange
        using var client = await SignedInAsync();
        var recipe = await CreateRecipeAsync(client);

        // Act
        var saved = await PutAsync(client, recipe.Id, recipe.ETag, new
        {
            title = "Bolognese",
            language = "en",
            yieldAmount = 4,
            yieldKind = "servings",
            groups = new[] { new { name = (string?)null, ingredients = Array.Empty<object>() } },
            steps = new object[]
            {
                new
                {
                    segments = new object[] { new { type = "text", value = "Combine." } },
                    uses = new[] { Guid.CreateVersion7() }
                }
            },
            tags = Array.Empty<string>()
        });

        // Assert
        // Named as a failure rather than reaching the foreign key on the
        // reference index, which is what that check exists to stop.
        Assert.Equal(HttpStatusCode.BadRequest, saved.StatusCode);
        Assert.Equal("recipes.unknown_ingredient_reference", saved.ProblemCode);
    }

    [Fact]
    public async Task Update_ShouldRejectAStepReferringToAnIngredientTheRecipeDoesNotHave()
    {
        // Arrange
        using var client = await SignedInAsync();
        var recipe = await CreateRecipeAsync(client);

        // Act
        var saved = await PutAsync(client, recipe.Id, recipe.ETag, new
        {
            title = "Bolognese",
            language = "en",
            yieldAmount = 4,
            yieldKind = "servings",
            groups = new[] { new { name = (string?)null, ingredients = Array.Empty<object>() } },
            steps = new[]
            {
                new
                {
                    segments = new object[]
                    {
                        new { type = "ingredient", recipeIngredientId = Guid.CreateVersion7() }
                    }
                }
            },
            tags = Array.Empty<string>()
        });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, saved.StatusCode);
        Assert.Equal("recipes.unknown_ingredient_reference", saved.ProblemCode);
    }

    [Fact]
    public async Task Update_ShouldRequireIfMatch_AndRejectAStaleOne()
    {
        // Arrange
        using var client = await SignedInAsync();
        var recipe = await CreateRecipeAsync(client);
        await PutAsync(client, recipe.Id, recipe.ETag, FullRecipe(Guid.CreateVersion7()));

        // Act
        var missing = await client.PutAsync(
            $"/api/v1/recipes/{recipe.Id}",
            FullRecipe(Guid.CreateVersion7()),
            Token);
        var stale = await PutAsync(client, recipe.Id, recipe.ETag, FullRecipe(Guid.CreateVersion7()));

        // Assert
        Assert.Equal(HttpStatusCode.PreconditionRequired, missing.StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
    }

    [Fact]
    public async Task Read_ShouldAnswer304_WhenTheCallerAlreadyHasThisVersion()
    {
        // Arrange
        using var client = await SignedInAsync();
        var recipe = await CreateRecipeAsync(client);

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/recipes/{recipe.Id}");
        request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(recipe.ETag));
        var response = await client.SendAsync(request, Token);

        // Assert
        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ShouldBeIdempotent_BecauseTheOutcomeIsWhatTheCallerWanted()
    {
        // Arrange
        using var client = await SignedInAsync();
        var recipe = await CreateRecipeAsync(client);

        // Act
        var first = await client.DeleteAsync($"/api/v1/recipes/{recipe.Id}", Token);
        var second = await client.DeleteAsync($"/api/v1/recipes/{recipe.Id}", Token);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
    }

    [Fact]
    public async Task Read_ShouldSayNotFound_ForARecipeInAnotherHousehold()
    {
        // Arrange
        using var owner = await SignedInAsync();
        var recipe = await CreateRecipeAsync(owner);
        using var stranger = await SecondUserAsync();

        // Act
        var response = await stranger.GetAsync($"/api/v1/recipes/{recipe.Id}", Token);

        // Assert
        // Never a 403: that would confirm the recipe exists.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("recipes.not_found", response.ProblemCode);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Guid[] Uses(JsonElement step) =>
        [.. step.GetProperty("uses").EnumerateArray().Select(one => one.GetGuid())];

    /// <summary>The same recipe, with the needs of each step written down.</summary>
    private static object WithNeeds(Guid butterId, Guid saltId) => new
    {
        title = "Bolognese",
        language = "en",
        yieldAmount = 4,
        yieldKind = "servings",
        groups = new[]
        {
            new
            {
                name = (string?)null,
                ingredients = new object[]
                {
                    new { ingredientId = butterId, quantity = 200.5, unit = "g", name = "butter" },
                    new { ingredientId = saltId, name = "salt" }
                }
            }
        },
        steps = new object[]
        {
            new
            {
                segments = new object[] { new { type = "text", value = "Season the pan." } },
                uses = new[] { saltId }
            },
            new
            {
                // Listed out of order, and butter is named rather than listed.
                uses = new[] { saltId },
                segments = new object[]
                {
                    new { type = "text", value = "Melt " },
                    new { type = "ingredient", recipeIngredientId = butterId },
                    new { type = "text", value = "." }
                }
            }
        },
        tags = Array.Empty<string>()
    };

    [Fact]
    public async Task Update_ShouldKeepTheRecipesOwnWordsForItsYieldAndItsSteps()
    {
        // Arrange
        using var client = await SignedInAsync();
        var recipe = await CreateRecipeAsync(client);

        // Act
        var saved = await PutAsync(client, recipe.Id, recipe.ETag, InItsOwnWords());

        // Assert
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var body = saved.Json!.Value;

        // The word replaces the wording and nothing else: the kind is still
        // what the servings control counts by.
        Assert.Equal("Cake", body.GetProperty("yieldLabel").GetString());
        Assert.Equal("servings", body.GetProperty("yieldKind").GetString());

        var steps = body.GetProperty("steps");
        Assert.Equal("Prepare the base", steps[0].GetProperty("title").GetString());
        // A step that was never named stays unnamed rather than inheriting one.
        Assert.Equal(JsonValueKind.Null, steps[1].GetProperty("title").ValueKind);
    }

    [Fact]
    public async Task Update_ShouldForgetTheRecipesOwnWords_WhenTheyAreClearedAgain()
    {
        // Arrange
        using var client = await SignedInAsync();
        var recipe = await CreateRecipeAsync(client);
        var named = await PutAsync(client, recipe.Id, recipe.ETag, InItsOwnWords());

        // Act
        // Emptied, not omitted — which is what an author clearing both fields
        // sends, and it has to mean "back to the usual wording".
        var cleared = await PutAsync(
            client,
            recipe.Id,
            named.Headers.ETag!.ToString(),
            InItsOwnWords(yieldLabel: "   ", stepTitle: ""));

        // Assert
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        var body = cleared.Json!.Value;
        Assert.Equal(JsonValueKind.Null, body.GetProperty("yieldLabel").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("steps")[0].GetProperty("title").ValueKind);
    }

    [Fact]
    public async Task Update_ShouldRefuseAYieldWordLongEnoughToBeASentence()
    {
        // Arrange
        using var client = await SignedInAsync();
        var recipe = await CreateRecipeAsync(client);

        // Act
        var saved = await PutAsync(
            client,
            recipe.Id,
            recipe.ETag,
            InItsOwnWords(yieldLabel: new string('x', 41)));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, saved.StatusCode);
    }

    private static object InItsOwnWords(string? yieldLabel = "Cake", string? stepTitle = "Prepare the base") => new
    {
        title = "Lemon cake",
        language = "en",
        yieldAmount = 1,
        yieldKind = "servings",
        yieldLabel,
        groups = new[] { new { name = (string?)null, ingredients = new object[] { new { name = "flour" } } } },
        steps = new object[]
        {
            new { title = stepTitle, segments = new object[] { new { type = "text", value = "Rub the butter in." } } },
            new { segments = new object[] { new { type = "text", value = "Bake." } } }
        },
        tags = Array.Empty<string>()
    };

    private static object FullRecipe(Guid butterId) => new
    {
        title = "Bolognese",
        description = "A weeknight standby.",
        language = "en",
        yieldAmount = 4,
        yieldKind = "servings",
        prepMinutes = 15,
        cookMinutes = 45,
        groups = new[]
        {
            new
            {
                name = (string?)null,
                ingredients = new object[]
                {
                    new { ingredientId = butterId, quantity = 200.5, unit = "g", name = "butter", note = "cubed" },
                    new { name = "salt" }
                }
            }
        },
        steps = new object[]
        {
            new { segments = new object[] { new { type = "text", value = "Preheat the pan." } } },
            new
            {
                durationSeconds = 300,
                segments = new object[]
                {
                    new { type = "text", value = "Melt " },
                    new { type = "ingredient", recipeIngredientId = butterId },
                    new { type = "text", value = "." }
                }
            }
        },
        tags = new[] { "quick", "weeknight" }
    };

    private static async Task<ApiResponse> PutAsync(ApiClient client, Guid recipeId, string etag, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/recipes/{recipeId}")
        {
            Content = JsonContent.Create(body)
        };

        request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(etag));

        return await client.SendAsync(request, Token);
    }

    [Fact]
    public async Task Update_ShouldBlameTheCaller_WhenTheBodyCannotBeRead()
    {
        // Arrange
        // A step segment without its `type`, which is the shape the contract
        // requires. The body is rejected before any handler runs, and what the
        // caller gets back must still be a problem document saying it was their
        // request — not a 500, which sends them looking on the wrong side and
        // buries real faults in the log.
        using var client = await SignedInAsync();
        var recipe = await CreateRecipeAsync(client);

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/recipes/{recipe.Id}")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new
            {
                title = "Bolognese",
                language = "en",
                yieldAmount = 4,
                yieldKind = "servings",
                groups = Array.Empty<object>(),
                steps = new[] { new { segments = new[] { new { value = "Stir." } } } },
                tags = Array.Empty<string>()
            })
        };

        request.Headers.IfMatch.Add(
            System.Net.Http.Headers.EntityTagHeaderValue.Parse(recipe.ETag));

        // Act
        var response = await client.SendAsync(request, Token);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        // The code differs by which layer caught it — minimal APIs turn this
        // into a status, and GlobalExceptionHandler answers where the exception
        // escapes instead — so what is pinned here is the part that matters:
        // a 400, described the way every other failure is.
        Assert.NotNull(response.ProblemCode);
        Assert.Equal(400, response.Json!.Value.GetProperty("status").GetInt32());
    }

    /// <summary>Switches the signed-in account to German.</summary>
    private static async Task ReadInGermanAsync(ApiClient client)
    {
        var before = await client.GetAsync("/api/v1/users/me/settings", Token);

        var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/users/me/settings")
        {
            Content = JsonContent.Create(
                new { locale = "de", theme = "warm-paper", mode = "system", measurementSystem = "metric" })
        };

        request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(before.ETag!));

        var response = await client.SendAsync(request, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<Guid> FirstHouseholdIdAsync(ApiClient client) =>
        (await client.GetAsync("/api/v1/households", Token))
            .Json!.Value.GetProperty("items")[0].GetProperty("householdId").GetGuid();

    private static async Task<(Guid Id, string ETag)> CreateRecipeAsync(ApiClient client)
    {
        var householdId = await FirstHouseholdIdAsync(client);
        var created = await client.PostAsync(
            "/api/v1/recipes",
            new { householdId, title = "Bolognese" },
            Token);
        var id = created.Json!.Value.GetProperty("recipeId").GetGuid();
        var read = await client.GetAsync($"/api/v1/recipes/{id}", Token);

        return (id, read.ETag!);
    }

    private async Task<ApiClient> SignedInAsync()
    {
        await postgres.ResetAsync(Token);

        return await SignInAsync("ada@example.com");
    }

    private async Task<ApiClient> SecondUserAsync()
    {
        var settings = postgres.Api.Services
            .GetRequiredService<Application.Abstractions.Settings.RegistrationSettings>();
        settings.OpenRegistration = true;
        settings.RequireInvitation = false;

        return await SignInAsync("grace@example.com");
    }

    private async Task<ApiClient> SignInAsync(string email)
    {
        var client = postgres.Api.NewApiClient();

        await client.PostAsync(
            "/api/v1/users",
            new { email, displayName = "Ada", password = Password },
            Token);
        await client.PostAsync("/api/v1/sessions", new { email, password = Password }, Token);

        return client;
    }
}
