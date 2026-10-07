using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using IntegrationTests.Fixtures;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace IntegrationTests.Recipes;

/// <summary>Every uploaded file is treated as hostile: the bytes decide what it is, and only Culina's re-encoding is served.</summary>
[Collection(RequiresDatabase.Name)]
public class RecipeImageTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task Upload_ShouldAttachTheImage_AndServeItAsWebp()
    {
        var (client, recipeId) = await SeedAsync();

        var uploaded = await UploadAsync(client, recipeId, TestImages.Png(1200, 800), "photo.png", "image/png");
        var served = await client.GetAsync($"/api/v1/recipes/{recipeId}/image?w=800", Token);

        Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
        Assert.NotEqual(Guid.Empty, uploaded.Json!.Value.GetProperty("imageId").GetGuid());
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.Equal("image/webp", served.ContentHeaders.ContentType?.MediaType);
    }

    [Fact]
    public async Task Upload_ShouldRejectAFileThatLiesAboutWhatItIs()
    {
        var (client, recipeId) = await SeedAsync();
        var notAnImage = System.Text.Encoding.UTF8.GetBytes("<?php echo 'hello'; ?>");

        var response = await UploadAsync(client, recipeId, notAnImage, "photo.png", "image/png");

        // The content type and extension said PNG; the bytes did not.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("recipes.image_unreadable", response.ProblemCode);
    }

    [Fact]
    public async Task Served_ShouldCarryAContentHashETag_AndBePrivate()
    {
        var (client, recipeId) = await SeedAsync();
        await UploadAsync(client, recipeId, TestImages.Png(600, 400), "photo.png", "image/png");

        var served = await client.GetAsync($"/api/v1/recipes/{recipeId}/image", Token);

        Assert.NotNull(served.ETag);
        Assert.Contains("private", served.Headers.CacheControl!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Served_ShouldRefuseAWidthItDoesNotKeep()
    {
        var (client, recipeId) = await SeedAsync();
        await UploadAsync(client, recipeId, TestImages.Png(600, 400), "photo.png", "image/png");

        var response = await client.GetAsync($"/api/v1/recipes/{recipeId}/image?w=1234", Token);

        // An arbitrary width would be a denial-of-service lever and a cache that never warms.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("recipes.image_unknown_width", response.ProblemCode);
    }

    [Fact]
    public async Task Served_ShouldBeNotFound_WhenTheRecipeHasNoImage()
    {
        var (client, recipeId) = await SeedAsync();

        var response = await client.GetAsync($"/api/v1/recipes/{recipeId}/image", Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Remove_ShouldDetachTheImage_AndStopServingIt()
    {
        var (client, recipeId) = await SeedAsync();
        await UploadAsync(client, recipeId, TestImages.Png(600, 400), "photo.png", "image/png");

        var removed = await client.DeleteAsync($"/api/v1/recipes/{recipeId}/image", Token);
        var served = await client.GetAsync($"/api/v1/recipes/{recipeId}/image", Token);

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, served.StatusCode);
    }

    [Fact]
    public async Task Upload_ShouldBeRefused_ForARecipeInAnotherHousehold()
    {
        var (client, _) = await SeedAsync();
        var foreignRecipeId = Guid.CreateVersion7();

        var response = await UploadAsync(
            client,
            foreignRecipeId,
            TestImages.Png(600, 400),
            "photo.png",
            "image/png");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Served_ShouldCarryNoMetadataFromTheOriginal()
    {
        // Photos carry GPS location; the re-encode must drop it.
        var (client, recipeId) = await SeedAsync();

        await UploadAsync(client, recipeId, TestImages.LocatedPhotograph(), "kitchen.jpg", "image/jpeg");

        var served = await client.GetAsync($"/api/v1/recipes/{recipeId}/image?w=400", Token);

        using var decoded = Image.Load(served.Bytes.Span);

        Assert.Null(decoded.Metadata.ExifProfile);
        Assert.Null(decoded.Metadata.XmpProfile);
        Assert.Null(decoded.Metadata.IptcProfile);
    }

    [Fact]
    public async Task Served_ShouldStandTheRightWayUp_WhenTheCameraSaidSoInATag()
    {
        // Tags are dropped on re-encode, so the EXIF rotation must already be applied to the pixels.
        var (client, recipeId) = await SeedAsync();

        await UploadAsync(client, recipeId, HeldUpright(), "plate.jpg", "image/jpeg");

        var served = await client.GetAsync($"/api/v1/recipes/{recipeId}/image?w=400", Token);

        using var decoded = Image.Load(served.Bytes.Span);

        Assert.True(
            decoded.Height > decoded.Width,
            $"Served {decoded.Width}x{decoded.Height}; the upright photograph came back on its side.");
    }

    [Fact]
    public async Task Upload_ShouldRefuseAnImageTooLargeToDecode_WithoutDecodingIt()
    {
        // A byte limit passes a huge single-colour image; the dimensions must come from the header, before allocating.
        var (client, recipeId) = await SeedAsync();

        var response = await UploadAsync(
            client,
            recipeId,
            TestImages.Png(10_000, 10_000),
            "huge.png",
            "image/png");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("recipes.image_too_many_pixels", response.ProblemCode);
    }

    [Fact]
    public async Task Upload_ShouldRefuseAnImageWhoseOneFrameIsTooLargeInMemory_RatherThanFail()
    {
        // 16-bit channels quadruple decode memory; the allocator's refusal must surface as an image error, not a 500.
        var (client, recipeId) = await SeedAsync();

        var response = await UploadAsync(client, recipeId, DeepPng(6000, 6000), "deep.png", "image/png");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("recipes.image_too_many_pixels", response.ProblemCode);
    }

    private static byte[] DeepPng(int width, int height)
    {
        using var image = new Image<Rgba64>(width, height);
        using var buffer = new MemoryStream();

        image.Save(buffer, new PngEncoder { BitDepth = PngBitDepth.Bit16, ColorType = PngColorType.RgbWithAlpha });

        return buffer.ToArray();
    }

    [Fact]
    public async Task Upload_ShouldKeepOnlyTheFirstFrame_OfAnAnimatedImage()
    {
        // A GIF can declare many full-canvas frames; only the first may be decoded.
        var (client, recipeId) = await SeedAsync();

        await UploadAsync(client, recipeId, Animated(), "dancing.gif", "image/gif");
        var served = await client.GetAsync($"/api/v1/recipes/{recipeId}/image?w=400", Token);

        using var decoded = Image.Load(served.Bytes.Span);

        Assert.Single(decoded.Frames);
    }

    private static byte[] Animated()
    {
        using var image = new Image<Rgba32>(64, 64, Color.Red);
        using var buffer = new MemoryStream();

        image.Frames.CreateFrame(Color.Green);
        image.Frames.CreateFrame(Color.Blue);
        image.Save(buffer, new SixLabors.ImageSharp.Formats.Gif.GifEncoder());

        return buffer.ToArray();
    }

    /// <summary>A photograph stored as a camera does: wide pixels plus the EXIF tag to turn them.</summary>
    private static byte[] HeldUpright()
    {
        using var image = new Image<Rgba32>(200, 100);
        using var buffer = new MemoryStream();

        var exif = new SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifProfile();

        // 6: rotate a quarter turn clockwise to display.
        exif.SetValue(
            SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifTag.Orientation,
            (ushort)6);

        image.Metadata.ExifProfile = exif;
        image.Save(buffer, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder());

        return buffer.ToArray();
    }

    [Fact]
    public async Task ReplacingOneRecipesPhoto_ShouldNotBreakAnotherUsingTheSameFile()
    {
        // Storage is content-addressed: one picture on two recipes is two rows and one file.
        var (client, first) = await SeedAsync();
        var householdId = (await client.GetAsync("/api/v1/households", Token))
            .Json!.Value.GetProperty("items")[0].GetProperty("householdId").GetGuid();
        var second = (await client.PostAsync(
                "/api/v1/recipes",
                new { householdId, title = "Lasagne" },
                Token))
            .Json!.Value.GetProperty("recipeId").GetGuid();

        var shared = TestImages.Png(500, 500);

        await UploadAsync(client, first, shared, "photo.png", "image/png");
        await UploadAsync(client, second, shared, "photo.png", "image/png");

        await UploadAsync(client, first, TestImages.Png(640, 480), "other.png", "image/png");

        var served = await client.GetAsync($"/api/v1/recipes/{second}/image?w=800", Token);

        // Deleting the displaced file would have broken the other recipe.
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
    }

    [Fact]
    public async Task RemovingOneRecipesPhoto_ShouldNotBreakAnotherUsingTheSameFile()
    {
        var (client, first) = await SeedAsync();
        var householdId = (await client.GetAsync("/api/v1/households", Token))
            .Json!.Value.GetProperty("items")[0].GetProperty("householdId").GetGuid();
        var second = (await client.PostAsync(
                "/api/v1/recipes",
                new { householdId, title = "Lasagne" },
                Token))
            .Json!.Value.GetProperty("recipeId").GetGuid();

        var shared = TestImages.Png(500, 500);

        await UploadAsync(client, first, shared, "photo.png", "image/png");
        await UploadAsync(client, second, shared, "photo.png", "image/png");

        await client.DeleteAsync($"/api/v1/recipes/{first}/image", Token);

        var served = await client.GetAsync($"/api/v1/recipes/{second}/image?w=800", Token);

        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
    }

    [Fact]
    public async Task RemovingARecipesPhoto_ShouldNotBreakACookPhotoOfTheSameFile()
    {
        // Re-encoding is deterministic, so a recipe picture and a cook photo of the same image share one file.
        var (client, recipeId, entryId) = await SharedWithACookPhotoAsync();

        await client.DeleteAsync($"/api/v1/recipes/{recipeId}/image", Token);

        var served = await client.GetAsync(CookPhoto(recipeId, entryId), Token);

        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
    }

    [Fact]
    public async Task ReplacingARecipesPhoto_ShouldNotBreakACookPhotoOfTheSameFile()
    {
        var (client, recipeId, entryId) = await SharedWithACookPhotoAsync();

        await UploadAsync(client, recipeId, TestImages.Png(640, 480), "other.png", "image/png");

        var served = await client.GetAsync(CookPhoto(recipeId, entryId), Token);

        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
    }

    private static string CookPhoto(Guid recipeId, Guid entryId) =>
        $"/api/v1/recipes/{recipeId}/cook-log/{entryId}/photo";

    private async Task<(ApiClient Client, Guid RecipeId, Guid EntryId)> SharedWithACookPhotoAsync()
    {
        var (client, recipeId) = await SeedAsync();
        var entryId = (await client.PostAsync($"/api/v1/recipes/{recipeId}/cook-log", new { }, Token))
            .Json!.Value.GetProperty("entryId").GetGuid();
        var shared = TestImages.Png(500, 500);

        await UploadAsync(client, recipeId, shared, "photo.png", "image/png");

        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(shared);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        content.Add(file, "file", "attempt.png");
        await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Put, CookPhoto(recipeId, entryId)) { Content = content },
            Token);

        var picture = await client.GetAsync($"/api/v1/recipes/{recipeId}/image?w=800", Token);
        var photo = await client.GetAsync(CookPhoto(recipeId, entryId), Token);
        Assert.Equal(picture.ETag, photo.ETag);

        return (client, recipeId, entryId);
    }

    [Fact]
    public async Task Copy_ShouldShareThePicture_AndKeepItWhenTheOriginalLosesItsOwn()
    {
        var (client, original) = await SeedAsync();
        var householdId = (await client.GetAsync("/api/v1/households", Token))
            .Json!.Value.GetProperty("items")[0].GetProperty("householdId").GetGuid();
        await UploadAsync(client, original, TestImages.Png(600, 400), "photo.png", "image/png");

        var copied = await client.PostAsync($"/api/v1/recipes/{original}/copies", new { householdId }, Token);
        var copy = copied.Json!.Value.GetProperty("recipeId").GetGuid();
        await client.DeleteAsync($"/api/v1/recipes/{original}/image", Token);

        // The copy must not be left pointing at nothing when the original lets go of the shared file.
        Assert.NotEqual(JsonValueKind.Null, copied.Json!.Value.GetProperty("imageId").ValueKind);
        var served = await client.GetAsync($"/api/v1/recipes/{copy}/image?w=800", Token);
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

    /// <summary>Drawing with no assistant must answer with a status code, not a 200 stream carrying a failure.</summary>
    [Fact]
    public async Task Draw_ShouldSayThereIsNothingHere_WhenNoAssistantIsConnected()
    {
        var (client, recipeId) = await SeedAsync();

        var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, $"/api/v1/recipes/{recipeId}/image"),
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("assistance.not_configured", response.ProblemCode);

        Assert.NotEqual("text/event-stream", response.ContentHeaders.ContentType?.MediaType);
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
