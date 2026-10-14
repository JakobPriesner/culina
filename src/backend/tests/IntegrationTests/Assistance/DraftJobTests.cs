using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Application.Abstractions;
using Infrastructure.Import;
using Infrastructure.Persistence;
using IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Assistance;

/// <summary>
/// The composer's drafts as server jobs (docs/native-app-design.md, part 8): written by the intake worker whatever the
/// page does, ending at the draft rather than a recipe, and announced only to somebody who is not looking.
/// </summary>
[Collection(RequiresDatabase.Name)]
public sealed class DraftJobTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    private static readonly string[] Written =
    [
        "{\"title\":\"Auber",
        "ginenauflauf\",\"description\":\"Warm und einfach\",",
        "\"groups\":[{\"ingredients\":[{\"quantity\":2,\"name\":\"Auberginen\"}]}],",
        "\"steps\":[{\"text\":\"Alles kochen\"}]}"
    ];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AnIdea_ShouldBeWrittenByTheWorker_AndStopAtTheDraft()
    {
        using var provider = new DraftStreamTests.StubProvider(Written);
        using var world = await ConnectedAsync(provider);
        var id = Guid.NewGuid();

        var started = await world.Client.PutAsync($"/api/v1/recipe-drafts/{id}", Idea(world), Token);
        var again = await world.Client.PutAsync($"/api/v1/recipe-drafts/{id}", Idea(world), Token);
        var job = await SettledAsync(world.Client, id);

        Assert.Equal(HttpStatusCode.Accepted, started.StatusCode);
        Assert.Equal("draft", started.Json!.Value.GetProperty("purpose").GetString());
        Assert.Equal("idea", started.Json.Value.GetProperty("draftKind").GetString());
        // The same id is the same job: asking again after a lost answer is not a second bill.
        Assert.Equal(HttpStatusCode.Accepted, again.StatusCode);
        Assert.Equal("ready", job.GetProperty("stage").GetString());
        Assert.Equal("Auberginenauflauf", job.GetProperty("draft").GetProperty("title").GetString());
        Assert.Equal("Alles kochen", job.GetProperty("draft").GetProperty("steps")[0].GetProperty("text").GetString());
        // A draft, never a recipe: the person corrects it and saves it.
        Assert.Equal(JsonValueKind.Null, job.GetProperty("recipeId").ValueKind);
        Assert.Equal(0, await postgres.QuerySingleAsync<long>("select count(*) from recipes", Token));
    }

    [Fact]
    public async Task ADraftNobodyIsLookingAt_ShouldBeAnnounced()
    {
        using var provider = new DraftStreamTests.StubProvider(Written);
        using var world = await ConnectedAsync(provider);
        await SubscribedAsync(world);
        var id = Guid.NewGuid();

        await world.Client.PutAsync($"/api/v1/recipe-drafts/{id}", Idea(world), Token);
        await SettledAsync(world.Client, id);

        Assert.Equal(1, await NotificationsAsync(id));
    }

    [Fact]
    public async Task ADraftAPageIsShowing_ShouldNotBeAnnounced()
    {
        using var provider = new DraftStreamTests.StubProvider(Written, paused: true);
        using var world = await ConnectedAsync(provider);
        await SubscribedAsync(world);
        var id = Guid.NewGuid();

        using var showing = await world.Client.OpenStreamAsync($"/api/v1/recipe-intakes/events?attending={id}", Token);
        await world.Client.PutAsync($"/api/v1/recipe-drafts/{id}", Idea(world), Token);
        provider.Continue();
        await SettledAsync(world.Client, id);

        Assert.Equal(0, await NotificationsAsync(id));
    }

    [Fact]
    public async Task ARevision_ShouldBeWrittenForTheRecipeItNames()
    {
        using var provider = new DraftStreamTests.StubProvider(Written);
        using var world = await ConnectedAsync(provider, capability: "improve");
        var created = await world.Client.PostAsync("/api/v1/recipes", new { householdId = world.HouseholdId, title = "Auflauf" }, Token);
        var recipeId = created.Json!.Value.GetProperty("recipeId").GetGuid();
        var id = Guid.NewGuid();

        var started = await world.Client.PutAsync(
            $"/api/v1/recipe-drafts/{id}",
            new { kind = "revision", householdId = world.HouseholdId, recipeId },
            Token);
        var job = await SettledAsync(world.Client, id);

        Assert.Equal(recipeId, started.Json!.Value.GetProperty("revisedRecipeId").GetGuid());
        Assert.Equal("ready", job.GetProperty("stage").GetString());
        Assert.Equal("Auberginenauflauf", job.GetProperty("draft").GetProperty("title").GetString());
    }

    [Fact]
    public async Task ADraftDismissedWhileBeingWritten_ShouldStop_AndStayDismissed()
    {
        using var provider = new DraftStreamTests.StubProvider(Written, paused: true);
        using var world = await ConnectedAsync(provider);
        var id = Guid.NewGuid();

        await world.Client.PutAsync($"/api/v1/recipe-drafts/{id}", Idea(world), Token);
        await StageAsync(world.Client, id, stage => stage is "reading" or "thinking");
        var dismissed = await world.Client.PostAsync($"/api/v1/recipe-intakes/{id}/reviewed", new { }, Token);
        provider.Continue();
        await Task.Delay(TimeSpan.FromSeconds(2), Token);

        Assert.Equal(HttpStatusCode.NoContent, dismissed.StatusCode);
        Assert.Equal("reviewed", await StageOfAsync(id));
    }

    [Fact]
    public async Task ANewDraft_ShouldDismissTheFinishedOnesBeforeIt()
    {
        using var provider = new DraftStreamTests.StubProvider(Written);
        using var world = await ConnectedAsync(provider);
        var first = Guid.NewGuid();
        await world.Client.PutAsync($"/api/v1/recipe-drafts/{first}", Idea(world), Token);
        await SettledAsync(world.Client, first);

        await world.Client.PutAsync($"/api/v1/recipe-drafts/{Guid.NewGuid()}", Idea(world), Token);

        Assert.Equal("reviewed", await StageOfAsync(first));
    }

    [Fact]
    public async Task AFinishedDraft_ShouldReachTheApps_WithItsOwnWords_AndThePageItWasAskedOn()
    {
        var relay = new ScriptedRelay();
        using var factory = new CulinaApiFactory(
            postgres,
            new Dictionary<string, string> { ["Push:RelayUrl"] = "https://relay.example/" },
            replace: services => services.AddSingleton(new RelayTransport(new HttpClient(relay))));
        await postgres.ResetAsync(Token);
        using var client = factory.NewApiClient();
        await client.PostAsync("/api/v1/users", new { email = "ada@example.com", displayName = "Ada", password = Password }, Token);
        await client.PostAsync("/api/v1/sessions", new { email = "ada@example.com", password = Password }, Token);
        await client.PutAsync($"/api/v1/push/devices/{Guid.NewGuid()}", new { relayToken = "rt-1", language = "de" }, Token);
        var me = (await client.GetAsync("/api/v1/users/me", Token)).Json!.Value;
        var householdId = me.GetProperty("households")[0].GetProperty("householdId").GetGuid();
        var recipeId = (await client.PostAsync("/api/v1/recipes", new { householdId, title = "Auflauf" }, Token)).Json!.Value.GetProperty("recipeId").GetGuid();
        var job = Guid.NewGuid();

        // As the worker leaves a revision it has finished writing; this host runs no worker of its own.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<DbExecutor>().ExecuteAsync(
                "insert into recipe_intake_jobs (id, user_id, household_id, material, stage, purpose) values (@Job, @UserId, @HouseholdId, @Material::jsonb, 'saving', 'draft')",
                new
                {
                    Job = job,
                    UserId = me.GetProperty("userId").GetGuid(),
                    HouseholdId = householdId,
                    Material = JsonSerializer.Serialize(new { text = "", transcript = "", language = "de", photos = Array.Empty<object>(), draftKind = "revision", recipeId })
                },
                Token);
            await scope.ServiceProvider.GetRequiredService<IRecipeIntakeJobs>().DraftedAsync(job, Token);
            await scope.ServiceProvider.GetRequiredService<IIntakeNotifications>().DeliverAsync(Token);
        }

        var (_, sent) = Assert.Single(relay.Received);
        Assert.Equal("Dein Entwurf ist fertig", sent.GetProperty("title").GetString());
        Assert.Equal($"/recipes/{recipeId}/edit", sent.GetProperty("path").GetString());
        Assert.Equal("/recipes/new?start=idea", IntakeNotifications.PathOf(job, "draft", "idea", null));
        Assert.Equal($"/recipes/imports/{job}", IntakeNotifications.PathOf(job, "import", null, null));
    }

    [Fact]
    public async Task ADraft_ShouldBeRefusedBeforeItIsQueued_WhenItCannotBeWritten()
    {
        await postgres.ResetAsync(Token);
        using var client = postgres.Api.NewApiClient();
        await client.PostAsync("/api/v1/users", new { email = "ada@example.com", displayName = "Ada", password = Password }, Token);
        await client.PostAsync("/api/v1/sessions", new { email = "ada@example.com", password = Password }, Token);
        var householdId = (await client.GetAsync("/api/v1/households", Token))
            .Json!.Value.GetProperty("items")[0].GetProperty("householdId").GetGuid();

        var noAssistant = await client.PutAsync(
            $"/api/v1/recipe-drafts/{Guid.NewGuid()}",
            new { kind = "idea", householdId, material = "aubergines", language = "en" },
            Token);
        var nothing = await client.PutAsync(
            $"/api/v1/recipe-drafts/{Guid.NewGuid()}",
            new { kind = "idea", householdId, material = "  ", language = "en" },
            Token);

        Assert.Equal(HttpStatusCode.NotFound, noAssistant.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, nothing.StatusCode);
        Assert.Equal(0, await postgres.QuerySingleAsync<long>("select count(*) from recipe_intake_jobs", Token));
    }

    private static object Idea(World world) =>
        new { kind = "idea", householdId = world.HouseholdId, material = "something with aubergines", language = "de" };

    /// <summary>Signed in to the host that runs the intake worker, with the stub as its assistant.</summary>
    private async Task<World> ConnectedAsync(DraftStreamTests.StubProvider provider, string capability = "draft")
    {
        await postgres.ResetAsync(Token);

        var client = postgres.Api.NewApiClient();
        await client.PostAsync("/api/v1/users", new { email = "ada@example.com", displayName = "Ada", password = Password }, Token);
        await client.PostAsync("/api/v1/sessions", new { email = "ada@example.com", password = Password }, Token);

        var settings = await client.PutAsync(
            "/api/v1/settings/assistance",
            new
            {
                enabled = true,
                connections = new[] { new { provider = "openai", apiKey = "sk-stub", baseUrl = provider.Address } },
                uses = new[] { new { capability, enabled = true, provider = "openai", model = "stub-one" } },
                monthlyBudget = (decimal?)null,
                personalBudget = (decimal?)null,
                prices = Array.Empty<object>()
            },
            Token);

        Assert.Equal(HttpStatusCode.OK, settings.StatusCode);

        var householdId = (await client.GetAsync("/api/v1/households", Token))
            .Json!.Value.GetProperty("items")[0].GetProperty("householdId").GetGuid();

        return new World(client, householdId);
    }

    /// <summary>A browser that wants to hear when something is ready.</summary>
    private static async Task SubscribedAsync(World world)
    {
        var browser = IntakeNotifications.GenerateKeys();
        var subscription = await world.Client.PutAsync(
            "/api/v1/push/subscription",
            new
            {
                endpoint = "https://fcm.googleapis.com/push/drafts",
                p256dh = browser.PublicKey,
                auth = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)),
                language = "en"
            },
            Token);

        Assert.Equal(HttpStatusCode.NoContent, subscription.StatusCode);
    }

    private Task<long> NotificationsAsync(Guid id) =>
        postgres.QuerySingleAsync<long>($"select count(*) from recipe_intake_notifications where job_id='{id}'", Token);

    private Task<string> StageOfAsync(Guid id) =>
        postgres.QuerySingleAsync<string>($"select stage from recipe_intake_jobs where id='{id}'", Token);

    /// <summary>The job once the worker is done with it: ready or failed.</summary>
    private static Task<JsonElement> SettledAsync(ApiClient client, Guid id) =>
        StageAsync(client, id, stage => stage is "ready" or "failed");

    private static async Task<JsonElement> StageAsync(ApiClient client, Guid id, Func<string, bool> reached)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));

        while (true)
        {
            var job = (await client.GetAsync($"/api/v1/recipe-intakes/{id}", deadline.Token)).Json;

            if (job is { } found && reached(found.GetProperty("stage").GetString()!))
            {
                return found;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), deadline.Token);
        }
    }

    private sealed record World(ApiClient Client, Guid HouseholdId) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }

    /// <summary>The publisher's relay, taking every notification and keeping what it was sent.</summary>
    private sealed class ScriptedRelay : HttpMessageHandler
    {
        private readonly Lock gate = new();

        public List<(string Url, JsonElement Body)> Received { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);

            lock (gate)
            {
                Received.Add((request.RequestUri!.ToString(), JsonDocument.Parse(body).RootElement.Clone()));
            }

            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }
    }
}
