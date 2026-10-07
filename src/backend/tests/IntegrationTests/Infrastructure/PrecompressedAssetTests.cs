using System.IO.Compression;
using System.Net;
using System.Text;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Infrastructure;

/// <summary>
/// Which of a static file's copies is sent and what the response says about it, through the whole pipeline against a web root laid out as the
/// frontend build leaves it (the client does not decompress, so bodies are the host's exact bytes).
/// </summary>
[Collection(RequiresDatabase.Name)]
public sealed class PrecompressedAssetTests : IDisposable
{
    private const string Chunk = "/_app/immutable/chunks/app.js";

    private static readonly string Script = string.Concat(Enumerable.Repeat("export const a = 1;\n", 200));

    private readonly string root = Directory.CreateTempSubdirectory("culina-webroot").FullName;

    private readonly CulinaApiFactory factory;

    public PrecompressedAssetTests(PostgresFixture postgres)
    {
        Write(Chunk, Script);
        Write("/service-worker.js", Script);
        Write("/robots.txt", "User-agent: *\n");

        factory = new CulinaApiFactory(
            postgres,
            new Dictionary<string, string> { ["webroot"] = root });
    }

    [Fact]
    public async Task Get_ShouldSendBrotli_WhenTheClientAcceptsIt()
    {
        using var response = await GetAsync(Chunk, "gzip, deflate, br");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["br"], response.Content.Headers.ContentEncoding);
        Assert.Equal(Script, await DecodeAsync(response, "br"));
    }

    [Fact]
    public async Task Get_ShouldStillNameTheFilesOwnType_WhenSendingACopy()
    {
        using var response = await GetAsync(Chunk, "br");

        // Unregistered, a ".br" file is refused outright; by its last extension it would be an octet stream no browser runs.
        Assert.Equal("text/javascript", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Get_ShouldSendGzip_WhenThatIsAllTheClientAccepts()
    {
        using var response = await GetAsync(Chunk, "gzip");

        Assert.Equal(["gzip"], response.Content.Headers.ContentEncoding);
        Assert.Equal(Script, await DecodeAsync(response, "gzip"));
    }

    [Fact]
    public async Task Get_ShouldSendTheFileAsBuilt_WhenTheClientAcceptsNeither()
    {
        using var response = await GetAsync(Chunk, acceptEncoding: null);

        Assert.Empty(response.Content.Headers.ContentEncoding);
        Assert.Equal(Script, await response.Content.ReadAsStringAsync(Token));
    }

    [Fact]
    public async Task Get_ShouldTakeNoForAnAnswer_WhenACodingIsRefusedByName()
    {
        // "br;q=0" is a refusal of Brotli, not a preference for it.
        using var response = await GetAsync(Chunk, "br;q=0, gzip");

        Assert.Equal(["gzip"], response.Content.Headers.ContentEncoding);
    }

    [Fact]
    public async Task Get_ShouldVaryOnAcceptEncoding_WhicheverCopyIsSent()
    {
        using var compressed = await GetAsync(Chunk, "br");
        using var plain = await GetAsync(Chunk, acceptEncoding: null);

        // Both, or a shared proxy keeps the Brotli answer and serves it to a client that cannot read it.
        Assert.Contains("Accept-Encoding", compressed.Headers.Vary);
        Assert.Contains("Accept-Encoding", plain.Headers.Vary);
    }

    [Fact]
    public async Task Get_ShouldKeepAHashedAssetImmutable_WhenSendingItsCopy()
    {
        using var response = await GetAsync(Chunk, "br");

        var caching = response.Headers.CacheControl!;

        Assert.True(caching.Public);
        Assert.Equal(TimeSpan.FromDays(365), caching.MaxAge);
    }

    [Fact]
    public async Task Get_ShouldStillRevalidateTheServiceWorker_WhenSendingItsCopy()
    {
        using var response = await GetAsync("/service-worker.js", "br");

        // Decided from the requested name, not the file on its way out (that once cached a stale service worker for a year).
        Assert.Equal(["br"], response.Content.Headers.ContentEncoding);
        Assert.True(response.Headers.CacheControl!.NoCache);
        Assert.Null(response.Headers.CacheControl.MaxAge);
    }

    [Fact]
    public async Task Get_ShouldLeaveAFileWithNoCopiesAlone()
    {
        using var response = await GetAsync("/robots.txt", "br, gzip");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(response.Content.Headers.ContentEncoding);
        Assert.DoesNotContain("Accept-Encoding", response.Headers.Vary);
    }

    [Fact]
    public async Task Get_ShouldNeverCompressAnApiResponse()
    {
        using var response = await GetAsync("/api/v1/registration/policy", "br, gzip");

        // Nothing the server generates is compressed.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(response.Content.Headers.ContentEncoding);
    }

    public void Dispose()
    {
        factory.Dispose();
        Directory.Delete(root, recursive: true);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<HttpResponseMessage> GetAsync(string path, string? acceptEncoding)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);

        if (acceptEncoding is not null)
        {
            request.Headers.TryAddWithoutValidation("Accept-Encoding", acceptEncoding);
        }

        return await client.SendAsync(request, Token);
    }

    private static async Task<string> DecodeAsync(HttpResponseMessage response, string coding)
    {
        await using var body = await response.Content.ReadAsStreamAsync(Token);
        await using Stream decoded = coding == "br"
            ? new BrotliStream(body, CompressionMode.Decompress)
            : new GZipStream(body, CompressionMode.Decompress);
        using var reader = new StreamReader(decoded, Encoding.UTF8);

        return await reader.ReadToEndAsync(Token);
    }

    /// <summary>A file and the two copies the frontend build would write beside it.</summary>
    private void Write(string path, string contents)
    {
        var file = Path.Combine(root, path.TrimStart('/'));
        var bytes = Encoding.UTF8.GetBytes(contents);

        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllBytes(file, bytes);

        if (path.EndsWith(".txt", StringComparison.Ordinal))
        {
            // A single line does not shrink, so the build writes it no copies.
            return;
        }

        using (var brotli = new BrotliStream(File.Create(file + ".br"), CompressionLevel.SmallestSize))
        {
            brotli.Write(bytes);
        }

        using var gzip = new GZipStream(File.Create(file + ".gz"), CompressionLevel.SmallestSize);
        gzip.Write(bytes);
    }
}
