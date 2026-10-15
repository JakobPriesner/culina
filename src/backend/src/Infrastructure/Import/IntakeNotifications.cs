using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Application.Abstractions;
using Contracts.Recipes.Intake;
using Domain.Shared;
using Infrastructure.Persistence;
using Lib.Net.Http.WebPush;

namespace Infrastructure.Import;

internal sealed class IntakeNotifications(
    DbExecutor db,
    ISecretProtector secrets,
    PushTransport transport,
    RelayTransport relay,
    Application.Abstractions.Settings.PushSettings push) : IIntakeNotifications
{
    public async Task<string> PublicKeyAsync(CancellationToken token) => (await IdentityAsync(token).ConfigureAwait(false)).PublicKey;

    public async Task<Result> RegisterAsync(Guid userId, PushRegistration subscription, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        if (!AllowedEndpoint(subscription.Endpoint) || !ValidKey(subscription.P256dh, 65) || !ValidKey(subscription.Auth, 16))
        {
            return new Error("Push.InvalidSubscription", "The notification subscription is not valid.", ErrorType.Validation);
        }

        var count = await db.ExecuteScalarAsync<int>("select count(*) from web_push_subscriptions where user_id=@UserId", new { UserId = userId }, token).ConfigureAwait(false);
        var existing = await db.ExecuteScalarAsync<bool>("select exists(select 1 from web_push_subscriptions where endpoint=@Endpoint and user_id=@UserId)", new { subscription.Endpoint, UserId = userId }, token).ConfigureAwait(false);
        if (count >= 10 && !existing)
        {
            return new Error("Push.TooManyDevices", "Ten devices are already registered.", ErrorType.Conflict);
        }

        await db.ExecuteAsync("""
            insert into web_push_subscriptions(endpoint,user_id,p256dh,auth,language) values(@Endpoint,@UserId,@P256dh,@Auth,@Language)
            on conflict(endpoint) do update set user_id=excluded.user_id,p256dh=excluded.p256dh,auth=excluded.auth,language=excluded.language
            """, new { subscription.Endpoint, UserId = userId, subscription.P256dh, subscription.Auth, Language = subscription.Language == "de" ? "de" : "en" }, token).ConfigureAwait(false);
        return Result.Success();
    }

    public Task RemoveAsync(Guid userId, string endpoint, CancellationToken token) => db.ExecuteAsync(
        "delete from web_push_subscriptions where endpoint=@Endpoint and user_id=@UserId", new { Endpoint = endpoint, UserId = userId }, token);

    public async Task<Result> RegisterDeviceAsync(Guid userId, Guid deviceId, string relayToken, string? language, CancellationToken token)
    {
        if (!push.RelayOn)
        {
            return new Error("push.relay_off", "This server sends no notifications to the apps.", ErrorType.Conflict);
        }

        if (string.IsNullOrWhiteSpace(relayToken) || relayToken.Length > 200)
        {
            return new Error("push.invalid_device", "That device registration is not valid.", ErrorType.Validation);
        }

        var count = await db.ExecuteScalarAsync<int>("select count(*) from native_push_devices where user_id=@UserId and device_id<>@DeviceId", new { UserId = userId, DeviceId = deviceId }, token).ConfigureAwait(false);
        if (count >= 10)
        {
            return new Error("Push.TooManyDevices", "Ten devices are already registered.", ErrorType.Conflict);
        }

        // The same phone signing in as somebody else takes the row over: its notifications follow who is signed in.
        await db.ExecuteAsync("""
            insert into native_push_devices(device_id,user_id,relay_token,language) values(@DeviceId,@UserId,@RelayToken,@Language)
            on conflict(device_id) do update set user_id=excluded.user_id,relay_token=excluded.relay_token,language=excluded.language
            """, new { DeviceId = deviceId, UserId = userId, RelayToken = relayToken, Language = language == "de" ? "de" : "en" }, token).ConfigureAwait(false);
        return Result.Success();
    }

    public Task RemoveDeviceAsync(Guid userId, Guid deviceId, CancellationToken token) => db.ExecuteAsync(
        "delete from native_push_devices where device_id=@DeviceId and user_id=@UserId", new { DeviceId = deviceId, UserId = userId }, token);

    public async Task DeliverAsync(CancellationToken token)
    {
        // One channel failing must not starve the other: browsers are served even when the device pass throws.
        try
        {
            await DeliverToDevicesAsync(token).ConfigureAwait(false);
        }
        finally
        {
            await DeliverToBrowsersAsync(token).ConfigureAwait(false);
        }
    }

    private async Task DeliverToBrowsersAsync(CancellationToken token)
    {
        var deliveries = await db.QueryAsync<Delivery>("""
            select n.job_id,s.endpoint,s.p256dh,s.auth,s.language,j.purpose,j.material->>'draftKind' as draft_kind,(j.material->>'recipeId')::uuid as revised_recipe_id
            from recipe_intake_notifications n join web_push_subscriptions s on s.endpoint=n.endpoint
            join recipe_intake_jobs j on j.id=n.job_id
            where n.delivered_at is null and n.retry_at<=now() and n.attempts<12 and j.stage='ready' and s.user_id=j.user_id
                and (j.purpose='draft' or exists(select 1 from recipes r where r.id=j.recipe_id))
            order by n.retry_at limit 10
            """, null, token).ConfigureAwait(false);
        if (deliveries.Count == 0)
        {
            return;
        }

        var identity = await IdentityAsync(token).ConfigureAwait(false);
        foreach (var delivery in deliveries)
        {
            // Claim this delivery before sending; a crash is retried after a
            // minute. Stable notification tags replace, rather than duplicate.
            var claimed = await db.ExecuteAsync("update recipe_intake_notifications set attempts=attempts+1,retry_at=now()+interval '1 minute' where job_id=@JobId and endpoint=@Endpoint and retry_at<=now() and delivered_at is null", delivery, token).ConfigureAwait(false);
            if (claimed == 0)
            {
                continue;
            }

            var (title, body) = Words(delivery.Language, delivery.Purpose);
            var payload = JsonSerializer.Serialize(new
            {
                title,
                body,
                url = PathOf(delivery.JobId, delivery.Purpose, delivery.DraftKind, delivery.RevisedRecipeId),
                tag = $"recipe-intake-{delivery.JobId}"
            });
            try
            {
                await transport.SendAsync(delivery.Endpoint, delivery.P256dh, delivery.Auth, payload, identity.PublicKey, identity.PrivateKey, token).ConfigureAwait(false);
                await db.ExecuteAsync("update recipe_intake_notifications set delivered_at=now() where job_id=@JobId and endpoint=@Endpoint", delivery, token).ConfigureAwait(false);
            }
            catch (PushServiceClientException exception) when (exception.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
            {
                await db.ExecuteAsync("delete from web_push_subscriptions where endpoint=@Endpoint", delivery, token).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is PushServiceClientException or HttpRequestException
                || (exception is TaskCanceledException && !token.IsCancellationRequested))
            {
                await db.ExecuteAsync("update recipe_intake_notifications set retry_at=now()+ least(power(2,attempts),60) * interval '1 minute' where job_id=@JobId and endpoint=@Endpoint", delivery, token).ConfigureAwait(false);
            }
        }
    }

    // The installed apps, through the relay: the same claim, retry and give-up rules as Web Push below.
    private async Task DeliverToDevicesAsync(CancellationToken token)
    {
        if (push.RelayUrl is not { } relayUrl)
        {
            return;
        }

        var deliveries = await db.QueryAsync<DeviceDelivery>("""
            select n.job_id,d.device_id,d.relay_token,d.language,j.purpose,j.material->>'draftKind' as draft_kind,(j.material->>'recipeId')::uuid as revised_recipe_id
            from recipe_intake_device_notifications n join native_push_devices d on d.device_id=n.device_id
            join recipe_intake_jobs j on j.id=n.job_id
            where n.delivered_at is null and n.retry_at<=now() and n.attempts<12 and j.stage='ready' and d.user_id=j.user_id
                and (j.purpose='draft' or exists(select 1 from recipes r where r.id=j.recipe_id))
            order by n.retry_at limit 10
            """, null, token).ConfigureAwait(false);

        foreach (var delivery in deliveries)
        {
            var claimed = await db.ExecuteAsync("update recipe_intake_device_notifications set attempts=attempts+1,retry_at=now()+interval '1 minute' where job_id=@JobId and device_id=@DeviceId and retry_at<=now() and delivered_at is null", delivery, token).ConfigureAwait(false);
            if (claimed == 0)
            {
                continue;
            }

            var (title, body) = Words(delivery.Language, delivery.Purpose);
            var outcome = await relay.SendAsync(
                relayUrl,
                new RelayNotification(delivery.RelayToken, title, body, PathOf(delivery.JobId, delivery.Purpose, delivery.DraftKind, delivery.RevisedRecipeId), $"recipe-intake-{delivery.JobId}"),
                token).ConfigureAwait(false);

            switch (outcome)
            {
                case RelayOutcome.Delivered:
                    await db.ExecuteAsync("update recipe_intake_device_notifications set delivered_at=now() where job_id=@JobId and device_id=@DeviceId", delivery, token).ConfigureAwait(false);
                    break;
                case RelayOutcome.Gone:
                    // The phone revoked its token or the store dropped it; its pending rows go with it.
                    await db.ExecuteAsync("delete from native_push_devices where device_id=@DeviceId", delivery, token).ConfigureAwait(false);
                    break;
                default:
                    await db.ExecuteAsync("update recipe_intake_device_notifications set retry_at=now()+ least(power(2,attempts),60) * interval '1 minute' where job_id=@JobId and device_id=@DeviceId", delivery, token).ConfigureAwait(false);
                    break;
            }
        }
    }

    private static (string Title, string Body) Words(string language, string purpose) => (language, purpose) switch
    {
        ("de", "draft") => ("Dein Entwurf ist fertig", "Olli hat ihn zu Ende geschrieben. Schau ihn dir in Ruhe an."),
        (_, "draft") => ("Your draft is ready", "Olli has finished writing it. It is ready for your review."),
        ("de", _) => ("Dein Rezept ist bereit", "Olli hat es gespeichert. Schau es dir in Ruhe an."),
        _ => ("Your recipe is ready", "Olli has saved it. It is ready for your review.")
    };

    /// <summary>
    /// Where a tap goes: an import's page, or the page a draft was asked on, which re-attaches to it
    /// (docs/native-app-design.md, part 8). The service worker opens only these shapes.
    /// </summary>
    internal static string PathOf(Guid jobId, string purpose, string? draftKind, Guid? revisedRecipeId) => purpose switch
    {
        "draft" when draftKind == "revision" && revisedRecipeId is { } recipeId => $"/recipes/{recipeId}/edit",
        "draft" => "/recipes/new?start=idea",
        _ => $"/recipes/imports/{jobId}"
    };

    private async Task<Identity> IdentityAsync(CancellationToken token)
    {
        var identity = await db.QuerySingleOrDefaultAsync<Identity>("select public_key,private_key from web_push_identity where singleton", null, token).ConfigureAwait(false);
        if (identity is null)
        {
            var generated = GenerateKeys();
            await db.ExecuteAsync("insert into web_push_identity(public_key,private_key) values(@PublicKey,@PrivateKey) on conflict do nothing",
                new { generated.PublicKey, PrivateKey = secrets.Protect(generated.PrivateKey) }, token).ConfigureAwait(false);
            identity = await db.QuerySingleOrDefaultAsync<Identity>("select public_key,private_key from web_push_identity where singleton", null, token).ConfigureAwait(false);
        }
        return identity! with { PrivateKey = secrets.Unprotect(identity!.PrivateKey) ?? throw new InvalidOperationException("The Web Push key ring could not be read.") };
    }

    internal static (string PublicKey, string PrivateKey) GenerateKeys()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parts = key.ExportParameters(true);
        byte[] point = [4, .. parts.Q.X!, .. parts.Q.Y!];
        return (Encode(point), Encode(parts.D!));
    }

    private static string Encode(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    internal static bool AllowedEndpoint(string endpoint) => endpoint.Length <= 2048 && Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && uri.Port == 443 && uri.UserInfo.Length == 0
        && (uri.Host == "fcm.googleapis.com" || uri.Host == "updates.push.services.mozilla.com"
            || uri.Host.EndsWith(".push.apple.com", StringComparison.Ordinal));
    private static bool ValidKey(string key, int length)
    {
        try { return Convert.FromBase64String(key.Replace('-', '+').Replace('_', '/').PadRight((key.Length + 3) / 4 * 4, '=')).Length == length; }
        catch (FormatException) { return false; }
    }
    private sealed record Identity(string PublicKey, string PrivateKey);
    private sealed record Delivery(Guid JobId, string Endpoint, string P256dh, string Auth, string Language, string Purpose, string? DraftKind, Guid? RevisedRecipeId);
    private sealed record DeviceDelivery(Guid JobId, Guid DeviceId, string RelayToken, string Language, string Purpose, string? DraftKind, Guid? RevisedRecipeId);
}
