using System.Buffers.Text;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Relay;

/// <summary>What a server asked to show: words for the person, a path to open, and a tag a later one replaces.</summary>
internal sealed record Notification(string Title, string Body, string Path, string? Tag);

/// <summary>How a hand-over to a store went.</summary>
internal enum Delivery
{
    /// <summary>The store took it.</summary>
    Delivered,

    /// <summary>The store no longer knows the device: uninstalled, or the token was replaced.</summary>
    Gone,

    /// <summary>The store could not take it now; the server retries.</summary>
    Failed
}

/// <summary>Hands one notification to a platform's push service.</summary>
internal interface IPushSender
{
    Platform Platform { get; }

    Task<Delivery> SendAsync(string deviceToken, Notification notification, CancellationToken cancellationToken);
}

/// <summary>APNs over HTTP/2 with a provider token: an ES256 JWT signed with the publisher's <c>.p8</c> key.</summary>
internal sealed class ApnsSender(HttpClient http, ApnsOptions options, TimeProvider time) : IPushSender, IDisposable
{
    // Apple refuses a token older than an hour and throttles one renewed more often than every 20 minutes.
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(50);

    private readonly Lock gate = new();
    private readonly ECDsa key = LoadKey(options.PrivateKeyPath);
    private (string Token, DateTimeOffset IssuedAt)? current;

    public Platform Platform => Platform.Ios;

    public async Task<Delivery> SendAsync(string deviceToken, Notification notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var host = options.Sandbox ? "https://api.sandbox.push.apple.com" : "https://api.push.apple.com";
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{host}/3/device/{Uri.EscapeDataString(deviceToken)}")
        {
            Version = HttpVersion.Version20,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact,
            Content = JsonContent.Create(
                new ApnsPayload(
                    new ApnsAps(new ApnsAlert(notification.Title, notification.Body), "default", notification.Tag),
                    notification.Path),
                RelayJson.Default.ApnsPayload)
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("bearer", ProviderToken());
        request.Headers.Add("apns-topic", options.Topic);
        request.Headers.Add("apns-push-type", "alert");
        request.Headers.Add("apns-priority", "10");

        if (notification.Tag is { Length: > 0 } tag)
        {
            request.Headers.Add("apns-collapse-id", tag);
        }

        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            return Delivery.Delivered;
        }

        // 410 Unregistered, or a token APNs says is not a device token at all.
        if (response.StatusCode == HttpStatusCode.Gone)
        {
            return Delivery.Gone;
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var reason = await response.Content.ReadFromJsonAsync(RelayJson.Default.ApnsError, cancellationToken)
                .ConfigureAwait(false);

            return reason?.Reason is "BadDeviceToken" or "DeviceTokenNotForTopic" ? Delivery.Gone : Delivery.Failed;
        }

        return Delivery.Failed;
    }

    /// <summary>The JWT every request carries, renewed before Apple would refuse it.</summary>
    internal string ProviderToken()
    {
        lock (gate)
        {
            var now = time.GetUtcNow();

            if (current is { } kept && now - kept.IssuedAt < TokenLifetime)
            {
                return kept.Token;
            }

            var token = Jwt.Sign(
                $$"""{"alg":"ES256","kid":"{{options.KeyId}}"}""",
                $$"""{"iss":"{{options.TeamId}}","iat":{{now.ToUnixTimeSeconds()}}}""",
                data => key.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));

            current = (token, now);

            return token;
        }
    }

    public void Dispose() => key.Dispose();

    private static ECDsa LoadKey(string path)
    {
        var key = ECDsa.Create();
        key.ImportFromPem(File.ReadAllText(path));

        return key;
    }
}

/// <summary>FCM HTTP v1, with an OAuth access token the service account signs for itself (RS256 JWT bearer grant).</summary>
internal sealed class FcmSender(HttpClient http, FcmOptions options, TimeProvider time) : IPushSender, IDisposable
{
    private const string Scope = "https://www.googleapis.com/auth/firebase.messaging";

    private const string DefaultTokenUri = "https://oauth2.googleapis.com/token";

    private readonly ServiceAccount account = ServiceAccount.Load(options.ServiceAccountPath);
    private readonly RSA key = LoadKey(options.ServiceAccountPath);
    private readonly SemaphoreSlim gate = new(1, 1);
    private (string Token, DateTimeOffset ExpiresAt)? current;

    public Platform Platform => Platform.Android;

