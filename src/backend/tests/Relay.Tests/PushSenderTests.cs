using System.Buffers.Text;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Relay.Tests;

/// <summary>What the relay sends to Apple and Google, byte for byte where it matters: the stores reject anything else.</summary>
public sealed class PushSenderTests : IDisposable
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly Notification Ready = new("Dein Rezept ist bereit", "Tippe, um es anzusehen.", "/recipes/imports/j1", "import-j1");

    private readonly string directory = Directory.CreateTempSubdirectory("relay-keys-").FullName;
    private readonly ECDsa apple = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly RSA google = RSA.Create(2048);
    private readonly Clock clock = new(DateTimeOffset.Parse("2026-10-14T08:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

    [Fact]
    public async Task Apns_ShouldPostTheAlertToTheDevice_WithASignedProviderToken()
    {
        var store = new Store(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var sender = Apns(store, sandbox: false);

        var delivery = await sender.SendAsync("a1b2c3", Ready, Token);

        Assert.Equal(Delivery.Delivered, delivery);
        var (request, body) = Assert.Single(store.Received);
        Assert.Equal("https://api.push.apple.com/3/device/a1b2c3", request.RequestUri!.ToString());
        Assert.Equal(HttpVersion.Version20, request.Version);
        Assert.Equal("app.culina", Header(request, "apns-topic"));
        Assert.Equal("alert", Header(request, "apns-push-type"));
        Assert.Equal("import-j1", Header(request, "apns-collapse-id"));

        using var payload = JsonDocument.Parse(body);
        var aps = payload.RootElement.GetProperty("aps");
        Assert.Equal("Dein Rezept ist bereit", aps.GetProperty("alert").GetProperty("title").GetString());
        Assert.Equal("Tippe, um es anzusehen.", aps.GetProperty("alert").GetProperty("body").GetString());
        Assert.Equal("/recipes/imports/j1", payload.RootElement.GetProperty("path").GetString());

        var jwt = request.Headers.Authorization!;
        Assert.Equal("bearer", jwt.Scheme);
        var (header, claims) = Verify(jwt.Parameter!, data => apple.VerifyData(
            data.Content, data.Signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        Assert.Equal("ES256", header.GetProperty("alg").GetString());
        Assert.Equal("KEY1234567", header.GetProperty("kid").GetString());
        Assert.Equal("TEAM123456", claims.GetProperty("iss").GetString());
        Assert.Equal(clock.GetUtcNow().ToUnixTimeSeconds(), claims.GetProperty("iat").GetInt64());
    }

    [Fact]
    public async Task Apns_ShouldReuseItsTokenForFiftyMinutes_ThenSignANewOne()
    {
        var store = new Store(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var sender = Apns(store, sandbox: true);

        await sender.SendAsync("a1", Ready, Token);
        clock.Advance(TimeSpan.FromMinutes(49));
        await sender.SendAsync("a1", Ready, Token);
        clock.Advance(TimeSpan.FromMinutes(2));
        await sender.SendAsync("a1", Ready, Token);

        var tokens = store.Received.Select(one => one.Request.Headers.Authorization!.Parameter).ToList();
        Assert.Equal(tokens[0], tokens[1]);
        Assert.NotEqual(tokens[1], tokens[2]);
        Assert.StartsWith("https://api.sandbox.push.apple.com/", store.Received[0].Request.RequestUri!.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.Gone, "", "Gone")]
    [InlineData(HttpStatusCode.BadRequest, """{"reason":"BadDeviceToken"}""", "Gone")]
    [InlineData(HttpStatusCode.BadRequest, """{"reason":"PayloadTooLarge"}""", "Failed")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "", "Failed")]
    public async Task Apns_ShouldSayWhetherTheDeviceIsGone(HttpStatusCode status, string reason, string expected)
    {
        var store = new Store(_ => new HttpResponseMessage(status) { Content = new StringContent(reason, Encoding.UTF8, "application/json") });
        using var sender = Apns(store, sandbox: false);

        Assert.Equal(Enum.Parse<Delivery>(expected), await sender.SendAsync("a1", Ready, Token));
    }

    [Fact]
    public async Task Fcm_ShouldAuthoriseAsTheServiceAccount_ThenSendTheMessage()
    {
        var store = new Store(request => request.RequestUri!.Host == "oauth2.googleapis.com"
            ? Json("""{"access_token":"ya29.token","expires_in":3600}""")
            : new HttpResponseMessage(HttpStatusCode.OK));
        using var sender = Fcm(store);

        var delivery = await sender.SendAsync("fcm:token-1", Ready, Token);
        await sender.SendAsync("fcm:token-1", Ready, Token);

        Assert.Equal(Delivery.Delivered, delivery);
        // One grant, reused for the second message.
        Assert.Equal(3, store.Received.Count);

        var (grant, form) = store.Received[0];
        Assert.Equal("https://oauth2.googleapis.com/token", grant.RequestUri!.ToString());
        var fields = form.Split('&').Select(pair => pair.Split('=')).ToDictionary(pair => pair[0], pair => Uri.UnescapeDataString(pair[1]));
        Assert.Equal("urn:ietf:params:oauth:grant-type:jwt-bearer", fields["grant_type"]);
        var (header, claims) = Verify(fields["assertion"], data => google.VerifyData(
            data.Content, data.Signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        Assert.Equal("RS256", header.GetProperty("alg").GetString());
        Assert.Equal("relay@culina-app.iam.gserviceaccount.com", claims.GetProperty("iss").GetString());
        Assert.Equal("https://www.googleapis.com/auth/firebase.messaging", claims.GetProperty("scope").GetString());

        var (send, body) = store.Received[1];
        Assert.Equal("https://fcm.googleapis.com/v1/projects/culina-app/messages:send", send.RequestUri!.ToString());
        Assert.Equal("ya29.token", send.Headers.Authorization!.Parameter);
        using var message = JsonDocument.Parse(body);
        var root = message.RootElement.GetProperty("message");
        Assert.Equal("fcm:token-1", root.GetProperty("token").GetString());
        Assert.Equal("Dein Rezept ist bereit", root.GetProperty("notification").GetProperty("title").GetString());
        Assert.Equal("/recipes/imports/j1", root.GetProperty("data").GetProperty("path").GetString());
        Assert.Equal("import-j1", root.GetProperty("android").GetProperty("collapse_key").GetString());
    }

    [Fact]
    public async Task Fcm_ShouldSayAnUnregisteredTokenIsGone_AndAFailedGrantFailed()
    {
        var gone = new Store(request => request.RequestUri!.Host == "oauth2.googleapis.com"
            ? Json("""{"access_token":"ya29.token","expires_in":3600}""")
            : new HttpResponseMessage(HttpStatusCode.NotFound));
        var refused = new Store(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var first = Fcm(gone);
        using var second = Fcm(refused);

        Assert.Equal(Delivery.Gone, await first.SendAsync("fcm:token-1", Ready, Token));
        Assert.Equal(Delivery.Failed, await second.SendAsync("fcm:token-1", Ready, Token));
    }

    public void Dispose()
    {
        apple.Dispose();
        google.Dispose();
        Directory.Delete(directory, recursive: true);
    }

    private ApnsSender Apns(Store store, bool sandbox)
    {
        var path = Path.Combine(directory, "apns.p8");
        File.WriteAllText(path, apple.ExportPkcs8PrivateKeyPem());

        return new ApnsSender(
            new HttpClient(store),
            new ApnsOptions { TeamId = "TEAM123456", KeyId = "KEY1234567", PrivateKeyPath = path, Topic = "app.culina", Sandbox = sandbox },
            clock);
    }

    private FcmSender Fcm(Store store)
    {
        var path = Path.Combine(directory, "service-account.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["client_email"] = "relay@culina-app.iam.gserviceaccount.com",
            ["private_key"] = google.ExportPkcs8PrivateKeyPem(),
            ["token_uri"] = "https://oauth2.googleapis.com/token"
        }));

        return new FcmSender(new HttpClient(store), new FcmOptions { ProjectId = "culina-app", ServiceAccountPath = path }, clock);
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static string? Header(HttpRequestMessage request, string name) =>
        request.Headers.TryGetValues(name, out var values) ? values.Single() : null;

    /// <summary>Checks a JWT's signature with the given key and returns its header and claims.</summary>
    private static (JsonElement Header, JsonElement Claims) Verify(string jwt, Func<(byte[] Content, byte[] Signature), bool> check)
    {
        var parts = jwt.Split('.');

        Assert.Equal(3, parts.Length);
        Assert.True(check((Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), Base64Url.DecodeFromChars(parts[2]))));

        return (
            JsonDocument.Parse(Base64Url.DecodeFromChars(parts[0])).RootElement.Clone(),
            JsonDocument.Parse(Base64Url.DecodeFromChars(parts[1])).RootElement.Clone());
    }

    /// <summary>Apple's or Google's end: records each request and its body, and answers as told.</summary>
    private sealed class Store(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Received { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Received.Add((request, body));

            return answer(request);
        }
    }

    /// <summary>A clock the test moves.</summary>
    private sealed class Clock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset now = start;

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan by) => now += by;
    }
}
