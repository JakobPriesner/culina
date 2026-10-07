using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Application.Abstractions.Settings;
using Infrastructure.Import;
using IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Import;

/// <summary>
/// An import, from the request that asks for it to the last event of the stream, against a real
/// database and a real Tandoor-shaped server.
/// </summary>
/// <remarks>
/// The only test of the feature as it is: a request returning before the work, a worker importing
/// several recipes at a time, a stream reporting each. The fake Tandoor sleeps per recipe so "in
/// parallel" is observable, and fails one on purpose.
/// </remarks>
[Collection(RequiresDatabase.Name)]
public class BackgroundImportTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    /// <summary>The recipe the fake refuses, so a failure has somewhere to be reported.</summary>
    private const int Broken = 3;

    private static readonly string[] Four = ["1", "2", "3", "4"];
    private static readonly string[] Two = ["1", "2"];
    private static readonly string[] TheSecond = ["2"];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Import_ShouldBringTheRecipesOver_AndReportEachOneOnTheStream()
    {
        using var tandoor = FakeTandoor.Start(recipes: 4, broken: Broken);
        using var factory = Factory();
        using var client = await SignedInAsync(factory);
        var sourceId = await ConnectAsync(client, tandoor);

        var started = await client.PostAsync(
            $"/api/v1/recipe-sources/{sourceId}/imports",
            new { externalIds = Four },
            Token);

        var importId = started.Json!.Value.GetProperty("importId").GetGuid();
        var events = await WatchAsync(client, sourceId, importId);

        // Accepted, not done: the answer comes back with a shelf and an id long before the recipes.
        Assert.Equal(HttpStatusCode.Accepted, started.StatusCode);
        Assert.Equal(4, started.Json!.Value.GetProperty("total").GetInt32());

        // Four recipes and the event that says it is over.
        Assert.Equal(5, events.Count);
        Assert.True(events[^1].GetProperty("finished").GetBoolean());
        Assert.Equal(4, events[^1].GetProperty("done").GetInt32());

        var outcomes = events
            .Where(one => one.TryGetProperty("recipe", out var recipe) && recipe.ValueKind != JsonValueKind.Null)
            .Select(one => one.GetProperty("recipe"))
            .ToDictionary(
                recipe => recipe.GetProperty("externalId").GetString()!,
                recipe => recipe.GetProperty("outcome").GetString()!);

        Assert.Equal("imported", outcomes["1"]);
        Assert.Equal("imported", outcomes["2"]);
        Assert.Equal("imported", outcomes["4"]);

        // One recipe that could not be read is one line, not the end of the run.
        Assert.Equal("failed", outcomes[Broken.ToString(CultureInfo.InvariantCulture)]);

        // The three that worked are on the shelf the request named: what just happened, and how to
        // undo it.
        var cookbookId = started.Json!.Value.GetProperty("cookbookId").GetGuid();
        var shelf = await client.GetAsync($"/api/v1/cookbooks/{cookbookId}", Token);

        Assert.Equal(3, shelf.Json!.Value.GetProperty("recipeCount").GetInt32());

        // An operator who reads only the log sees how it went.
        var finished = Assert.Single(factory.Logs.Lines, line => line.EventId == 1213);
        Assert.Equal(importId.ToString(), finished["ImportId"]);
        Assert.Equal("3", finished["Imported"]);
        Assert.Equal("4", finished["Total"]);
        Assert.Equal("1", finished["Failed"]);

        // No request carries the run, so its trace is what ties its lines together.
        Assert.NotNull(finished["TraceId"]);
    }

    [Fact]
    public async Task Import_ShouldAccountForEachRecipe_WhenTheLogIsTurnedUpToDebug()
    {
        using var tandoor = FakeTandoor.Start(recipes: 4, broken: Broken);
        using var factory = Factory(logLevel: "Debug");
        using var client = await SignedInAsync(factory);
        var sourceId = await ConnectAsync(client, tandoor);

        var started = await client.PostAsync(
            $"/api/v1/recipe-sources/{sourceId}/imports",
            new { externalIds = Four },
            Token);

        await WatchAsync(client, sourceId, started.Json!.Value.GetProperty("importId").GetGuid());

        // Raising the level is all an operator can do without a debugger, so it must show where
        // each recipe went.
        Assert.Single(factory.Logs.Lines, line => line.EventId == 1214);

        var recipes = factory.Logs.Lines.Where(line => line.EventId == 1215).ToList();

        Assert.Equal(4, recipes.Count);
        Assert.Equal("failed", Assert.Single(recipes, line => line["ExternalId"] == "3")["Outcome"]);
    }

    [Fact]
    public async Task Import_ShouldFetchSeveralRecipesAtOnce_RatherThanOneAfterAnother()
    {
        using var tandoor = FakeTandoor.Start(recipes: 4, broken: null, dwell: TimeSpan.FromMilliseconds(200));
        using var factory = Factory();
        using var client = await SignedInAsync(factory);
        var sourceId = await ConnectAsync(client, tandoor);

        var started = await client.PostAsync(
            $"/api/v1/recipe-sources/{sourceId}/imports",
            new { externalIds = Four },
            Token);

        await WatchAsync(client, sourceId, started.Json!.Value.GetProperty("importId").GetGuid());

        // The reason the import moved to the server: waiting for each of four round trips to
        // another instance before the next is where the ten minutes went.
        Assert.True(
            tandoor.MostAtOnce > 1,
            $"Recipes were fetched one at a time: at most {tandoor.MostAtOnce} was in flight.");
    }

    [Fact]
    public async Task Import_ShouldBeANoOpTheSecondTime_SoAnInterruptedOneCanBeAskedForAgain()
    {
        using var tandoor = FakeTandoor.Start(recipes: 2, broken: null);
        using var factory = Factory();
        using var client = await SignedInAsync(factory);
        var sourceId = await ConnectAsync(client, tandoor);

        var first = await client.PostAsync(
            $"/api/v1/recipe-sources/{sourceId}/imports",
            new { externalIds = Two },
            Token);

        await WatchAsync(client, sourceId, first.Json!.Value.GetProperty("importId").GetGuid());

        var second = await client.PostAsync(
            $"/api/v1/recipe-sources/{sourceId}/imports",
            new { externalIds = Two },
            Token);

        var events = await WatchAsync(
            client, sourceId, second.Json!.Value.GetProperty("importId").GetGuid());

        // Neither an error nor a second copy: this is how somebody catches up on what is new, and
        // how an interrupted import is finished.
        var outcomes = events
            .Where(one => one.GetProperty("recipe").ValueKind != JsonValueKind.Null)
            .Select(one => one.GetProperty("recipe").GetProperty("outcome").GetString())
            .ToList();

        Assert.Equal(["already_here", "already_here"], outcomes.Order());
    }

    /// <summary>
    /// A recipe the household already has, under the same name, is held back
    /// and named — and brought over after all when somebody says so.
    /// </summary>
    [Fact]
    public async Task Import_ShouldHoldBackALookalike_AndBringItOverWhenAskedToAnyway()
    {
        using var tandoor = FakeTandoor.Start(recipes: 2, broken: null);
        using var factory = Factory();
        using var client = await SignedInAsync(factory);
        var sourceId = await ConnectAsync(client, tandoor);
        var householdId = await HouseholdIdAsync(client);

        // Typed in by hand long before anybody connected Tandoor.
        var typed = await client.PostAsync("/api/v1/recipes", new { householdId, title = "Recipe 2" }, Token);
        var mine = typed.Json!.Value.GetProperty("recipeId").GetGuid();

        var first = await client.PostAsync(
            $"/api/v1/recipe-sources/{sourceId}/imports",
            new { externalIds = Two },
            Token);
        var cookbookId = first.Json!.Value.GetProperty("cookbookId").GetGuid();
        var held = Outcomes(await WatchAsync(client, sourceId, first.Json!.Value.GetProperty("importId").GetGuid()));

        var anyway = await client.PostAsync(
            $"/api/v1/recipe-sources/{sourceId}/imports",
            new { externalIds = TheSecond, allowLookalikes = true, cookbookId },
            Token);
        var brought = Outcomes(await WatchAsync(client, sourceId, anyway.Json!.Value.GetProperty("importId").GetGuid()));

        Assert.Equal("imported", held["1"].GetProperty("outcome").GetString());

        // Not written, not dropped: held, with what it looks like.
        Assert.Equal("looks_like", held["2"].GetProperty("outcome").GetString());
        Assert.Equal(mine, held["2"].GetProperty("recipeId").GetGuid());
        Assert.Equal("Recipe 2", held["2"].GetProperty("looksLike").GetProperty("title").GetString());

        Assert.Equal("imported", brought["2"].GetProperty("outcome").GetString());
        Assert.Equal(cookbookId, anyway.Json!.Value.GetProperty("cookbookId").GetGuid());

        var shelf = await client.GetAsync($"/api/v1/cookbooks/{cookbookId}", Token);
        Assert.Equal(2, shelf.Json!.Value.GetProperty("recipeCount").GetInt32());

        var shelves = await client.GetAsync($"/api/v1/cookbooks?householdId={householdId}", Token);
        Assert.Single(shelves.Json!.Value.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Import_ShouldSayNotFound_WhenAskedToLandOnAnotherKitchensShelf()
    {
        using var tandoor = FakeTandoor.Start(recipes: 2, broken: null);
        using var factory = Factory();
        using var client = await SignedInAsync(factory);
        var sourceId = await ConnectAsync(client, tandoor);

        var settings = factory.Services
            .GetRequiredService<Application.Abstractions.Settings.RegistrationSettings>();
        settings.OpenRegistration = true;
        settings.RequireInvitation = false;

        using var stranger = factory.NewApiClient();
        await stranger.PostAsync(
            "/api/v1/users",
            new { email = "grace@example.com", displayName = "Grace", password = Password },
            Token);
        await stranger.PostAsync(
            "/api/v1/sessions",
            new { email = "grace@example.com", password = Password },
            Token);
        var kitchen = await stranger.PostAsync("/api/v1/households", new { name = "Graces Küche" }, Token);
        var theirs = await stranger.PostAsync(
            "/api/v1/cookbooks",
            new { householdId = kitchen.Json!.Value.GetProperty("householdId").GetGuid(), name = "Graces Sonntage" },
            Token);

        var started = await client.PostAsync(
            $"/api/v1/recipe-sources/{sourceId}/imports",
            new { externalIds = Two, cookbookId = theirs.Json!.Value.GetProperty("cookbookId").GetGuid() },
            Token);

        // Never somebody else's shelf, and never a hint that it exists.
        Assert.Equal(HttpStatusCode.NotFound, started.StatusCode);
        Assert.Equal("cookbooks.not_found", started.ProblemCode);
    }

    private static Dictionary<string, JsonElement> Outcomes(List<JsonElement> events) =>
        events
            .Where(one => one.TryGetProperty("recipe", out var recipe) && recipe.ValueKind != JsonValueKind.Null)
            .Select(one => one.GetProperty("recipe"))
            .ToDictionary(recipe => recipe.GetProperty("externalId").GetString()!);

    /// <summary>
    /// Reads the stream to its end: the whole body at once, which works as the stream is finite; a
    /// browser reads event by event.
    /// </summary>
    private static async Task<List<JsonElement>> WatchAsync(
        ApiClient client,
        Guid sourceId,
        Guid importId)
    {
        var streamed = await client.GetAsync(
            $"/api/v1/recipe-sources/{sourceId}/imports/{importId}/events",
            Token);

        Assert.Equal(HttpStatusCode.OK, streamed.StatusCode);
        Assert.Equal("text/event-stream", streamed.ContentHeaders.ContentType?.MediaType);

        return streamed.Body
            .Split('\n')
            .Where(line => line.StartsWith("data:", StringComparison.Ordinal))
            .Select(line => JsonDocument.Parse(line["data:".Length..]).RootElement.Clone())
            .ToList();
    }

    private CulinaApiFactory Factory(string logLevel = "Information") => new(
        postgres,
        new Dictionary<string, string>
        {
            ["RateLimits:SourceRequestsPerHour"] = "10000",
            ["Logging:LogLevel:Default"] = logLevel
        },
        // The fake Tandoor is on loopback, which no setting lets a deployment reach (not even
        // allow-private), so its client is swapped for one that may.
        replace: services => services.AddSingleton(provider =>
            new SourceHttp(provider.GetRequiredService<StorageSettings>(), admits: _ => true)));

    private async Task<ApiClient> SignedInAsync(CulinaApiFactory factory)
    {
        await postgres.ResetAsync(Token);

        var client = factory.NewApiClient();

        await client.PostAsync(
            "/api/v1/users",
            new { email = "ada@example.com", displayName = "Ada", password = Password },
            Token);
        await client.PostAsync(
            "/api/v1/sessions",
            new { email = "ada@example.com", password = Password },
            Token);

        return client;
    }

    private static async Task<Guid> HouseholdIdAsync(ApiClient client)
    {
        var me = await client.GetAsync("/api/v1/users/me", Token);

        return me.Json!.Value.GetProperty("households")[0].GetProperty("householdId").GetGuid();
    }

    private static async Task<Guid> ConnectAsync(ApiClient client, FakeTandoor tandoor)
    {
        var householdId = await HouseholdIdAsync(client);

        var connected = await client.PostAsync(
            "/api/v1/recipe-sources",
            new
            {
                householdId,
                kind = "tandoor",
                address = tandoor.Address,
                token = "a-token"
            },
            Token);

        Assert.Equal(HttpStatusCode.Created, connected.StatusCode);

        return connected.Json!.Value.GetProperty("sourceId").GetGuid();
    }
}

/// <summary>Enough of Tandoor to be read: a list, a detail, and a way to be slow.</summary>
/// <remarks>
/// A real socket, not a stubbed handler: what is proved includes the server opening several
/// connections at once, which a fake at the wrong layer would answer by construction.
/// </remarks>
internal sealed class FakeTandoor : IDisposable
{
    private readonly HttpListener listener = new();
    private readonly CancellationTokenSource stopping = new();
    private readonly Lock gate = new();

    private int recipes;
    private int? broken;
    private TimeSpan dwell;
    private int inFlight;

    /// <summary>The most recipe fetches that were in flight at the same time.</summary>
    public int MostAtOnce { get; private set; }

    public string Address { get; private set; } = string.Empty;

    public static FakeTandoor Start(int recipes, int? broken, TimeSpan dwell = default)
    {
        var port = FreePort();
        var fake = new FakeTandoor
        {
            recipes = recipes,
            broken = broken,
            dwell = dwell,
            Address = $"http://127.0.0.1:{port.ToString(CultureInfo.InvariantCulture)}"
        };

        fake.listener.Prefixes.Add($"{fake.Address}/");
        fake.listener.Start();

        _ = Task.Run(fake.ServeAsync);

        return fake;
    }

    public void Dispose()
    {
        stopping.Cancel();
        listener.Close();
        stopping.Dispose();
    }

    private static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);

        probe.Start();

        var port = ((IPEndPoint)probe.LocalEndpoint).Port;

        probe.Stop();

        return port;
    }

    private async Task ServeAsync()
    {
        while (!stopping.IsCancellationRequested)
        {
            HttpListenerContext context;

            try
            {
                context = await listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (HttpListenerException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            _ = Task.Run(() => AnswerAsync(context));
        }
    }

    private async Task AnswerAsync(HttpListenerContext context)
    {
        var path = context.Request.Url!.AbsolutePath;

        if (path == "/api/recipe/")
        {
            await WriteAsync(context, 200, Page()).ConfigureAwait(false);

            return;
        }

        var id = Detail(path);

        if (id is null)
        {
            await WriteAsync(context, 404, "{}").ConfigureAwait(false);

            return;
        }

        await BusyAsync().ConfigureAwait(false);

        if (dwell > TimeSpan.Zero)
        {
            await Task.Delay(dwell).ConfigureAwait(false);
        }

        Free();

        await (id == broken
            ? WriteAsync(context, 500, "{}")
            : WriteAsync(context, 200, Recipe(id.Value))).ConfigureAwait(false);
    }

    private async Task BusyAsync()
    {
        await Task.Yield();

        lock (gate)
        {
            inFlight += 1;
            MostAtOnce = Math.Max(MostAtOnce, inFlight);
        }
    }

    private void Free()
    {
        lock (gate)
        {
            inFlight -= 1;
        }
    }

    private static int? Detail(string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return parts is ["api", "recipe", var id] && int.TryParse(id, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private string Page()
    {
        var items = Enumerable.Range(1, recipes).Select(id =>
            $$"""{"id":{{id}},"name":"Recipe {{id}}","description":"Theirs","working_time":10}""");

        return $$"""{"count":{{recipes}},"next":null,"results":[{{string.Join(',', items)}}]}""";
    }

    private static string Recipe(int id) =>
        $$"""
        {
          "id": {{id}},
          "name": "Recipe {{id}}",
          "description": "Theirs",
          "working_time": 10,
          "waiting_time": 5,
          "servings": 2,
          "steps": [
            {
              "instruction": "Cook it.",
              "ingredients": [
                {
                  "amount": 2,
                  "unit": { "name": "g" },
                  "food": { "name": "Salt" }
                }
              ]
            }
          ]
        }
        """;

    private static async Task WriteAsync(HttpListenerContext context, int status, string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = bytes.Length;

        await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);

        context.Response.Close();
    }
}