    public async Task<Delivery> SendAsync(string deviceToken, Notification notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var accessToken = await AccessTokenAsync(cancellationToken).ConfigureAwait(false);

        if (accessToken is null)
        {
            return Delivery.Failed;
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://fcm.googleapis.com/v1/projects/{Uri.EscapeDataString(options.ProjectId)}/messages:send")
        {
            Content = JsonContent.Create(
                new FcmRequest(new FcmMessage(
                    deviceToken,
                    new FcmNotification(notification.Title, notification.Body),
                    new Dictionary<string, string> { ["path"] = notification.Path },
                    new FcmAndroid(notification.Tag, "high", new FcmAndroidNotification(notification.Tag)))),
                RelayJson.Default.FcmRequest)
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);

        return response.StatusCode switch
        {
            HttpStatusCode.OK => Delivery.Delivered,
            // UNREGISTERED: the app was removed or its token replaced.
            HttpStatusCode.NotFound => Delivery.Gone,
            _ => Delivery.Failed
        };
    }

    private async Task<string?> AccessTokenAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var now = time.GetUtcNow();

            if (current is { } kept && now < kept.ExpiresAt)
            {
                return kept.Token;
            }

            var assertion = Jwt.Sign(
                """{"alg":"RS256","typ":"JWT"}""",
                $$"""{"iss":"{{account.ClientEmail}}","scope":"{{Scope}}","aud":"{{account.TokenUri}}","iat":{{now.ToUnixTimeSeconds()}},"exp":{{now.AddHours(1).ToUnixTimeSeconds()}}}""",
                data => key.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = assertion
            });
            using var response = await http.PostAsync(new Uri(account.TokenUri), form, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var granted = await response.Content.ReadFromJsonAsync(RelayJson.Default.OAuthToken, cancellationToken)
                .ConfigureAwait(false);

            if (granted?.AccessToken is not { Length: > 0 } token)
            {
                return null;
            }

            // Renewed five minutes early, so a request never carries one that expires on the way.
            current = (token, now.AddSeconds(Math.Max(60, granted.ExpiresIn - 300)));

            return token;
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose()
    {
        key.Dispose();
        gate.Dispose();
    }

    private static RSA LoadKey(string path)
    {
        using var file = JsonDocument.Parse(File.ReadAllText(path));
        var key = RSA.Create();
        key.ImportFromPem(file.RootElement.GetProperty("private_key").GetString());

        return key;
    }

    private sealed record ServiceAccount(string ClientEmail, string TokenUri)
    {
        internal static ServiceAccount Load(string path)
        {
            using var file = JsonDocument.Parse(File.ReadAllText(path));
            var root = file.RootElement;

            return new ServiceAccount(
                root.GetProperty("client_email").GetString() ?? string.Empty,
                root.TryGetProperty("token_uri", out var uri) ? uri.GetString() ?? DefaultTokenUri : DefaultTokenUri);
        }
    }
}

/// <summary>A compact JWS: base64url header and claims, signed by the caller's key.</summary>
internal static class Jwt
{
    internal static string Sign(string header, string claims, Func<byte[], byte[]> sign)
    {
        ArgumentNullException.ThrowIfNull(sign);

        var signingInput = $"{Encode(header)}.{Encode(claims)}";

        return $"{signingInput}.{Base64Url.EncodeToString(sign(Encoding.ASCII.GetBytes(signingInput)))}";
    }

    private static string Encode(string json) => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(json));
}

internal sealed record ApnsPayload(
    [property: JsonPropertyName("aps")] ApnsAps Aps,
    [property: JsonPropertyName("path")] string Path);

internal sealed record ApnsAps(
    [property: JsonPropertyName("alert")] ApnsAlert Alert,
    [property: JsonPropertyName("sound")] string Sound,
    [property: JsonPropertyName("thread-id")] string? ThreadId);

internal sealed record ApnsAlert(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("body")] string Body);

internal sealed record ApnsError([property: JsonPropertyName("reason")] string? Reason);

internal sealed record FcmRequest([property: JsonPropertyName("message")] FcmMessage Message);

internal sealed record FcmMessage(
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("notification")] FcmNotification Notification,
    [property: JsonPropertyName("data")] Dictionary<string, string> Data,
    [property: JsonPropertyName("android")] FcmAndroid Android);

internal sealed record FcmNotification(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("body")] string Body);

internal sealed record FcmAndroid(
    [property: JsonPropertyName("collapse_key")] string? CollapseKey,
    [property: JsonPropertyName("priority")] string Priority,
    [property: JsonPropertyName("notification")] FcmAndroidNotification Notification);

internal sealed record FcmAndroidNotification([property: JsonPropertyName("tag")] string? Tag);

internal sealed record OAuthToken(
    [property: JsonPropertyName("access_token")] string? AccessToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn);

/// <summary>Source-generated JSON for every shape the relay reads or writes; nulls are left out.</summary>
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ApnsPayload))]
[JsonSerializable(typeof(ApnsError))]
[JsonSerializable(typeof(FcmRequest))]
[JsonSerializable(typeof(OAuthToken))]
[JsonSerializable(typeof(DeviceRequest))]
[JsonSerializable(typeof(DeviceResponse))]
[JsonSerializable(typeof(NotificationRequest))]
internal sealed partial class RelayJson : JsonSerializerContext;
