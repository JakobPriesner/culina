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

    [Fact]
    public async Task Served_ShouldCarryNoMetadataFromTheOriginal()
    {
        // Arrange
        // A photograph taken in somebody's kitchen carries where that kitchen
        // is. Culina re-encodes every upload, and the re-encoding is the place
        // that has to drop it — an instance shared with a household would
        // otherwise hand out its own address with a picture of dinner.
        var (client, recipeId) = await SeedAsync();

        await UploadAsync(client, recipeId, LocatedPhotograph(), "kitchen.jpg", "image/jpeg");

        // Act
        var served = await client.GetAsync($"/api/v1/recipes/{recipeId}/image?w=400", Token);

        // Assert
        using var decoded = Image.Load(served.Bytes.Span);

        Assert.Null(decoded.Metadata.ExifProfile);
        Assert.Null(decoded.Metadata.XmpProfile);
        Assert.Null(decoded.Metadata.IptcProfile);
    }

    [Fact]
    public async Task Upload_ShouldRefuseAnImageTooLargeToDecode_WithoutDecodingIt()
    {
        // Arrange
        // Ten thousand pixels square of one colour compresses to a few hundred
        // kilobytes, so a byte limit lets it straight through — and decoding it
        // to find out how big it is *is* the attack. The dimensions come from
        // the header, before anything is allocated.
        var (client, recipeId) = await SeedAsync();

        // Act
        var response = await UploadAsync(
            client,
            recipeId,
            PngBytes(10_000, 10_000),
            "huge.png",
            "image/png");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("recipes.image_too_many_pixels", response.ProblemCode);
    }

    /// <summary>A small photograph that says where it was taken.</summary>
    private static byte[] LocatedPhotograph()
    {
        using var image = new Image<Rgba32>(64, 48);
        using var buffer = new MemoryStream();

        var exif = new SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifProfile();

        exif.SetValue(
            SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifTag.GPSLatitudeRef,
            "N");
        exif.SetValue(
            SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifTag.GPSLatitude,
            [new Rational(49), new Rational(47), new Rational(0)]);

        image.Metadata.ExifProfile = exif;
        image.Save(buffer, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder());

        return buffer.ToArray();
    }

    private static byte[] PngBytes(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        using var buffer = new MemoryStream();

        image.Save(buffer, new PngEncoder());

        return buffer.ToArray();
    }

    [Fact]
    public async Task ReplacingOneRecipesPhoto_ShouldNotBreakAnotherUsingTheSameFile()
    {
        // Arrange
        // Storage is content-addressed, so one picture on two recipes is two
        // rows and one file. Importing a library where many recipes carry the
        // same placeholder turns that from a curiosity into the normal case.
        var (client, first) = await SeedAsync();
        var householdId = (await client.GetAsync("/api/v1/households", Token))
            .Json!.Value.GetProperty("items")[0].GetProperty("householdId").GetGuid();
        var second = (await client.PostAsync(
                "/api/v1/recipes",
                new { householdId, title = "Lasagne" },
                Token))
            .Json!.Value.GetProperty("recipeId").GetGuid();

        var shared = PngBytes(500, 500);

        await UploadAsync(client, first, shared, "photo.png", "image/png");
        await UploadAsync(client, second, shared, "photo.png", "image/png");

        // Act
        await UploadAsync(client, first, PngBytes(640, 480), "other.png", "image/png");

        // Assert
        var served = await client.GetAsync($"/api/v1/recipes/{second}/image?w=800", Token);

        // Deleting the displaced file would not have broken the recipe being
        // edited. It would have broken this one, silently.
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
    }

    [Fact]
    public async Task RemovingOneRecipesPhoto_ShouldNotBreakAnotherUsingTheSameFile()
    {
        // Arrange
        var (client, first) = await SeedAsync();
        var householdId = (await client.GetAsync("/api/v1/households", Token))
            .Json!.Value.GetProperty("items")[0].GetProperty("householdId").GetGuid();
        var second = (await client.PostAsync(
                "/api/v1/recipes",
                new { householdId, title = "Lasagne" },
                Token))
            .Json!.Value.GetProperty("recipeId").GetGuid();

        var shared = PngBytes(500, 500);

        await UploadAsync(client, first, shared, "photo.png", "image/png");
        await UploadAsync(client, second, shared, "photo.png", "image/png");

        // Act
        await client.DeleteAsync($"/api/v1/recipes/{first}/image", Token);

        // Assert
        var served = await client.GetAsync($"/api/v1/recipes/{second}/image?w=800", Token);

        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
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
