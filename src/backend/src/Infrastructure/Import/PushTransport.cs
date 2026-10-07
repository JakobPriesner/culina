using System.Security.Cryptography.X509Certificates;
using Domain.Import;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;

namespace Infrastructure.Import;

/// <summary>One pooled, RFC 8291 (aes128gcm) push client.</summary>
internal sealed class PushTransport : IDisposable
{
    private readonly HttpClient http;
    private readonly PushServiceClient client;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The owned HttpClient disposes its handler and is disposed by this singleton.")]
    public PushTransport() : this(new HttpClient(Handler(), disposeHandler: true) { Timeout = TimeSpan.FromSeconds(15) }) { }
    internal PushTransport(HttpClient http)
    {
        this.http = http;
        client = new PushServiceClient(http) { AutoRetryAfter = false, DefaultTimeToLive = 86400 };
    }

    internal async Task SendAsync(string endpoint, string p256dh, string auth, string payload, string publicKey, string privateKey, CancellationToken token)
    {
        using var authentication = new VapidAuthentication(publicKey, privateKey) { Subject = "https://culina.app" };
        var subscription = new PushSubscription { Endpoint = endpoint, Keys = new Dictionary<string, string> { ["p256dh"] = p256dh, ["auth"] = auth } };
        await client.RequestPushMessageDeliveryAsync(subscription, new PushMessage(payload), authentication, VapidAuthenticationScheme.Vapid, token).ConfigureAwait(false);
    }

    public void Dispose() => http.Dispose();

    // Public addresses only, no proxy, no redirects. The endpoint is browser-supplied and the allow list holds
    // names, so where a name resolves is checked here at connect time.
    private static SocketsHttpHandler Handler()
    {
        var handler = CheckedConnections.Handler(admits: PublicAddress.IsPublic);

        handler.SslOptions.CertificateRevocationCheckMode = X509RevocationMode.Online;

        return handler;
    }
}
