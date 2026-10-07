using System.Net;
using System.Text.Json;
using Domain.Searches;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Searches;

/// <summary>What a saved search is, proved against a real database; notably that every remembered order is one the recipe list accepts.</summary>
[Collection(RequiresDatabase.Name)]
public class SavedSearchEndpointTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    private static readonly string[] Vegetarian = ["vegetarisch"];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_ShouldReturnEveryFilterItWasGiven()
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        var response = await client.PostAsync(
            "/api/v1/searches",
            new
            {
                householdId,
                name = "Schnell und fleischlos",
                criteria = new
                {
                    query = "auflauf",
                    tags = Vegetarian,
                    maxMinutes = 30,
                    sort = "totalMinutes"
                }
            },
            Token);

        // A copy, not a translation: a saved search must reopen as the search that was saved.
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var criteria = response.Json!.Value.GetProperty("criteria");

        Assert.Equal("auflauf", criteria.GetProperty("query").GetString());
        Assert.Equal("vegetarisch", criteria.GetProperty("tags")[0].GetString());
        Assert.Equal(30, criteria.GetProperty("maxMinutes").GetInt32());
        Assert.Equal("totalMinutes", criteria.GetProperty("sort").GetString());
    }

    [Fact]
    public async Task Create_ShouldRefuseASearchThatAsksForNothing()
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        var response = await client.PostAsync(
            "/api/v1/searches",
            new { householdId, name = "Alles", criteria = new { tags = Array.Empty<string>() } },
            Token);

        // It would be the library, the screen it is applied from.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_ShouldRefuseASecondSearchWithTheSameName()
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        await SaveAsync(client, householdId, "Feierabend", new { maxMinutes = 20 });

        var second = await SaveAsync(client, householdId, "feierabend", new { maxMinutes = 25 });

        // Case-insensitively: "Feierabend" and "feierabend" are indistinguishable chips.
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Create_ShouldRefuseAnOrderTheLibraryCannotApply()
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        // Legal on GET /recipes only alongside a cookbookId, so a saved search could not carry it.
        var response = await SaveAsync(
            client,
            householdId,
            "Reihenfolge",
            new { sort = "cookbookOrder" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_ShouldBeOldestFirst()
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        await SaveAsync(client, householdId, "Erste", new { maxMinutes = 15 });
        await SaveAsync(client, householdId, "Zweite", new { maxMinutes = 45 });

        var response = await client.GetAsync($"/api/v1/searches?householdId={householdId}", Token);

        // A row of chips that reordered itself is a row nobody learns.
        var items = response.Json!.Value.GetProperty("items").EnumerateArray().ToList();

        Assert.Equal("Erste", items[0].GetProperty("name").GetString());
        Assert.Equal("Zweite", items[1].GetProperty("name").GetString());
    }

    [Fact]
    public async Task List_ShouldBeEmptyForSomebodyElsesHousehold()
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        await SaveAsync(client, householdId, "Meins", new { maxMinutes = 30 });

        using var stranger = await SecondAccountAsync();

        var response = await stranger.GetAsync($"/api/v1/searches?householdId={householdId}", Token);

        // 404, not 403: a stranger learns nothing about which kitchens exist.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ShouldPointAnExistingSearchAtDifferentFilters()
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        var created = await SaveAsync(client, householdId, "Wochenende", new { maxMinutes = 90 });
        var searchId = created.Json!.Value.GetProperty("searchId").GetGuid();

        var response = await client.PatchAsync(
            $"/api/v1/searches/{searchId}",
            new { name = "Wochenende", criteria = new { query = "braten", sort = "-cookCount" } },
            Token);

        // No If-Match: "save what I am looking at now" is a deliberate overwrite, not a clash.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var criteria = response.Json!.Value.GetProperty("criteria");

        Assert.Equal("braten", criteria.GetProperty("query").GetString());
        Assert.Equal("-cookCount", criteria.GetProperty("sort").GetString());
        Assert.False(
            criteria.TryGetProperty("maxMinutes", out var minutes)
            && minutes.ValueKind != JsonValueKind.Null);
    }

    [Fact]
    public async Task Delete_ShouldAnswerTheSameWayTwice()
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        var created = await SaveAsync(client, householdId, "Weg damit", new { maxMinutes = 10 });
        var searchId = created.Json!.Value.GetProperty("searchId").GetGuid();

        await client.DeleteAsync($"/api/v1/searches/{searchId}", Token);

        var again = await client.DeleteAsync($"/api/v1/searches/{searchId}", Token);

        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
    }

    [Theory]
    [MemberData(nameof(EveryOrderASearchMayRemember))]
    public async Task EveryStoredOrder_ShouldBeOneTheRecipeListAccepts(string order)
    {
        // Guards the vocabularies drifting: SearchOrders (what a saved search may hold) vs GetRecipesRequestExtensions (what the list parses).
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        await SaveAsync(client, householdId, $"Nach {order}", new { sort = order });

        var response = await client.GetAsync(
            $"/api/v1/recipes?householdId={householdId}&sort={Uri.EscapeDataString(order)}",
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public static TheoryData<string> EveryOrderASearchMayRemember()
    {
        var orders = new TheoryData<string>();

        foreach (var order in SearchOrders.Known)
        {
            orders.Add(order);
        }

        return orders;
    }

    private static Task<ApiResponse> SaveAsync(
        ApiClient client,
        Guid householdId,
        string name,
        object criteria) =>
        client.PostAsync("/api/v1/searches", new { householdId, name, criteria }, Token);

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

    private async Task<ApiClient> SecondAccountAsync()
    {
        // The first account is the admin, so registration must be opened before a second exists.
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
