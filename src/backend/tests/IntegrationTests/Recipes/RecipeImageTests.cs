using System.Net;
using System.Net.Http.Headers;
using IntegrationTests.Fixtures;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace IntegrationTests.Recipes;

/// <summary>
/// Every uploaded file is treated as hostile: the bytes decide what it is, and
/// what is served back is always Culina's own re-encoding.
/// </summary>
[Collection(RequiresDatabase.Name)]
public class RecipeImageTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task Upload_ShouldAttachTheImage_AndServeItAsWebp()
    {
        // Arrange
        var (client, recipeId) = await SeedAsync();

        // Act
        var uploaded = await UploadAsync(client, recipeId, PngBytes(1200, 800), "photo.png", "image/png");
        var served = await client.GetAsync($"/api/v1/recipes/{recipeId}/image?w=800", Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
        Assert.NotEqual(Guid.Empty, uploaded.Json!.Value.GetProperty("imageId").GetGuid());
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        // Always re-encoded, whatever went in.
        Assert.Equal("image/webp", served.ContentHeaders.ContentType?.MediaType);
    }

    [Fact]
    public async Task Upload_ShouldRejectAFileThatLiesAboutWhatItIs()
    {
        // Arrange
        var (client, recipeId) = await SeedAsync();
        var notAnImage = System.Text.Encoding.UTF8.GetBytes("<?php echo 'hello'; ?>");

        // Act
        var response = await UploadAsync(client, recipeId, notAnImage, "photo.png", "image/png");

        // Assert
        // The content type said PNG and the extension agreed; the bytes did not.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("recipes.image_unreadable", response.ProblemCode);
    }

    [Fact]
    public async Task Served_ShouldCarryAContentHashETag_AndBePrivate()
    {
        // Arrange
        var (client, recipeId) = await SeedAsync();
        await UploadAsync(client, recipeId, PngBytes(600, 400), "photo.png", "image/png");

        // Act
        var served = await client.GetAsync($"/api/v1/recipes/{recipeId}/image", Token);

        // Assert
        // An image is exactly as private as the recipe it belongs to.
        Assert.NotNull(served.ETag);
        Assert.Contains("private", served.Headers.CacheControl!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Served_ShouldRefuseAWidthItDoesNotKeep()
    {
        // Arrange
        var (client, recipeId) = await SeedAsync();
        await UploadAsync(client, recipeId, PngBytes(600, 400), "photo.png", "image/png");

        // Act
        var response = await client.GetAsync($"/api/v1/recipes/{recipeId}/image?w=1234", Token);

        // Assert
        // An arbitrary width would be a denial-of-service lever and a cache that
        // never warms.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("recipes.image_unknown_width", response.ProblemCode);
    }

    [Fact]
    public async Task Served_ShouldBeNotFound_WhenTheRecipeHasNoImage()
    {
        // Arrange
        var (client, recipeId) = await SeedAsync();

        // Act
        var response = await client.GetAsync($"/api/v1/recipes/{recipeId}/image", Token);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Remove_ShouldDetachTheImage_AndStopServingIt()
    {
        // Arrange
        var (client, recipeId) = await SeedAsync();
        await UploadAsync(client, recipeId, PngBytes(600, 400), "photo.png", "image/png");

        // Act
        var removed = await client.DeleteAsync($"/api/v1/recipes/{recipeId}/image", Token);
        var served = await client.GetAsync($"/api/v1/recipes/{recipeId}/image", Token);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, served.StatusCode);
    }

    [Fact]
    public async Task Upload_ShouldBeRefused_ForARecipeInAnotherHousehold()
    {
        // Arrange
        var (client, _) = await SeedAsync();
        var foreignRecipeId = Guid.CreateVersion7();

        // Act
        var response = await UploadAsync(
            client,
            foreignRecipeId,
            PngBytes(600, 400),
            "photo.png",
            "image/png");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static byte[] PngBytes(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        using var buffer = new MemoryStream();

        image.Save(buffer, new PngEncoder());

        return buffer.ToArray();
    }

    private static async Task<ApiResponse> UploadAsync(
        ApiClient client,
        Guid recipeId,
        byte[] bytes,
        string fileName,
        string contentType)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);

        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        content.Add(file, "file", fileName);

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/recipes/{recipeId}/image")
        {
            Content = content
        };

        return await client.SendAsync(request, Token);
    }

    private async Task<(ApiClient Client, Guid RecipeId)> SeedAsync()
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

        return (client, recipeId);
    }
}
