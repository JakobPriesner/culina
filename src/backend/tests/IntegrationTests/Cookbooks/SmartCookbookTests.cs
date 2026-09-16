using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Application.Abstractions.Settings;
using IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Cookbooks;

/// <summary>
/// A shelf that fills itself.
/// </summary>
/// <remarks>
/// The rules are stored and what matches them is not, so the claim worth
/// proving is the one that would be a lie if anything were cached: a recipe
/// written after the cookbook was made is on it, with nothing run in between.
/// </remarks>
[Collection(RequiresDatabase.Name)]
public class SmartCookbookTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A recipe carrying no tags, named so the analyser stops asking.</summary>
    private static readonly string[] Untagged = [];

    /// <summary>The rule these tests keep asking for, for the same reason.</summary>
    private static readonly string[] Chicken = ["Hähnchen"];

    private static readonly string[] Tofu = ["Tofu"];

    private static readonly string[] MainCourse = ["hauptspeise"];

    /// <summary>Tags as somebody would type them, before they are slugged.</summary>
    private static readonly string[] Main = ["Hauptspeise"];

    private static readonly string[] Starter = ["Vorspeise"];

    private static readonly string[] MainAndQuick = ["Hauptspeise", "Schnell"];

    [Fact]
    public async Task ARecipeWrittenAfterwards_ShouldBeOnItWithoutAnythingRunning()
    {
        // Arrange
        // The whole feature, in one test.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        var cookbookId = await SmartAsync(client, householdId, "Hähnchen-Hauptspeisen", new
        {
            tags = MainCourse,
            ingredients = Chicken
        });

        // Act
        await RecipeAsync(client, householdId, "Hähnchenbrust mit Reis", "Hähnchenbrust", Main);

        // Assert
        var onIt = await client.GetAsync(
            $"/api/v1/recipes?householdId={householdId}&cookbookId={cookbookId}",
            Token);

        Assert.Equal(["Hähnchenbrust mit Reis"], Titles(onIt));
    }

    [Fact]
    public async Task EveryRuleMustHold_NotJustOne()
    {
        // Arrange
        // The shelves worth having are the narrow ones, so the rules are ANDed.
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

        // Act
        var onIt = await client.GetAsync(
            $"/api/v1/recipes?householdId={householdId}&cookbookId={cookbookId}",
            Token);

        // Assert
        Assert.Equal(["Beides"], Titles(onIt));
    }

    [Fact]
    public async Task EditingARecipeOutOfTheRules_ShouldTakeItOffTheShelf()
    {
        // Arrange
        // The other half of "it fills itself": it empties itself too, and
        // without a sweep to run or a row to delete.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        var cookbookId = await SmartAsync(client, householdId, "Hähnchen", new
        {
            ingredients = Chicken
        });

        var recipeId = await RecipeAsync(client, householdId, "Wird geändert", "Hähnchenbrust", Untagged);

        // Act
        await ReplaceIngredientAsync(client, recipeId, "Tofu");

        // Assert
        var onIt = await client.GetAsync(
            $"/api/v1/recipes?householdId={householdId}&cookbookId={cookbookId}",
            Token);

        Assert.Empty(onIt.Json!.Value.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task SearchingInside_ShouldNarrowTheRulesRatherThanReplaceThem()
    {
        // Arrange
        // A search inside a smart shelf must not become a search of everything:
        // that would quietly show recipes the shelf does not contain.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        var cookbookId = await SmartAsync(client, householdId, "Hähnchen", new
        {
            ingredients = Chicken
        });

        await RecipeAsync(client, householdId, "Hähnchensuppe", "Hähnchenbrust", Untagged);
        await RecipeAsync(client, householdId, "Hähnchenpfanne", "Hähnchenbrust", Untagged);
        await RecipeAsync(client, householdId, "Linsensuppe", "Linsen", Untagged);

        // Act
        var onIt = await client.GetAsync(
            $"/api/v1/recipes?householdId={householdId}&cookbookId={cookbookId}&query=suppe",
            Token);

        // Assert
        Assert.Equal(["Hähnchensuppe"], Titles(onIt));
    }

    [Fact]
    public async Task TheTimeRule_ShouldExcludeARecipeThatNeverSaidHowLong()
    {
        // Arrange
        // "Everything under 25 minutes" asks for recipes known to fit. An
        // unknown time is not an answer, which is what the search already does.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        var cookbookId = await SmartAsync(client, householdId, "Schnell", new { maxMinutes = 25 });

        await RecipeAsync(client, householdId, "Schnell genug", "Nudeln", Untagged, prepMinutes: 10);
        await RecipeAsync(client, householdId, "Sagt nichts", "Nudeln", Untagged);

        // Act
        var onIt = await client.GetAsync(
            $"/api/v1/recipes?householdId={householdId}&cookbookId={cookbookId}",
            Token);

        // Assert
        Assert.Equal(["Schnell genug"], Titles(onIt));
    }

    [Fact]
    public async Task ItShouldRefuseARecipePutOnByHand()
    {
        // Arrange
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        var cookbookId = await SmartAsync(client, householdId, "Schnell", new { maxMinutes = 25 });
        var recipeId = await RecipeAsync(client, householdId, "Irgendwas", "Nudeln", Untagged);

        // Act
        var response = await client.PutAsync(
            $"/api/v1/cookbooks/{cookbookId}/recipes/{recipeId}",
            new { },
            Token);

        // Assert
        // A conflict, not a bad request: the same call against any other
        // cookbook would be fine. It is this one's nature that refuses it.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            "cookbooks.rules_decide_membership",
            response.Json!.Value.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ItShouldCountAndCoverWhatMatches()
    {
        // Arrange
        // The shelf's own card is drawn from the rules too, so a count taken
        // from cookbook_recipes would say nought on every smart shelf.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        var cookbookId = await SmartAsync(client, householdId, "Hähnchen", new
        {
            ingredients = Chicken
        });

        await RecipeAsync(client, householdId, "Eins", "Hähnchenbrust", Untagged);
        await RecipeAsync(client, householdId, "Zwei", "Hähnchenschenkel", Untagged);
        await RecipeAsync(client, householdId, "Nicht dabei", "Tofu", Untagged);

        // Act
        var shelf = await client.GetAsync($"/api/v1/cookbooks/{cookbookId}", Token);

        // Assert
        Assert.Equal(2, shelf.Json!.Value.GetProperty("recipeCount").GetInt32());
        Assert.Equal("smart", shelf.Json!.Value.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task ItShouldAlreadyBeFullTheMomentItIsMade()
    {
        // Arrange
        // A shelf somebody fills starts empty. One that fills itself never
        // does, and a card saying "0 recipes" on a shelf with four on it is
        // the first thing anybody would notice.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        await RecipeAsync(client, householdId, "Schon da", "Hähnchenbrust", Untagged);

        // Act
        var created = await client.PostAsync(
            "/api/v1/cookbooks",
            new { householdId, name = "Hähnchen", rules = new { ingredients = Chicken } },
            Token);

        // Assert
        Assert.Equal(1, created.Json!.Value.GetProperty("recipeCount").GetInt32());
    }

    [Fact]
    public async Task ItShouldRefuseToBeMadeWithNoRules()
    {
        // Arrange
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        // Act
        var response = await client.PostAsync(
            "/api/v1/cookbooks",
            new { householdId, name = "Alles", rules = new { } },
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "cookbooks.rules_required",
            response.Json!.Value.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ChangingTheRules_ShouldChangeWhatIsOnIt()
    {
        // Arrange
        // No backfill and no sweep: the rules are the only thing stored, so
        // editing them is the whole of editing the shelf.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        var cookbookId = await SmartAsync(client, householdId, "Erst Hähnchen", new
        {
            ingredients = Chicken
        });

        await RecipeAsync(client, householdId, "Hähnchen", "Hähnchenbrust", Untagged);
        await RecipeAsync(client, householdId, "Tofu", "Tofu", Untagged);

        var read = await client.GetAsync($"/api/v1/cookbooks/{cookbookId}", Token);

        // Act
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

        // Assert
        var onIt = await client.GetAsync(
            $"/api/v1/recipes?householdId={householdId}&cookbookId={cookbookId}",
            Token);

        Assert.Equal(["Tofu"], Titles(onIt));
    }

    [Fact]
    public async Task ASmartCookbook_ShouldNotBeReadInAnOrderNobodyChose()
    {
        // Arrange
        // cookbookOrder orders by when something was put on a shelf, and
        // nothing was ever put on this one. Falling back beats ordering every
        // row by a null.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        var cookbookId = await SmartAsync(client, householdId, "Alles Tofu", new
        {
            ingredients = Tofu
        });

        await RecipeAsync(client, householdId, "Eins", "Tofu", Untagged);
        await RecipeAsync(client, householdId, "Zwei", "Tofu", Untagged);

        // Act
        var onIt = await client.GetAsync(
            $"/api/v1/recipes?householdId={householdId}&cookbookId={cookbookId}&sort=cookbookOrder",
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, onIt.StatusCode);
        Assert.Equal(2, onIt.Json!.Value.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Tags_ShouldComeBackWithHowMuchOfTheCollectionCarriesThem()
    {
        // Arrange
        // What the rule editor offers, so nobody has to guess a slug.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        await RecipeAsync(client, householdId, "Eins", "Nudeln", Main);
        await RecipeAsync(client, householdId, "Zwei", "Nudeln", MainAndQuick);

        // Act
        var response = await client.GetAsync($"/api/v1/tags?householdId={householdId}", Token);

        // Assert
        var items = response.Json!.Value.GetProperty("items").EnumerateArray().ToList();
        var main = items.Single(item => item.GetProperty("slug").GetString() == "hauptspeise");

        Assert.Equal(2, main.GetProperty("recipeCount").GetInt32());

        // Most used first, because the words this kitchen reaches for are the
        // ones worth offering.
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
        // Arrange
        // The count on the card and the list you get when you open it are two
        // queries asking the same question. They agree only because they share
        // one predicate, and this is the test that says so — every other smart
        // cookbook test pins one side or the other, never that the two match.
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

        // Act
        var card = await client.GetAsync($"/api/v1/cookbooks/{cookbookId}", Token);
        var opened = await client.GetAsync(
            $"/api/v1/recipes?householdId={householdId}&cookbookId={cookbookId}",
            Token);

        // Assert
        var counted = card.Json!.Value.GetProperty("recipeCount").GetInt32();
        var listed = opened.Json!.Value.GetProperty("total").GetInt32();

        Assert.Equal(counted, listed);

        // And it is the right number, so the two are not agreeing on nothing.
        Assert.Equal(1, listed);
    }

    [Fact]
    public async Task Rules_ShouldNotBeBorrowed_FromAnotherHouseholdsShelf()
    {
        // Arrange
        // A smart shelf carries conditions rather than rows, so a foreign one
        // is not emptied by the membership join the way a manual one is. Left
        // unchecked, its rules would be applied to the caller's own recipes and
        // answer which of them match — telling a stranger what another
        // household's shelf is looking for.
        var (ada, grace) = await TwoHouseholdsAsync();
        using var adaClient = ada;
        using var graceClient = grace;

        var adaHousehold = await HouseholdAsync(ada);
        var graceHousehold = await HouseholdAsync(grace);

        var hers = await SmartAsync(grace, graceHousehold, "Schnell", new { maxMinutes = 10 });

        await RecipeAsync(ada, adaHousehold, "Schnelles", "Butter", Untagged, prepMinutes: 5);
        await RecipeAsync(ada, adaHousehold, "Langsames", "Butter", Untagged, prepMinutes: 90);

        // Act
        var read = await ada.GetAsync(
            $"/api/v1/recipes?householdId={adaHousehold}&cookbookId={hers}",
            Token);

        // Assert
        // Nothing, not "the recipes of Ada's that happen to match Grace's rule".
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Empty(read.Json!.Value.GetProperty("items").EnumerateArray());
        Assert.Equal(0, read.Json!.Value.GetProperty("total").GetInt32());
    }

    /// <summary>Two accounts in two different households, on one instance.</summary>
    private async Task<(ApiClient Ada, ApiClient Grace)> TwoHouseholdsAsync()
    {
        var ada = await SignedInAsync();

        var settings = postgres.Api.Services.GetRequiredService<RegistrationSettings>();

        settings.OpenRegistration = true;

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
