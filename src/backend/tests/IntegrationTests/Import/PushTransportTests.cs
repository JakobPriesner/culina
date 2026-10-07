using System.Net;
using System.Security.Cryptography;
using Infrastructure.Import;

namespace IntegrationTests.Import;

public class PushTransportTests
{
    [Fact]
    public async Task Push_ShouldEncryptWithAes128Gcm_AndAuthenticateWithoutSendingPlainRecipeText()
    {
        using var handler = new CapturingHandler();
        using var http = new HttpClient(handler);
        using var transport = new PushTransport(http);
        var server = IntakeNotifications.GenerateKeys();
        var browser = IntakeNotifications.GenerateKeys();
        var auth = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        await transport.SendAsync("https://fcm.googleapis.com/push/test", browser.PublicKey, auth, "recipe ready for review", server.PublicKey, server.PrivateKey, TestContext.Current.CancellationToken);
        Assert.Equal("aes128gcm", handler.Encoding);
        Assert.Equal("vapid", handler.AuthenticationScheme);
        Assert.NotEmpty(handler.Encrypted);
        Assert.DoesNotContain("recipe ready", System.Text.Encoding.UTF8.GetString(handler.Encrypted), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://fcm.googleapis.com/fcm/send/test", true)]
    [InlineData("https://updates.push.services.mozilla.com/wpush/v2/test", true)]
    [InlineData("https://web.push.apple.com/test", true)]
    [InlineData("http://fcm.googleapis.com/test", false)]
    [InlineData("https://127.0.0.1/test", false)]
    [InlineData("https://fcm.googleapis.com.evil.test/test", false)]
    [InlineData("https://user@fcm.googleapis.com/test", false)]
    [InlineData("https://fcm.googleapis.com:8443/test", false)]
    public void Push_ShouldAcceptOnlySupportedSecureServices(string endpoint, bool allowed) =>
        Assert.Equal(allowed, IntakeNotifications.AllowedEndpoint(endpoint));

    [Fact]
    public async Task Push_ShouldNotConnect_ToAnAddressInsideTheNetwork()
    {
        // Arrange
        // The real transport, aimed at a listener on loopback: the allow list
        // checks a push service's name, and this is what checks where it
        // resolves to.
        using var server = new LoopbackServer("{}");
        using var transport = new PushTransport();
        var keys = IntakeNotifications.GenerateKeys();
        var browser = IntakeNotifications.GenerateKeys();
        var auth = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        // Act
        var failure = await Record.ExceptionAsync(() => transport.SendAsync(
            $"https://127.0.0.1:{server.Port}/push", browser.PublicKey, auth, "ready", keys.PublicKey, keys.PrivateKey,
            TestContext.Current.CancellationToken));

        // Assert
        Assert.IsType<HttpRequestException>(failure);
        Assert.Equal(0, server.Requests);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        internal string? Encoding { get; private set; }
        internal string? AuthenticationScheme { get; private set; }
        internal byte[] Encrypted { get; private set; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Encoding = string.Join(",", request.Content!.Headers.ContentEncoding);
            AuthenticationScheme = request.Headers.Authorization?.Scheme;
            Encrypted = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.Created);
        }
    }
}
