using System.Net;
using System.Net.Http.Headers;
using IntegrationTests.Fixtures;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace IntegrationTests.Recipes;

/// <summary>
/// The photograph of one attempt, and how a browser is told to cache it.
/// </summary>
/// <remarks>
/// This route had no tests at all, which is how it kept an hour of freshness
/// after the two other image routes were changed to revalidate. A cook photo is
/// replaced under the address it is served from — the PUT and the GET are the
/// same URL, and nothing versions it — so an hour of freshness meant replacing
/// one left the old picture on screen.
/// </remarks>
[Collection(RequiresDatabase.Name)]
public class CookPhotoTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task Served_ShouldBeRevalidated_NotFreshForAnHour()
    {
        // Arrange
        var (client, recipeId, entryId) = await SeedAsync();
        await UploadAsync(client, recipeId, entryId, PngBytes(600, 400));

        // Act
        var served = await client.GetAsync(Photo(recipeId, entryId), Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.NotNull(served.ETag);

        var caching = served.Headers.CacheControl!.ToString();

        // Private, because it is one person's photograph; no-cache, because the
        // next one arrives at this same address.
        Assert.Contains("private", caching, StringComparison.Ordinal);
        Assert.Contains("no-cache", caching, StringComparison.Ordinal);
        Assert.DoesNotContain("max-age=3600", caching, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Served_ShouldAnswerNotModified_WhenTheCallerAlreadyHasThisPhoto()
    {
        // Arrange
        var (client, recipeId, entryId) = await SeedAsync();
        await UploadAsync(client, recipeId, entryId, PngBytes(600, 400));

        var first = await client.GetAsync(Photo(recipeId, entryId), Token);

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, Photo(recipeId, entryId));
        request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(first.ETag!));
        var again = await client.SendAsync(request, Token);

        // Assert
        // Revalidating costs a request and no bytes. Without this the route was
        // paying for the round trip and sending the picture anyway.
        Assert.Equal(HttpStatusCode.NotModified, again.StatusCode);
    }

    [Fact]
    public async Task Served_ShouldSendTheNewPicture_WhenThePhotoWasReplaced()
    {
        // Arrange
        var (client, recipeId, entryId) = await SeedAsync();
        await UploadAsync(client, recipeId, entryId, PngBytes(600, 400));

        var before = await client.GetAsync(Photo(recipeId, entryId), Token);

        // Act
        await UploadAsync(client, recipeId, entryId, PngBytes(320, 240));

        var request = new HttpRequestMessage(HttpMethod.Get, Photo(recipeId, entryId));
        request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(before.ETag!));
        var after = await client.SendAsync(request, Token);

        // Assert
        // The tag is the content hash, so a replaced picture cannot match the
        // one the caller is holding.
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        Assert.NotEqual(before.ETag, after.ETag);
    }

    [Fact]
    public async Task Served_ShouldRefuseAWidthItDoesNotKeep()
    {
        // Arrange
        var (client, recipeId, entryId) = await SeedAsync();
        await UploadAsync(client, recipeId, entryId, PngBytes(600, 400));

        // Act
        var response = await client.GetAsync($"{Photo(recipeId, entryId)}?w=1234", Token);

        // Assert
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

    private static byte[] PngBytes(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        using var buffer = new MemoryStream();

        image.Save(buffer, new PngEncoder());

        return buffer.ToArray();
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
