using System.Net;
using System.Net.Http.Headers;
using Application.Abstractions.Settings;
using IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Recipes;

/// <summary>The photograph of one attempt and how a browser caches it: it is replaced under the address it is served from, so an hour of freshness would keep the old picture on screen.</summary>
[Collection(RequiresDatabase.Name)]
public class CookPhotoTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task Served_ShouldBeRevalidated_NotFreshForAnHour()
    {
        var (client, recipeId, entryId) = await SeedAsync();
        await UploadAsync(client, recipeId, entryId, TestImages.Png(600, 400));

        var served = await client.GetAsync(Photo(recipeId, entryId), Token);

        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.NotNull(served.ETag);

        var caching = served.Headers.CacheControl!.ToString();

        // Private (one person's photograph) and no-cache (the next arrives at the same address).
        Assert.Contains("private", caching, StringComparison.Ordinal);
        Assert.Contains("no-cache", caching, StringComparison.Ordinal);
        Assert.DoesNotContain("max-age=3600", caching, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Served_ShouldAnswerNotModified_WhenTheCallerAlreadyHasThisPhoto()
    {
        var (client, recipeId, entryId) = await SeedAsync();
        await UploadAsync(client, recipeId, entryId, TestImages.Png(600, 400));

        var first = await client.GetAsync(Photo(recipeId, entryId), Token);

        var request = new HttpRequestMessage(HttpMethod.Get, Photo(recipeId, entryId));
        request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(first.ETag!));
        var again = await client.SendAsync(request, Token);

        // Revalidating costs a request and no bytes.
        Assert.Equal(HttpStatusCode.NotModified, again.StatusCode);
    }

    [Fact]
    public async Task Served_ShouldAnswerNotModified_WithoutReadingTheFile()
    {
        var (client, recipeId, entryId) = await SeedAsync();
        await UploadAsync(client, recipeId, entryId, TestImages.Png(610, 410));

        var first = await client.GetAsync(Photo(recipeId, entryId), Token);
        var hash = first.ETag!.Trim('"');

        // Take the file away: a 304 that still reads it would now fail with a 404.
        var storage = postgres.Api.Services.GetRequiredService<StorageSettings>();

        foreach (var file in Directory.GetFiles(
                     Path.Combine(storage.ImagePath, hash[..2], hash[2..4]),
                     $"{hash}-*.webp"))
        {
            File.Delete(file);
        }

        var request = new HttpRequestMessage(HttpMethod.Get, Photo(recipeId, entryId));
        request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(first.ETag!));
        var again = await client.SendAsync(request, Token);

        // The hash is enough to know the caller has these bytes.
        Assert.Equal(HttpStatusCode.NotModified, again.StatusCode);
        Assert.Equal(first.ETag, again.ETag);
    }

    [Fact]
    public async Task Served_ShouldSendTheNewPicture_WhenThePhotoWasReplaced()
    {
        var (client, recipeId, entryId) = await SeedAsync();
        await UploadAsync(client, recipeId, entryId, TestImages.Png(600, 400));

        var before = await client.GetAsync(Photo(recipeId, entryId), Token);

        await UploadAsync(client, recipeId, entryId, TestImages.Png(320, 240));

        var request = new HttpRequestMessage(HttpMethod.Get, Photo(recipeId, entryId));
        request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(before.ETag!));
        var after = await client.SendAsync(request, Token);

        // The tag is the content hash, so a replaced picture cannot match the one the caller holds.
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        Assert.NotEqual(before.ETag, after.ETag);
    }

    [Fact]
    public async Task Served_ShouldRefuseAWidthItDoesNotKeep()
    {
        var (client, recipeId, entryId) = await SeedAsync();
        await UploadAsync(client, recipeId, entryId, TestImages.Png(600, 400));

        var response = await client.GetAsync($"{Photo(recipeId, entryId)}?w=1234", Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("recipes.image_unknown_width", response.ProblemCode);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Photo(Guid recipeId, Guid entryId) =>
        $"/api/v1/recipes/{recipeId}/cook-log/{entryId}/photo";

    private static async Task<ApiResponse> UploadAsync(
        ApiClient client,
        Guid recipeId,
        Guid entryId,
        byte[] bytes)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);

        file.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        content.Add(file, "file", "attempt.png");

        var request = new HttpRequestMessage(HttpMethod.Put, Photo(recipeId, entryId))
        {
            Content = content
        };

        return await client.SendAsync(request, Token);
    }

    private async Task<(ApiClient Client, Guid RecipeId, Guid EntryId)> SeedAsync()
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
        var recipeId = (await client.PostAsync(
                "/api/v1/recipes",
                new { householdId, title = "Bolognese" },
                Token))
            .Json!.Value.GetProperty("recipeId").GetGuid();
        var entryId = (await client.PostAsync(
                $"/api/v1/recipes/{recipeId}/cook-log",
                new { },
                Token))
            .Json!.Value.GetProperty("entryId").GetGuid();

        return (client, recipeId, entryId);
    }
}
