using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Cookbooks;

/// <summary>A chosen cover: one photographed recipe on the shelf, which falls back to the mosaic when that stops being true.</summary>
[Collection(RequiresDatabase.Name)]
public class CookbookCoverTests(PostgresFixture postgres)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ChosenCover_ShouldBeTheWholeCover_AndGoBackToTheMosaic_WhenTheRecipeLeavesTheShelf()
    {
        var kitchen = await Kitchen.OpenAsync(postgres);
        var (cookbookId, ids) = await ShelfAsync(kitchen, "Eins", "Zwei", "Drei");
        var images = new List<Guid>();

        foreach (var (id, at) in ids.Select((id, at) => (id, at)))
        {
            images.Add(await PhotographAsync(kitchen, id, TestImages.Png(600 + (at * 10), 400)));
        }

        var chosen = await UpdateAsync(kitchen, cookbookId, ids[2]);

        Assert.Equal(HttpStatusCode.OK, chosen.StatusCode);
        Assert.Equal(ids[2], chosen.Json!.Value.GetProperty("coverRecipeId").GetGuid());
        Assert.Equal([images[2]], CoverImages(chosen.Json.Value));

        // Listed the same way.
        var listed = (await kitchen.Client.GetAsync($"/api/v1/cookbooks?householdId={kitchen.HouseholdId}", Token))
            .Json!.Value.GetProperty("items").EnumerateArray().Single(one => one.GetProperty("cookbookId").GetGuid() == cookbookId);
        Assert.Equal([images[2]], CoverImages(listed));

        await kitchen.Client.DeleteAsync($"/api/v1/cookbooks/{cookbookId}/recipes/{ids[2]}", Token);
        var after = await kitchen.Client.GetAsync($"/api/v1/cookbooks/{cookbookId}", Token);

        // The choice is kept but draws nothing, so the first photographs are back.
        Assert.Equal(ids[2], after.Json!.Value.GetProperty("coverRecipeId").GetGuid());
        Assert.Equal([images[0], images[1]], CoverImages(after.Json.Value));
    }

    [Fact]
    public async Task Cover_ShouldBeRefused_ForARecipeNotOnTheShelfOrWithoutAPhoto()
    {
        var kitchen = await Kitchen.OpenAsync(postgres);
        var (cookbookId, ids) = await ShelfAsync(kitchen, "Ohne Foto");
        var elsewhere = await kitchen.SaveAsync("Woanders", "de", null, null, [], [], "Kochen.");
        await PhotographAsync(kitchen, elsewhere, TestImages.Png(600, 400));

        foreach (var wrong in new[] { ids[0], elsewhere })
        {
            var refused = await UpdateAsync(kitchen, cookbookId, wrong);

            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.Equal("cookbooks.invalid_cover", refused.ProblemCode);
        }
    }

    [Fact]
    public async Task Cover_ShouldBeClearedByLeavingItOut_SinceTheFormIsSentWhole()
    {
        var kitchen = await Kitchen.OpenAsync(postgres);
        var (cookbookId, ids) = await ShelfAsync(kitchen, "Eins");
        await PhotographAsync(kitchen, ids[0], TestImages.Png(600, 400));
        await UpdateAsync(kitchen, cookbookId, ids[0]);

        var cleared = await UpdateAsync(kitchen, cookbookId, null);

        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        Assert.False(cleared.Json!.Value.TryGetProperty("coverRecipeId", out var cover) && cover.ValueKind != JsonValueKind.Null);
    }

    [Fact]
    public async Task Archive_ShouldCarryTheChosenCover()
    {
        var kitchen = await Kitchen.OpenAsync(postgres);
        var (cookbookId, ids) = await ShelfAsync(kitchen, "Eins", "Zwei");
        await PhotographAsync(kitchen, ids[0], TestImages.Png(600, 400));
        await PhotographAsync(kitchen, ids[1], TestImages.Png(610, 400));
        await UpdateAsync(kitchen, cookbookId, ids[1]);

        var exported = await kitchen.Client.GetAsync($"/api/v1/households/{kitchen.HouseholdId}/archive", Token);
        var file = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(exported.Body));
        file.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
        var restore = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/households/{kitchen.HouseholdId}/archive")
        {
            Content = new MultipartFormDataContent { { file, "file", "archive.json" } }
        };
        Assert.Equal(HttpStatusCode.OK, (await kitchen.Client.SendAsync(restore, Token)).StatusCode);

        var copy = (await kitchen.Client.GetAsync($"/api/v1/cookbooks?householdId={kitchen.HouseholdId}", Token))
            .Json!.Value.GetProperty("items").EnumerateArray().Single(one => one.GetProperty("cookbookId").GetGuid() != cookbookId);
        var coverCopy = copy.GetProperty("coverRecipeId").GetGuid();
        var title = (await kitchen.Client.GetAsync($"/api/v1/recipes/{coverCopy}", Token)).Json!.Value.GetProperty("title").GetString();

        Assert.NotEqual(ids[1], coverCopy);
        Assert.Equal("Zwei", title);
    }

    private static List<Guid> CoverImages(JsonElement cookbook) =>
        [.. cookbook.GetProperty("coverPictures").EnumerateArray().Select(one => one.GetProperty("imageId").GetGuid())];

    private static async Task<ApiResponse> UpdateAsync(Kitchen kitchen, Guid cookbookId, Guid? coverRecipeId)
    {
        var read = await kitchen.Client.GetAsync($"/api/v1/cookbooks/{cookbookId}", Token);
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/cookbooks/{cookbookId}")
        {
            Content = JsonContent.Create(new { name = read.Json!.Value.GetProperty("name").GetString(), coverRecipeId })
        };
        request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(read.ETag!));

        return await kitchen.Client.SendAsync(request, Token);
    }

    private static async Task<(Guid CookbookId, List<Guid> Ids)> ShelfAsync(Kitchen kitchen, params string[] titles)
    {
        var created = await kitchen.Client.PostAsync(
            "/api/v1/cookbooks",
            new { householdId = kitchen.HouseholdId, name = "Mit Bild" },
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

    private static async Task<Guid> PhotographAsync(Kitchen kitchen, Guid recipeId, byte[] png)
    {
        var file = new ByteArrayContent(png);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/recipes/{recipeId}/image")
        {
            Content = new MultipartFormDataContent { { file, "file", "photo.png" } }
        };

        return (await kitchen.Client.SendAsync(request, Token)).Json!.Value.GetProperty("imageId").GetGuid();
    }
}
