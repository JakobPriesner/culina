using System.Net;
using System.Text.Json;
using Application.Abstractions;
using Infrastructure.Import;
using Infrastructure.Persistence;
using IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Import;

/// <summary>"Your recipe is ready" for the installed apps, through the publisher's push relay (docs/native-app-design.md, 7.3).</summary>
[Collection(RequiresDatabase.Name)]
public sealed class RelayDeliveryTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AReadyImport_ShouldReachEveryDeviceOfItsOwner_ThroughTheRelay_Once()
    {
        var relay = new ScriptedRelay(HttpStatusCode.Accepted);
        using var factory = WithRelay(relay);
        using var client = await SignedInAsync(factory);
        var device = Guid.NewGuid();

        var available = await client.GetAsync("/api/v1/push/relay", Token);
        var registered = await client.PutAsync($"/api/v1/push/devices/{device}", new { relayToken = "rt-1", language = "de" }, Token);
        var job = await ReadyImportAsync(factory, client);
        await DeliverAsync(factory);
        await DeliverAsync(factory);

        Assert.True(available.Json!.Value.GetProperty("available").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, registered.StatusCode);
        var (url, sent) = Assert.Single(relay.Received);
        Assert.Equal("https://relay.example/v1/notifications", url);
        Assert.Equal("rt-1", sent.GetProperty("relayToken").GetString());
        Assert.Equal("Dein Rezept ist bereit", sent.GetProperty("title").GetString());
        Assert.Equal($"/recipes/imports/{job}", sent.GetProperty("path").GetString());
        Assert.Equal($"recipe-intake-{job}", sent.GetProperty("tag").GetString());
        // Words, a path and a tag: no recipe title, no name, no server address.
        Assert.Equal(["body", "path", "relayToken", "tag", "title"], sent.EnumerateObject().Select(one => one.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task ADeviceTheRelaySaysIsGone_ShouldBeForgotten()
    {
        var relay = new ScriptedRelay(HttpStatusCode.Gone);
        using var factory = WithRelay(relay);
        using var client = await SignedInAsync(factory);
        await client.PutAsync($"/api/v1/push/devices/{Guid.NewGuid()}", new { relayToken = "rt-1" }, Token);

        await ReadyImportAsync(factory, client);
        await DeliverAsync(factory);

        Assert.Single(relay.Received);
        Assert.Equal(0, await CountAsync(factory, "select count(*) from native_push_devices"));
    }

    [Fact]
    public async Task ARelayThatCannotTakeItNow_ShouldBeAskedAgainLater_NotAtOnce()
    {
        var relay = new ScriptedRelay(HttpStatusCode.ServiceUnavailable);
        using var factory = WithRelay(relay);
        using var client = await SignedInAsync(factory);
        await client.PutAsync($"/api/v1/push/devices/{Guid.NewGuid()}", new { relayToken = "rt-1" }, Token);

        await ReadyImportAsync(factory, client);
        await DeliverAsync(factory);
        await DeliverAsync(factory);

        Assert.Single(relay.Received);
        Assert.Equal(1, await CountAsync(factory, "select count(*) from recipe_intake_device_notifications where delivered_at is null and retry_at > now()"));
        Assert.Equal(1, await CountAsync(factory, "select count(*) from native_push_devices"));
    }

    [Fact]
    public async Task Devices_ShouldBeRefused_WhenNoRelayIsNamed()
    {
        await postgres.ResetAsync(Token);
        using var client = await SignedInAsync(postgres.Api);

        var available = await client.GetAsync("/api/v1/push/relay", Token);
        var refused = await client.PutAsync($"/api/v1/push/devices/{Guid.NewGuid()}", new { relayToken = "rt-1" }, Token);

        Assert.False(available.Json!.Value.GetProperty("available").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("push.relay_off", refused.ProblemCode);
    }

    [Fact]
    public async Task Devices_ShouldBeTenAPerson_AndTheSamePhoneReplaceItsRow()
    {
        using var factory = WithRelay(new ScriptedRelay(HttpStatusCode.Accepted));
        using var client = await SignedInAsync(factory);
        var phones = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToList();

        foreach (var phone in phones)
        {
            Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync($"/api/v1/push/devices/{phone}", new { relayToken = $"rt-{phone}" }, Token)).StatusCode);
        }

        var again = await client.PutAsync($"/api/v1/push/devices/{phones[0]}", new { relayToken = "rt-new" }, Token);
        var eleventh = await client.PutAsync($"/api/v1/push/devices/{Guid.NewGuid()}", new { relayToken = "rt-11" }, Token);
        var forgotten = await client.DeleteAsync($"/api/v1/push/devices/{phones[1]}", Token);
        var nowFits = await client.PutAsync($"/api/v1/push/devices/{Guid.NewGuid()}", new { relayToken = "rt-12" }, Token);

        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, eleventh.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, forgotten.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, nowFits.StatusCode);
        Assert.Equal(10, await CountAsync(factory, "select count(*) from native_push_devices"));
    }

    private CulinaApiFactory WithRelay(ScriptedRelay relay) => new(
        postgres,
        new Dictionary<string, string> { ["Push:RelayUrl"] = "https://relay.example/" },
        replace: services => services.AddSingleton(new RelayTransport(new HttpClient(relay))));

    private async Task<ApiClient> SignedInAsync(CulinaApiFactory factory)
    {
        await postgres.ResetAsync(Token);

        var client = factory.NewApiClient();
        await client.PostAsync("/api/v1/users", new { email = "ada@example.com", displayName = "Ada", password = Password }, Token);
        await client.PostAsync("/api/v1/sessions", new { email = "ada@example.com", password = Password }, Token);

        return client;
    }

    /// <summary>An import the worker has just saved: a recipe, and its job moved from saving to ready.</summary>
    private static async Task<Guid> ReadyImportAsync(CulinaApiFactory factory, ApiClient client)
    {
        var me = await client.GetAsync("/api/v1/users/me", Token);
        var householdId = me.Json!.Value.GetProperty("households")[0].GetProperty("householdId").GetGuid();
        var userId = me.Json.Value.GetProperty("userId").GetGuid();
        var created = await client.PostAsync("/api/v1/recipes", new { householdId, title = "Auberginenauflauf" }, Token);
        var recipeId = created.Json!.Value.GetProperty("recipeId").GetGuid();
        var job = Guid.NewGuid();

        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DbExecutor>().ExecuteAsync(
            "insert into recipe_intake_jobs (id, user_id, household_id, material, stage) values (@Job, @UserId, @HouseholdId, '{}'::jsonb, 'saving')",
            new { Job = job, UserId = userId, HouseholdId = householdId },
            Token);
        await scope.ServiceProvider.GetRequiredService<IRecipeIntakeJobs>().CompleteAsync(job, recipeId, Token);

        return job;
    }

    private static async Task DeliverAsync(CulinaApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<IIntakeNotifications>().DeliverAsync(Token);
    }

    private static async Task<int> CountAsync(CulinaApiFactory factory, string sql)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<DbExecutor>().ExecuteScalarAsync<int>(sql, null, Token);
    }

    /// <summary>The publisher's relay, answering every notification the same way and keeping what it was sent.</summary>
    private sealed class ScriptedRelay(HttpStatusCode answer) : HttpMessageHandler
    {
        public List<(string Url, JsonElement Body)> Received { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Received.Add((request.RequestUri!.ToString(), JsonDocument.Parse(body).RootElement.Clone()));

            return new HttpResponseMessage(answer);
        }
    }
}
