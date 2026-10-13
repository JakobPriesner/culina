using System.Net;
using System.Net.Http.Headers;
using Application.Abstractions;
using Application.Abstractions.Settings;
using IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Recipes;

/// <summary>With a bucket configured, a recipe's photo goes there and nowhere else, and comes back out of it.</summary>
[Collection(RequiresDatabase.Name)]
public sealed class RecipeImageOnS3Tests(PostgresFixture postgres, MinioFixture minio) : IClassFixture<MinioFixture>
{
    private const string Password = "correct horse battery staple";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Upload_ShouldStoreEveryWidthInTheBucket_ServeIt_AndRemoveIt()
    {
        var bucket = await minio.NewBucketAsync(Token);
        using var api = new CulinaApiFactory(postgres, new Dictionary<string, string>
        {
            ["Storage:S3:Endpoint"] = bucket.Endpoint,
            ["Storage:S3:Bucket"] = bucket.Bucket,
            ["Storage:S3:AccessKey"] = bucket.AccessKey,
            ["Storage:S3:SecretKey"] = bucket.SecretKey,
            ["Storage:S3:ForcePathStyle"] = "true"
        });
        var (client, recipeId) = await SeedAsync(api);

        var uploaded = await UploadAsync(client, recipeId, TestImages.Png(1200, 800));
        var served = await client.GetAsync($"/api/v1/recipes/{recipeId}/image?w=800", Token);

        Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.Equal("image/webp", served.ContentHeaders.ContentType?.MediaType);
        Assert.Equal(ImageWidths.All.Count, (await MinioFixture.KeysAsync(bucket, Token)).Count);
        Assert.False(Directory.Exists(api.Services.GetRequiredService<StorageSettings>().ImagePath));

        var removed = await client.DeleteAsync($"/api/v1/recipes/{recipeId}/image", Token);

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Empty(await MinioFixture.KeysAsync(bucket, Token));
    }

    private static async Task<ApiResponse> UploadAsync(ApiClient client, Guid recipeId, byte[] bytes)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);

        file.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        content.Add(file, "file", "photo.png");

        return await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Put, $"/api/v1/recipes/{recipeId}/image") { Content = content },
            Token);
    }

    private async Task<(ApiClient Client, Guid RecipeId)> SeedAsync(CulinaApiFactory api)
    {
        await postgres.ResetAsync(Token);

        var client = api.NewApiClient();
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
