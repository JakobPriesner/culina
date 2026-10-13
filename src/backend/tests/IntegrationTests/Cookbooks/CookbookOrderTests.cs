using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Cookbooks;

/// <summary>Arranging a chosen shelf: the whole order at once, guarded by the cookbook's version.</summary>
[Collection(RequiresDatabase.Name)]
public class CookbookOrderTests(PostgresFixture postgres)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Reorder_ShouldBeTheOrderTheShelfReadsIn_AndPutANewRecipeLast()
    {
        var kitchen = await Kitchen.OpenAsync(postgres);
        var (cookbookId, ids) = await ShelfAsync(kitchen, "Zuerst", "Dann", "Zuletzt");

        var arranged = await ReorderAsync(kitchen, cookbookId, await ETagAsync(kitchen, cookbookId), [ids[2], ids[0], ids[1]]);
        var later = await kitchen.SaveAsync("Nachzügler", "de", null, null, [], [], "Später.");
        await kitchen.Client.PutAsync($"/api/v1/cookbooks/{cookbookId}/recipes/{later}", new { }, Token);

        Assert.Equal(HttpStatusCode.OK, arranged.StatusCode);
        Assert.NotNull(arranged.ETag);
        Assert.Equal(["Zuletzt", "Zuerst", "Dann", "Nachzügler"], await TitlesAsync(kitchen, cookbookId));
    }

    [Fact]
    public async Task Reorder_ShouldPageInTheArrangedOrder()
    {
        var kitchen = await Kitchen.OpenAsync(postgres);
        var (cookbookId, ids) = await ShelfAsync(kitchen, "A", "B", "C", "D", "E");

        await ReorderAsync(kitchen, cookbookId, await ETagAsync(kitchen, cookbookId), [.. ids.AsEnumerable().Reverse()]);

        List<string> walked = [];
        string? cursor = null;

        do
        {
            var page = await kitchen.Client.GetAsync(
                $"/api/v1/recipes?householdId={kitchen.HouseholdId}&cookbookId={cookbookId}&limit=2"
                + (cursor is null ? string.Empty : $"&cursor={Uri.EscapeDataString(cursor)}"),
                Token);

            walked.AddRange(page.Json!.Value.GetProperty("items").EnumerateArray().Select(one => one.GetProperty("title").GetString()!));
            cursor = page.Json.Value.TryGetProperty("nextCursor", out var next) ? next.GetString() : null;
        }
        while (cursor is not null);

        Assert.Equal(["E", "D", "C", "B", "A"], walked);
    }

    [Fact]
    public async Task Reorder_ShouldRefuseAnOrderThatIsNotExactlyTheShelf()
    {
        var kitchen = await Kitchen.OpenAsync(postgres);
        var (cookbookId, ids) = await ShelfAsync(kitchen, "Eins", "Zwei");
        var stranger = await kitchen.SaveAsync("Nicht drauf", "de", null, null, [], [], "Nichts.");
        var tag = await ETagAsync(kitchen, cookbookId);

        foreach (var wrong in new List<Guid>[] { [ids[0]], [ids[0], ids[0]], [ids[0], stranger], [ids[0], ids[1], stranger] })
        {
            var refused = await ReorderAsync(kitchen, cookbookId, tag, wrong);

            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.Equal("cookbooks.order_mismatch", refused.ProblemCode);
        }

        Assert.Equal(["Eins", "Zwei"], await TitlesAsync(kitchen, cookbookId));
    }

    [Fact]
    public async Task Reorder_ShouldInsistOnAnIfMatch_AndRefuseAStaleOne()
    {
        var kitchen = await Kitchen.OpenAsync(postgres);
        var (cookbookId, ids) = await ShelfAsync(kitchen, "Eins", "Zwei");
        var stale = await ETagAsync(kitchen, cookbookId);

        // Somebody adds a recipe after the order was read: the order sent no longer covers the shelf.
        var added = await kitchen.SaveAsync("Drei", "de", null, null, [], [], "Dazu.");
        await kitchen.Client.PutAsync($"/api/v1/cookbooks/{cookbookId}/recipes/{added}", new { }, Token);

        var missing = await kitchen.Client.PutAsync(
            $"/api/v1/cookbooks/{cookbookId}/recipes",
            new { recipeIds = new[] { ids[1], ids[0] } },
            Token);
        var outdated = await ReorderAsync(kitchen, cookbookId, stale, [ids[1], ids[0]]);

        Assert.Equal(HttpStatusCode.PreconditionRequired, missing.StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, outdated.StatusCode);
        Assert.Equal(["Eins", "Zwei", "Drei"], await TitlesAsync(kitchen, cookbookId));
    }

    [Fact]
    public async Task Reorder_ShouldBeRefused_ForASmartCookbook()
    {
        var kitchen = await Kitchen.OpenAsync(postgres);
        string[] baking = ["backen"];
        var created = await kitchen.Client.PostAsync(
            "/api/v1/cookbooks",
            new { householdId = kitchen.HouseholdId, name = "Backen", rules = new { tags = baking } },
            Token);
        var cookbookId = created.Json!.Value.GetProperty("cookbookId").GetGuid();

        var refused = await ReorderAsync(kitchen, cookbookId, await ETagAsync(kitchen, cookbookId), []);

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("cookbooks.rules_decide_membership", refused.ProblemCode);
    }

    [Fact]
    public async Task Reorder_ShouldSayNotFound_ToSomebodyOutsideTheHousehold()
    {
        var kitchen = await Kitchen.OpenAsync(postgres);
        var (cookbookId, ids) = await ShelfAsync(kitchen, "Eins", "Zwei");
        var tag = await ETagAsync(kitchen, cookbookId);
        using var stranger = await Kitchen.StrangerAsync(postgres);

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/cookbooks/{cookbookId}/recipes")
        {
            Content = JsonContent.Create(new { recipeIds = new[] { ids[1], ids[0] } })
        };
        request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(tag));

        var refused = await stranger.SendAsync(request, Token);

        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        Assert.Equal(["Eins", "Zwei"], await TitlesAsync(kitchen, cookbookId));
    }

    private static async Task<(Guid CookbookId, List<Guid> Ids)> ShelfAsync(Kitchen kitchen, params string[] titles)
    {
        var created = await kitchen.Client.PostAsync(
            "/api/v1/cookbooks",
            new { householdId = kitchen.HouseholdId, name = "Der Reihe nach" },
            Token);
        var cookbookId = created.Json!.Value.GetProperty("cookbookId").GetGuid();
        List<Guid> ids = [];

        foreach (var title in titles)
        {
            var recipeId = await kitchen.SaveAsync(title, "de", null, null, [], [], "Kochen.");

            await kitchen.Client.PutAsync($"/api/v1/cookbooks/{cookbookId}/recipes/{recipeId}", new { }, Token);
            ids.Add(recipeId);
        }

        return (cookbookId, ids);
    }

    private static async Task<string> ETagAsync(Kitchen kitchen, Guid cookbookId) =>
        (await kitchen.Client.GetAsync($"/api/v1/cookbooks/{cookbookId}", Token)).ETag!;

    private static Task<ApiResponse> ReorderAsync(Kitchen kitchen, Guid cookbookId, string ifMatch, List<Guid> order)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/cookbooks/{cookbookId}/recipes")
        {
            Content = JsonContent.Create(new { recipeIds = order })
        };
        request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(ifMatch));

        return kitchen.Client.SendAsync(request, Token);
    }

    private static async Task<List<string>> TitlesAsync(Kitchen kitchen, Guid cookbookId) =>
    [
        .. (await kitchen.Client.GetAsync(
                $"/api/v1/recipes?householdId={kitchen.HouseholdId}&cookbookId={cookbookId}",
                Token))
            .Json!.Value.GetProperty("items").EnumerateArray()
            .Select(one => one.GetProperty("title").GetString()!)
    ];
}
