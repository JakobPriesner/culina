using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
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
        var uploaded = await UploadAsync(client, recipeId, TestImages.Png(1200, 800), "photo.png", "image/png");
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
        await UploadAsync(client, recipeId, TestImages.Png(600, 400), "photo.png", "image/png");

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
        await UploadAsync(client, recipeId, TestImages.Png(600, 400), "photo.png", "image/png");

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
        await UploadAsync(client, recipeId, TestImages.Png(600, 400), "photo.png", "image/png");

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
            TestImages.Png(600, 400),
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
    public async Task Served_ShouldStandTheRightWayUp_WhenTheCameraSaidSoInATag()
    {
        // Arrange
        // A phone photographing a plate from above writes the pixels landscape
        // and adds "turn this a quarter" beside them. Culina throws every tag
        // away on re-encode, so unless the turn has already been made in the
        // pixels, what is served is the dish on its side — and a frame that
        // centres it only centres it sideways.
        var (client, recipeId) = await SeedAsync();

        await UploadAsync(client, recipeId, HeldUpright(), "plate.jpg", "image/jpeg");

        // Act
        var served = await client.GetAsync($"/api/v1/recipes/{recipeId}/image?w=400", Token);

        // Assert
        using var decoded = Image.Load(served.Bytes.Span);

        Assert.True(
            decoded.Height > decoded.Width,
            $"Served {decoded.Width}x{decoded.Height}; the upright photograph came back on its side.");
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
            TestImages.Png(10_000, 10_000),
            "huge.png",
            "image/png");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("recipes.image_too_many_pixels", response.ProblemCode);
    }

    [Fact]
    public async Task Upload_ShouldRefuseAnImageWhoseOneFrameIsTooLargeInMemory_RatherThanFail()
    {
        // Arrange
        // Inside the pixel ceiling, but sixteen bits a channel: each pixel
        // decodes to eight bytes rather than four, so one frame asks for more
        // than a quarter of a gigabyte. The allocator refuses it, and that has
        // to reach the cook as an answer about the image, not as a 500.
        var (client, recipeId) = await SeedAsync();

        // Act
        var response = await UploadAsync(client, recipeId, DeepPng(6000, 6000), "deep.png", "image/png");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("recipes.image_too_many_pixels", response.ProblemCode);
    }

    /// <summary>A blank PNG at sixteen bits a channel.</summary>
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
        // Arrange
        // The pixel limit is checked on one frame, and an animation is decoded
        // as a whole canvas per frame: a GIF under a kilobyte can declare a
        // large screen and thousands of frames, and every one of them is a
        // full allocation. Only the first frame may ever be decoded.
        var (client, recipeId) = await SeedAsync();

        // Act
        await UploadAsync(client, recipeId, Animated(), "dancing.gif", "image/gif");
        var served = await client.GetAsync($"/api/v1/recipes/{recipeId}/image?w=400", Token);

        // Assert
        using var decoded = Image.Load(served.Bytes.Span);

        Assert.Single(decoded.Frames);
    }

    /// <summary>A small GIF of three different frames.</summary>
    private static byte[] Animated()
    {
        using var image = new Image<Rgba32>(64, 64, Color.Red);
        using var buffer = new MemoryStream();

        image.Frames.CreateFrame(Color.Green);
        image.Frames.CreateFrame(Color.Blue);
        image.Save(buffer, new SixLabors.ImageSharp.Formats.Gif.GifEncoder());

        return buffer.ToArray();
    }

    /// <summary>
    /// A photograph taken upright, stored the way a camera stores one: wide
    /// pixels, plus the tag that says to turn them.
    /// </summary>
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

        var shared = TestImages.Png(500, 500);

        await UploadAsync(client, first, shared, "photo.png", "image/png");
        await UploadAsync(client, second, shared, "photo.png", "image/png");

        // Act
        await UploadAsync(client, first, TestImages.Png(640, 480), "other.png", "image/png");

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

        var shared = TestImages.Png(500, 500);

        await UploadAsync(client, first, shared, "photo.png", "image/png");
        await UploadAsync(client, second, shared, "photo.png", "image/png");

        // Act
        await client.DeleteAsync($"/api/v1/recipes/{first}/image", Token);

        // Assert
        var served = await client.GetAsync($"/api/v1/recipes/{second}/image?w=800", Token);

        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
    }

    [Fact]
    public async Task Copy_ShouldShareThePicture_AndKeepItWhenTheOriginalLosesItsOwn()
    {
        // Arrange
        var (client, original) = await SeedAsync();
        var householdId = (await client.GetAsync("/api/v1/households", Token))
            .Json!.Value.GetProperty("items")[0].GetProperty("householdId").GetGuid();
        await UploadAsync(client, original, TestImages.Png(600, 400), "photo.png", "image/png");

        // Act
        var copied = await client.PostAsync($"/api/v1/recipes/{original}/copies", new { householdId }, Token);
        var copy = copied.Json!.Value.GetProperty("recipeId").GetGuid();
        await client.DeleteAsync($"/api/v1/recipes/{original}/image", Token);

        // Assert
        // One file and two rows, like any picture two recipes share: the copy
        // is not left pointing at nothing when the original lets go of it.
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

    /// <summary>
    /// Drawing on an instance with no assistant, which is nearly every one.
    /// </summary>
    /// <remarks>
    /// A status code and not a stream, which is the whole reason the checks run
    /// before the answer opens. They did not once: drawing streamed a 200
    /// carrying a failure event instead, so an instance with no model answered
    /// "here is your picture being made" and then, seconds later, that it could
    /// not be. Nothing covered this route at all, which is how that got out.
    /// </remarks>
    [Fact]
    public async Task Draw_ShouldSayThereIsNothingHere_WhenNoAssistantIsConnected()
    {
        // Arrange
        var (client, recipeId) = await SeedAsync();

        // Act
        // No body and no content type, exactly as the browser asks for it.
        var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, $"/api/v1/recipes/{recipeId}/image"),
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("assistance.not_configured", response.ProblemCode);

        // And nothing that looks like the beginning of an answer.
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
