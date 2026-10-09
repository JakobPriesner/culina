using System.Net;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Pipeline;

/// <summary>
/// A hashed asset loads only as a script or style subresource, so it skips the headers that govern documents and workers; everything
/// else keeps the full set. Run against a web root laid out as the frontend build leaves it.
/// </summary>
[Collection(RequiresDatabase.Name)]
public sealed class HashedAssetHeaderTests : IDisposable
{
    private const string Chunk = "/_app/immutable/chunks/app.js";

    private readonly string root = Directory.CreateTempSubdirectory("culina-webroot").FullName;

    private readonly CulinaApiFactory factory;

    public HashedAssetHeaderTests(PostgresFixture postgres)
    {
        Write(Chunk, "export const a = 1;\n");
        Write("/_app/immutable/assets/logo.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
        Write("/service-worker.js", "self.skipWaiting();\n");
        Write("/robots.txt", "User-agent: *\n");
        Write("/index.html", "<!doctype html><script nonce=\"__CULINA_NONCE__\"></script>");

        factory = new CulinaApiFactory(
            postgres,
            new Dictionary<string, string> { ["webroot"] = root });
    }

    [Fact]
    public async Task HashedAsset_ShouldKeepNosniffAndTheResourcePolicy_WithoutTheDocumentHeaders()
    {
        using var response = await GetAsync(Chunk);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["nosniff"], response.Headers.GetValues("X-Content-Type-Options"));
        Assert.Equal(["same-origin"], response.Headers.GetValues("Cross-Origin-Resource-Policy"));
        Assert.Equal(["no-referrer"], response.Headers.GetValues("Referrer-Policy"));

        foreach (var header in DocumentOnly)
        {
            Assert.False(response.Headers.Contains(header), $"{header} should not be set");
        }
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/recipes/some-id")]
    [InlineData("/service-worker.js")]
    [InlineData("/robots.txt")]
    [InlineData("/_app/immutable/chunks/missing.js")]
    [InlineData("/_app/immutable/assets/logo.svg")]
    [InlineData("/api/v1/registration/policy")]
    public async Task EveryOtherResponse_ShouldCarryTheFullSet(string path)
    {
        using var response = await GetAsync(path);

        foreach (var header in DocumentOnly.Append("X-Content-Type-Options").Append("Cross-Origin-Resource-Policy"))
        {
            Assert.True(response.Headers.Contains(header), $"{header} was not set on {path}");
        }
    }

    public void Dispose()
    {
        factory.Dispose();
        Directory.Delete(root, recursive: true);
    }

    private static readonly string[] DocumentOnly =
    [
        "Content-Security-Policy",
        "Permissions-Policy",
        "X-Frame-Options",
        "Cross-Origin-Opener-Policy"
    ];

    private async Task<HttpResponseMessage> GetAsync(string path)
    {
        using var client = factory.CreateClient();

        return await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
    }

    private void Write(string path, string contents)
    {
        var file = Path.Combine(root, path.TrimStart('/'));

        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, contents);
    }
}
