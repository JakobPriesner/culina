using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Application.Abstractions.Settings;
using IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Cookbooks;

/// <summary>
/// A cookbook that fills itself: rules are stored, matches are not, so a recipe written later is on
/// it.
/// </summary>
[Collection(RequiresDatabase.Name)]
public class SmartCookbookTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly string[] Untagged = [];

    private static readonly string[] Chicken = ["Hähnchen"];

    private static readonly string[] Tofu = ["Tofu"];

    private static readonly string[] MainCourse = ["hauptspeise"];

    private static readonly string[] Main = ["Hauptspeise"];

    private static readonly string[] Starter = ["Vorspeise"];

    private static readonly string[] MainAndQuick = ["Hauptspeise", "Schnell"];

    [Fact]
    public async Task ARecipeWrittenAfterwards_ShouldBeOnItWithoutAnythingRunning()
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        var cookbookId = await SmartAsync(client, householdId, "Hähnchen-Hauptspeisen", new
        {
            tags = MainCourse,
            ingredients = Chicken
        });

        await RecipeAsync(client, householdId, "Hähnchenbrust mit Reis", "Hähnchenbrust", Main);

        var onIt = await client.GetAsync(
            $"/api/v1/recipes?householdId={householdId}&cookbookId={cookbookId}",
            Token);

        Assert.Equal(["Hähnchenbrust mit Reis"], Titles(onIt));
    }

    [Fact]
    public async Task EveryRuleMustHold_NotJustOne()
    {
        // Shelves worth having are narrow, so the rules are ANDed.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        var cookbookId = await SmartAsync(client, householdId, "Beides", new
        {
            tags = MainCourse,
            ingredients = Chicken
        });

        await RecipeAsync(client, householdId, "Beides", "Hähnchenschenkel", Main);
        await RecipeAsync(client, householdId, "Nur das Fleisch", "Hähnchenbrust", Starter);
        await RecipeAsync(client, householdId, "Nur die Kategorie", "Rindfleisch", Main);

        var onIt = await client.GetAsync(
            $"/api/v1/recipes?householdId={householdId}&cookbookId={cookbookId}",
            Token);

        Assert.Equal(["Beides"], Titles(onIt));
    }

    [Fact]
    public async Task EditingARecipeOutOfTheRules_ShouldTakeItOffTheShelf()
    {
        // It also empties itself, with no sweep to run or row to delete.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        var cookbookId = await SmartAsync(client, householdId, "Hähnchen", new
        {
            ingredients = Chicken
        });

        var recipeId = await RecipeAsync(client, householdId, "Wird geändert", "Hähnchenbrust", Untagged);

        await ReplaceIngredientAsync(client, recipeId, "Tofu");

        var onIt = await client.GetAsync(
            $"/api/v1/recipes?householdId={householdId}&cookbookId={cookbookId}",
            Token);

        Assert.Empty(onIt.Json!.Value.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task SearchingInside_ShouldNarrowTheRulesRatherThanReplaceThem()
    {
        // A search inside a smart shelf must not widen to everything.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        var cookbookId = await SmartAsync(client, householdId, "Hähnchen", new
        {
            ingredients = Chicken
        });

        await RecipeAsync(client, householdId, "Hähnchensuppe", "Hähnchenbrust", Untagged);
        await RecipeAsync(client, householdId, "Hähnchenpfanne", "Hähnchenbrust", Untagged);
        await RecipeAsync(client, householdId, "Linsensuppe", "Linsen", Untagged);

        var onIt = await client.GetAsync(
            $"/api/v1/recipes?householdId={householdId}&cookbookId={cookbookId}&query=suppe",
            Token);

        Assert.Equal(["Hähnchensuppe"], Titles(onIt));
    }

    [Fact]
    public async Task TheTimeRule_ShouldExcludeARecipeThatNeverSaidHowLong()
    {
        // An unknown time is not an answer to "under 25 minutes", as in the plain search.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        var cookbookId = await SmartAsync(client, householdId, "Schnell", new { maxMinutes = 25 });

        await RecipeAsync(client, householdId, "Schnell genug", "Nudeln", Untagged, prepMinutes: 10);
        await RecipeAsync(client, householdId, "Sagt nichts", "Nudeln", Untagged);

        var onIt = await client.GetAsync(
            $"/api/v1/recipes?householdId={householdId}&cookbookId={cookbookId}",
            Token);

        Assert.Equal(["Schnell genug"], Titles(onIt));
    }

    [Fact]
    public async Task ItShouldRefuseARecipePutOnByHand()
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        var cookbookId = await SmartAsync(client, householdId, "Schnell", new { maxMinutes = 25 });
        var recipeId = await RecipeAsync(client, householdId, "Irgendwas", "Nudeln", Untagged);

        var response = await client.PutAsync(
            $"/api/v1/cookbooks/{cookbookId}/recipes/{recipeId}",
            new { },
            Token);

        // A conflict, not a bad request: it is this cookbook's nature that refuses the call.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            "cookbooks.rules_decide_membership",
            response.Json!.Value.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ItShouldCountAndCoverWhatMatches()
    {
        // The card is drawn from the rules too; a count from cookbook_recipes would say nought.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        var cookbookId = await SmartAsync(client, householdId, "Hähnchen", new
        {
            ingredients = Chicken
        });

        await RecipeAsync(client, householdId, "Eins", "Hähnchenbrust", Untagged);
        await RecipeAsync(client, householdId, "Zwei", "Hähnchenschenkel", Untagged);
        await RecipeAsync(client, householdId, "Nicht dabei", "Tofu", Untagged);

        var shelf = await client.GetAsync($"/api/v1/cookbooks/{cookbookId}", Token);

        Assert.Equal(2, shelf.Json!.Value.GetProperty("recipeCount").GetInt32());
        Assert.Equal("smart", shelf.Json!.Value.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task ItShouldAlreadyBeFullTheMomentItIsMade()
    {
        // A self-filling shelf must not show "0 recipes" while it has four.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        await RecipeAsync(client, householdId, "Schon da", "Hähnchenbrust", Untagged);

        var created = await client.PostAsync(
            "/api/v1/cookbooks",
            new { householdId, name = "Hähnchen", rules = new { ingredients = Chicken } },
            Token);

        Assert.Equal(1, created.Json!.Value.GetProperty("recipeCount").GetInt32());
    }

    [Fact]
    public async Task ItShouldRefuseToBeMadeWithNoRules()
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        var response = await client.PostAsync(
            "/api/v1/cookbooks",
            new { householdId, name = "Alles", rules = new { } },
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "cookbooks.rules_required",
            response.Json!.Value.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ChangingTheRules_ShouldChangeWhatIsOnIt()
    {
        // No backfill or sweep: editing the rules is the whole of editing the shelf.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        var cookbookId = await SmartAsync(client, householdId, "Erst Hähnchen", new
        {
            ingredients = Chicken
        });

        await RecipeAsync(client, householdId, "Hähnchen", "Hähnchenbrust", Untagged);
        await RecipeAsync(client, householdId, "Tofu", "Tofu", Untagged);

        var read = await client.GetAsync($"/api/v1/cookbooks/{cookbookId}", Token);

        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/cookbooks/{cookbookId}")
        {
            Content = JsonContent.Create(new
            {
                name = "Jetzt Tofu",
                rules = new { ingredients = Tofu }
            })
        };

        request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(read.ETag!));

        await client.SendAsync(request, Token);

        var onIt = await client.GetAsync(
            $"/api/v1/recipes?householdId={householdId}&cookbookId={cookbookId}",
            Token);

        Assert.Equal(["Tofu"], Titles(onIt));
    }

    [Fact]
    public async Task ASmartCookbook_ShouldNotBeReadInAnOrderNobodyChose()
    {
        // cookbookOrder sorts by when something was shelved; nothing ever was, so it falls back.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        var cookbookId = await SmartAsync(client, householdId, "Alles Tofu", new
        {
            ingredients = Tofu
        });

        await RecipeAsync(client, householdId, "Eins", "Tofu", Untagged);
        await RecipeAsync(client, householdId, "Zwei", "Tofu", Untagged);

        var onIt = await client.GetAsync(
            $"/api/v1/recipes?householdId={householdId}&cookbookId={cookbookId}&sort=cookbookOrder",
            Token);

        Assert.Equal(HttpStatusCode.OK, onIt.StatusCode);
        Assert.Equal(2, onIt.Json!.Value.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Tags_ShouldComeBackWithHowMuchOfTheCollectionCarriesThem()
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        await RecipeAsync(client, householdId, "Eins", "Nudeln", Main);
        await RecipeAsync(client, householdId, "Zwei", "Nudeln", MainAndQuick);

        var response = await client.GetAsync($"/api/v1/tags?householdId={householdId}", Token);

        var items = response.Json!.Value.GetProperty("items").EnumerateArray().ToList();
        var main = items.Single(item => item.GetProperty("slug").GetString() == "hauptspeise");

        Assert.Equal(2, main.GetProperty("recipeCount").GetInt32());

        // Most used first.
        Assert.Equal("hauptspeise", items[0].GetProperty("slug").GetString());
    }

    private static List<string> Titles(ApiResponse response) =>
        [
            .. response.Json!.Value.GetProperty("items").EnumerateArray()
                .Select(item => item.GetProperty("title").GetString()!)
        ];

    [Fact]
    public async Task Card_ShouldCountExactlyWhatOpeningTheShelfLists()
    {
        // The card's count and the opened list agree only because they share one predicate; this
        // pins that.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        var cookbookId = await SmartAsync(client, householdId, "Schnelle Hauptspeisen", new
        {
            tags = MainCourse,
            ingredients = Chicken,
            maxMinutes = 30
        });

        await RecipeAsync(client, householdId, "Passt", "Hähnchenbrust", Main, prepMinutes: 20);
        await RecipeAsync(client, householdId, "Zu langsam", "Hähnchenbrust", Main, prepMinutes: 90);
        await RecipeAsync(client, householdId, "Falsches Fleisch", "Rindfleisch", Main, prepMinutes: 10);
        await RecipeAsync(client, householdId, "Falsche Kategorie", "Hähnchenbrust", Starter, prepMinutes: 10);
        await RecipeAsync(client, householdId, "Ohne Zeit", "Hähnchenbrust", Main);

        var card = await client.GetAsync($"/api/v1/cookbooks/{cookbookId}", Token);
        var opened = await client.GetAsync(
            $"/api/v1/recipes?householdId={householdId}&cookbookId={cookbookId}",
            Token);

        var counted = card.Json!.Value.GetProperty("recipeCount").GetInt32();
        var listed = opened.Json!.Value.GetProperty("total").GetInt32();

        Assert.Equal(counted, listed);

        Assert.Equal(1, listed);
    }

    [Fact]
    public async Task Rules_ShouldNotBeBorrowed_FromAnotherHouseholdsShelf()
    {
        // A smart shelf has conditions, not rows, so the membership join does not empty a foreign
        // one; unchecked, its rules would reveal what another household's shelf looks for.
        var (ada, grace) = await TwoHouseholdsAsync();
        using var adaClient = ada;
        using var graceClient = grace;

        var adaHousehold = await HouseholdAsync(ada);
        var graceHousehold = await HouseholdAsync(grace);

        var hers = await SmartAsync(grace, graceHousehold, "Schnell", new { maxMinutes = 10 });

        await RecipeAsync(ada, adaHousehold, "Schnelles", "Butter", Untagged, prepMinutes: 5);
        await RecipeAsync(ada, adaHousehold, "Langsames", "Butter", Untagged, prepMinutes: 90);

        var read = await ada.GetAsync(
            $"/api/v1/recipes?householdId={adaHousehold}&cookbookId={hers}",
            Token);

        // Nothing, not "Ada's recipes that happen to match Grace's rule".
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Empty(read.Json!.Value.GetProperty("items").EnumerateArray());
        Assert.Equal(0, read.Json!.Value.GetProperty("total").GetInt32());
    }

    private async Task<(ApiClient Ada, ApiClient Grace)> TwoHouseholdsAsync()
    {
        var ada = await SignedInAsync();

        var settings = postgres.Api.Services.GetRequiredService<RegistrationSettings>();

        settings.OpenRegistration = true;
        settings.RequireInvitation = false;

        var grace = postgres.Api.NewApiClient();

        await grace.PostAsync(
            "/api/v1/users",
            new { email = "grace@example.com", displayName = "Grace", password = Password },
            Token);
        await grace.PostAsync(
            "/api/v1/sessions",
            new { email = "grace@example.com", password = Password },
            Token);
        await grace.PostAsync("/api/v1/households", new { name = "Grace's kitchen" }, Token);

        return (ada, grace);
    }

    private static async Task<Guid> SmartAsync(
        ApiClient client,
        Guid householdId,
        string name,
        object rules)
    {
        var created = await client.PostAsync(
            "/api/v1/cookbooks",
            new { householdId, name, rules },
            Token);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        return created.Json!.Value.GetProperty("cookbookId").GetGuid();
    }

    private static async Task<Guid> RecipeAsync(
        ApiClient client,
        Guid householdId,
        string title,
        string ingredient,
        string[] tags,
        int? prepMinutes = null)
    {
        var created = await client.PostAsync("/api/v1/recipes", new { householdId, title }, Token);
        var recipeId = created.Json!.Value.GetProperty("recipeId").GetGuid();

        await WriteAsync(client, recipeId, title, ingredient, tags, prepMinutes);

        return recipeId;
    }

    private static async Task ReplaceIngredientAsync(ApiClient client, Guid recipeId, string ingredient)
    {
        var read = await client.GetAsync($"/api/v1/recipes/{recipeId}", Token);
        var stored = read.Json!.Value;

        await WriteAsync(
            client,
            recipeId,
            stored.GetProperty("title").GetString()!,
            ingredient,
            Untagged,
            null);
    }

    private static async Task WriteAsync(
        ApiClient client,
        Guid recipeId,
        string title,
        string ingredient,
        string[] tags,
        int? prepMinutes)
    {
        var read = await client.GetAsync($"/api/v1/recipes/{recipeId}", Token);

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/recipes/{recipeId}")
        {
            Content = JsonContent.Create(new
            {
                title,
                language = "de",
                yieldAmount = 4,
                yieldKind = "servings",
                prepMinutes,
                groups = new[]
                {
                    new { ingredients = new[] { new { quantity = 200, unit = "g", name = ingredient } } }
                },
                steps = Array.Empty<object>(),
                tags
            })
        };

        request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(read.ETag!));

        var saved = await client.SendAsync(request, Token);

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
    }

    private static async Task<Guid> HouseholdAsync(ApiClient client)
    {
        var me = await client.GetAsync("/api/v1/users/me", Token);

        return me.Json!.Value.GetProperty("households")[0].GetProperty("householdId").GetGuid();
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
}
