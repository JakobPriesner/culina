using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Relay.Tests;

/// <summary>The relay as a server and an app see it: register a device, push to it, and forget it.</summary>
public sealed class RelayEndpointTests : IDisposable
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly string directory = Directory.CreateTempSubdirectory("relay-").FullName;
    private readonly FakeSender apple = new(Platform.Ios);
    private readonly FakeSender google = new(Platform.Android);
    private readonly WebApplicationFactory<Program> factory;

    public RelayEndpointTests()
    {
        var key = Path.Combine(directory, "apns.p8");
        using (var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256))
        {
            File.WriteAllText(key, ec.ExportPkcs8PrivateKeyPem());
        }

        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseSetting("Relay:DatabasePath", DatabasePath);
            host.UseSetting("Relay:PerHour", "5");
            host.UseSetting("Relay:Apns:TeamId", "TEAM123456");
            host.UseSetting("Relay:Apns:KeyId", "KEY1234567");
            host.UseSetting("Relay:Apns:PrivateKeyPath", key);
            host.UseSetting("Relay:Apns:Topic", "app.culina");
            host.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPushSender>();
                services.AddSingleton<IPushSender>(apple);
                services.AddSingleton<IPushSender>(google);
            });
        });
    }

    private string DatabasePath => Path.Combine(directory, "relay.db");

    [Fact]
    public async Task ARegisteredDevice_ShouldBePushedTo_WithTheTextPathAndTag()
    {
        using var client = factory.CreateClient();
        var relayToken = await RegisterAsync(client, "ios", "a1b2c3d4");

        var pushed = await NotifyAsync(client, relayToken);

        Assert.Equal(HttpStatusCode.Accepted, pushed.StatusCode);
        var (deviceToken, notification) = Assert.Single(apple.Sent);
        Assert.Equal("a1b2c3d4", deviceToken);
        Assert.Equal(new Notification("Dein Rezept ist bereit", "Tippe, um es anzusehen.", "/recipes/imports/j1", "import-j1"), notification);
        Assert.Empty(google.Sent);
    }

    [Fact]
    public async Task ARelayToken_ShouldBeLongAndRandom_AndKeptOnlyAsItsHash()
    {
        using var client = factory.CreateClient();

        var first = await RegisterAsync(client, "android", "fcm:token-1");
        var second = await RegisterAsync(client, "android", "fcm:token-1");

        // 32 random bytes, base64url without padding.
        Assert.Equal(43, first.Length);
        Assert.NotEqual(first, second);

        var stored = await File.ReadAllBytesAsync(DatabasePath, Token);
        var wal = File.Exists(DatabasePath + "-wal") ? await File.ReadAllBytesAsync(DatabasePath + "-wal", Token) : [];
        var everything = System.Text.Encoding.ASCII.GetString([.. stored, .. wal]);

        Assert.DoesNotContain(first, everything, StringComparison.Ordinal);
        Assert.Contains("fcm:token-1", everything, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnknownOrRevokedToken_ShouldBeGone()
    {
        using var client = factory.CreateClient();
        var relayToken = await RegisterAsync(client, "ios", "a1b2c3d4");

        var unknown = await NotifyAsync(client, "never-issued");
        var revoked = await client.DeleteAsync(new Uri($"/v1/devices/{relayToken}", UriKind.Relative), Token);
        var afterRevoke = await NotifyAsync(client, relayToken);
        var revokedAgain = await client.DeleteAsync(new Uri($"/v1/devices/{relayToken}", UriKind.Relative), Token);

        Assert.Equal(HttpStatusCode.Gone, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        Assert.Equal(HttpStatusCode.Gone, afterRevoke.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, revokedAgain.StatusCode);
        Assert.Empty(apple.Sent);
    }

    [Fact]
    public async Task ADeviceTheStoreDropped_ShouldBeForgotten_AndTheServerToldItIsGone()
    {
        using var client = factory.CreateClient();
        var relayToken = await RegisterAsync(client, "ios", "a1b2c3d4");
        apple.Next = Delivery.Gone;

        var gone = await NotifyAsync(client, relayToken);
        apple.Next = Delivery.Delivered;
        var again = await NotifyAsync(client, relayToken);

        Assert.Equal(HttpStatusCode.Gone, gone.StatusCode);
        Assert.Equal(HttpStatusCode.Gone, again.StatusCode);
        Assert.Single(apple.Sent);
    }

    [Fact]
    public async Task AStoreThatCannotTakeItNow_ShouldBeABadGateway_SoTheServerRetries()
    {
        using var client = factory.CreateClient();
        var relayToken = await RegisterAsync(client, "android", "fcm:token-1");
        google.Next = Delivery.Failed;

        var failed = await NotifyAsync(client, relayToken);

        Assert.Equal(HttpStatusCode.BadGateway, failed.StatusCode);
    }

    [Fact]
    public async Task ASenderThatThrows_ShouldBeABadGateway_NotAServerError()
    {
        using var client = factory.CreateClient();
        var relayToken = await RegisterAsync(client, "ios", "a1b2c3d4");
        apple.Throw = new HttpRequestException("store unreachable");

        var timedOut = await NotifyAsync(client, relayToken);
        apple.Throw = new TaskCanceledException("timeout");
        var cancelled = await NotifyAsync(client, relayToken);
        apple.Throw = new JsonException("empty body");
        var malformed = await NotifyAsync(client, relayToken);

        Assert.Equal(HttpStatusCode.BadGateway, timedOut.StatusCode);
        Assert.Equal(HttpStatusCode.BadGateway, cancelled.StatusCode);
        Assert.Equal(HttpStatusCode.BadGateway, malformed.StatusCode);
    }

    [Theory]
    [InlineData("has spaces")]
    [InlineData("with/slash")]
    public async Task ARelayTokenThatCouldNeverHaveBeenIssued_ShouldBeRefused_BeforeAnyLimiterIsTouched(string relayToken)
    {
        using var client = factory.CreateClient();
        var tooLong = new string('a', 65);

        var oddShape = await NotifyAsync(client, relayToken);
        var longOne = await NotifyAsync(client, tooLong);

        Assert.Equal(HttpStatusCode.BadRequest, oddShape.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, longOne.StatusCode);
    }

    [Fact]
    public async Task UnknownTokens_ShouldNotUseUpTheTokenLimit_ButTheAddressLimitStillStopsThem()
    {
        using var client = factory.CreateClient();

        List<HttpStatusCode> answers = [];

        // PerHour is 5: the address limit is what ends a run of guesses, one token each.
        for (var attempt = 0; attempt < 6; attempt++)
        {
            answers.Add((await NotifyAsync(client, $"guess{attempt}")).StatusCode);
        }

        Assert.Equal([.. Enumerable.Repeat(HttpStatusCode.Gone, 5), HttpStatusCode.TooManyRequests], answers);
    }

    [Fact]
    public async Task OneToken_ShouldBeLimitedPerHour()
    {
        using var client = factory.CreateClient();
        var relayToken = await RegisterAsync(client, "ios", "a1b2c3d4");

        List<HttpStatusCode> answers = [];

        for (var attempt = 0; attempt < 6; attempt++)
        {
            answers.Add((await NotifyAsync(client, relayToken)).StatusCode);
        }

        Assert.Equal([.. Enumerable.Repeat(HttpStatusCode.Accepted, 5), HttpStatusCode.TooManyRequests], answers);
    }

    [Theory]
    [InlineData("recipes/1")]
    [InlineData("//evil.example/path")]
    [InlineData("")]
    public async Task APathThatIsNotTheAppsOwn_ShouldBeRefused(string path)
    {
        using var client = factory.CreateClient();
        var relayToken = await RegisterAsync(client, "ios", "a1b2c3d4");

        var refused = await client.PostAsJsonAsync(
            "/v1/notifications",
            new { relayToken, title = "Bereit", body = "Ein Rezept", path },
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Empty(apple.Sent);
    }

    [Theory]
    [InlineData("windows", "abc")]
    [InlineData("ios", "")]
    [InlineData("ios", "a b")]
    public async Task ARegistrationWithoutAKnownPlatformAndToken_ShouldBeRefused(string platform, string deviceToken)
    {
        using var client = factory.CreateClient();

        var refused = await client.PostAsJsonAsync("/v1/devices", new { platform, deviceToken }, Token);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
    }

    public void Dispose()
    {
        factory.Dispose();
        SqliteConnection.ClearAllPools();
        Directory.Delete(directory, recursive: true);
    }

    private static async Task<string> RegisterAsync(HttpClient client, string platform, string deviceToken)
    {
        var registered = await client.PostAsJsonAsync("/v1/devices", new { platform, deviceToken }, Token);

        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);

        using var body = JsonDocument.Parse(await registered.Content.ReadAsStringAsync(Token));

        return body.RootElement.GetProperty("relayToken").GetString()!;
    }

    private static Task<HttpResponseMessage> NotifyAsync(HttpClient client, string relayToken) =>
        client.PostAsJsonAsync(
            "/v1/notifications",
            new
            {
                relayToken,
                title = "Dein Rezept ist bereit",
                body = "Tippe, um es anzusehen.",
                path = "/recipes/imports/j1",
                tag = "import-j1"
            },
            Token);

    private sealed class FakeSender(Platform platform) : IPushSender
    {
        public List<(string DeviceToken, Notification Notification)> Sent { get; } = [];

        public Delivery Next { get; set; } = Delivery.Delivered;

        public Exception? Throw { get; set; }

        public Platform Platform => platform;

        public Task<Delivery> SendAsync(string deviceToken, Notification notification, CancellationToken cancellationToken)
        {
            if (Throw is not null)
            {
                throw Throw;
            }

            Sent.Add((deviceToken, notification));

            return Task.FromResult(Next);
        }
    }
}
