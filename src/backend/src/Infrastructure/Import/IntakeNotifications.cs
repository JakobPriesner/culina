using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Application.Abstractions;
using Contracts.Recipes.Intake;
using Domain.Shared;
using Infrastructure.Persistence;
using Lib.Net.Http.WebPush;

namespace Infrastructure.Import;

internal sealed class IntakeNotifications(DbExecutor db, ISecretProtector secrets, PushTransport transport) : IIntakeNotifications
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

    public async Task DeliverAsync(CancellationToken token)
    {
        var deliveries = await db.QueryAsync<Delivery>("""
            select n.job_id,s.endpoint,s.p256dh,s.auth,s.language,j.recipe_id
            from recipe_intake_notifications n join web_push_subscriptions s on s.endpoint=n.endpoint
            join recipe_intake_jobs j on j.id=n.job_id join recipes r on r.id=j.recipe_id
            where n.delivered_at is null and n.retry_at<=now() and n.attempts<12 and j.stage='ready' and s.user_id=j.user_id
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

            var german = delivery.Language == "de";
            var payload = JsonSerializer.Serialize(new
            {
                title = german ? "Dein Rezept ist bereit" : "Your recipe is ready",
                body = german ? "Olli hat es gespeichert. Schau es dir in Ruhe an." : "Olli has saved it. It is ready for your review.",
                url = $"/recipes/imports/{delivery.JobId}",
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
            catch (Exception exception) when (exception is PushServiceClientException or HttpRequestException)
            {
                await db.ExecuteAsync("update recipe_intake_notifications set retry_at=now()+ least(power(2,attempts),60) * interval '1 minute' where job_id=@JobId and endpoint=@Endpoint", delivery, token).ConfigureAwait(false);
            }
        }
    }

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
    private sealed record Delivery(Guid JobId, string Endpoint, string P256dh, string Auth, string Language, Guid RecipeId);
}
