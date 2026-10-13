using System.Diagnostics.Metrics;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;

namespace Relay;

/// <summary>An app registering its store token.</summary>
internal sealed record DeviceRequest(string? Platform, string? DeviceToken);

/// <summary>The relay token the app hands to its own server.</summary>
internal sealed record DeviceResponse(string RelayToken);

/// <summary>A server asking for one notification on a device it holds the relay token of.</summary>
internal sealed record NotificationRequest(string? RelayToken, string? Title, string? Body, string? Path, string? Tag);

/// <summary>The relay's three endpoints (docs/native-app-design.md, 7.1 and 7.5).</summary>
internal static partial class RelayEndpoints
{
    // Longer than any text Culina composes, short enough that the relay cannot be used to carry anything else.
    private const int MaxTitle = 120;
    private const int MaxBody = 400;
    private const int MaxPath = 200;
    private const int MaxTag = 64;
    private const int MaxDeviceToken = 4096;

    private static readonly Meter Meter = new("Culina.Relay");

    // Counters only: what happened, never to whom or what it said.
    private static readonly Counter<long> Notifications = Meter.CreateCounter<long>("relay.notifications");
    private static readonly Counter<long> Registrations = Meter.CreateCounter<long>("relay.registrations");

    internal static void MapRelay(this IEndpointRouteBuilder app)
    {
        app.MapGet("/healthz", () => Results.Ok());

        app.MapPost("/v1/devices", RegisterAsync);
        app.MapDelete("/v1/devices/{relayToken}", RevokeAsync);
        app.MapPost("/v1/notifications", NotifyAsync);
    }

    private static async Task<IResult> RegisterAsync(
        DeviceRequest request,
        HttpContext context,
        DeviceStore store,
        Limits limits,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (!limits.Admit($"register:{context.Connection.RemoteIpAddress}"))
        {
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);
        }

        var platform = request.Platform switch
        {
            "ios" => Platform.Ios,
            "android" => Platform.Android,
            _ => (Platform?)null
        };

        if (platform is null || request.DeviceToken is not { Length: > 0 and <= MaxDeviceToken } deviceToken
            || !TokenShape().IsMatch(deviceToken))
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "A platform (ios or android) and the store's device token are needed.");
        }

        var relayToken = await store.AddAsync(new Device(platform.Value, deviceToken), time.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);

        Registrations.Add(1, new KeyValuePair<string, object?>("platform", DeviceStore.Name(platform.Value)));

        return Results.Json(new DeviceResponse(relayToken), RelayJson.Default.DeviceResponse, statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> RevokeAsync(string relayToken, DeviceStore store, CancellationToken cancellationToken)
    {
        await store.RemoveAsync(relayToken, cancellationToken).ConfigureAwait(false);

        return Results.NoContent();
    }

    private static async Task<IResult> NotifyAsync(
        NotificationRequest request,
        HttpContext context,
        DeviceStore store,
        Limits limits,
        IEnumerable<IPushSender> senders,
        CancellationToken cancellationToken)
    {
        if (request.RelayToken is not { Length: > 0 } relayToken
            || request.Title is not { Length: > 0 and <= MaxTitle } title
            || request.Body is not { Length: > 0 and <= MaxBody } body
            || request.Path is not { Length: > 0 and <= MaxPath } path || path[0] != '/' || path.StartsWith("//", StringComparison.Ordinal)
            || request.Tag is { Length: > MaxTag })
        {
            Count("refused");

            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "A relay token, a title, a body and a path starting with / are needed.");
        }

        // Each token and each sending address is limited, so a leaked token can only annoy, never flood.
        if (!limits.Admit($"token:{relayToken}") || !limits.Admit($"notify:{context.Connection.RemoteIpAddress}"))
        {
            Count("rate_limited");

            return Results.StatusCode(StatusCodes.Status429TooManyRequests);
        }

        var device = await store.FindAsync(relayToken, cancellationToken).ConfigureAwait(false);

        if (device is null)
        {
            Count("unknown");

            return Results.StatusCode(StatusCodes.Status410Gone);
        }

        var sender = senders.FirstOrDefault(one => one.Platform == device.Platform);

        if (sender is null)
        {
            Count("unsupported");

            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var delivery = await sender.SendAsync(device.DeviceToken, new Notification(title, body, path, request.Tag), cancellationToken)
            .ConfigureAwait(false);

        switch (delivery)
        {
            case Delivery.Delivered:
                Count("delivered");

                return Results.Accepted();
            case Delivery.Gone:
                // The store dropped the device: forget it, and tell the server to forget it too.
                await store.RemoveAsync(relayToken, cancellationToken).ConfigureAwait(false);
                Count("gone");

                return Results.StatusCode(StatusCodes.Status410Gone);
            default:
                Count("failed");

                return Results.StatusCode(StatusCodes.Status502BadGateway);
        }
    }

    private static void Count(string outcome) =>
        Notifications.Add(1, new KeyValuePair<string, object?>("outcome", outcome));

    // APNs tokens are hex, FCM tokens base64url-ish with colons: nothing else belongs in a URL path or a JSON string.
    [GeneratedRegex("^[A-Za-z0-9_:\\-]+$")]
    private static partial Regex TokenShape();
}

/// <summary>A fixed hourly window per key (relay token or address), in memory: a restart forgets, which errs towards delivering.</summary>
internal sealed class Limits(RelayOptions options) : IDisposable
{
    private readonly PartitionedRateLimiter<string> limiter = PartitionedRateLimiter.Create<string, string>(key =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = options.PerHour,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0
        }));

    public bool Admit(string key)
    {
        using var lease = limiter.AttemptAcquire(key);

        return lease.IsAcquired;
    }

    public void Dispose() => limiter.Dispose();
}
