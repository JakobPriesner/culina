using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Import;

/// <summary>
/// An import, from the request that asks for it to the last event of the
/// stream, against a real database and a real Tandoor-shaped server.
/// </summary>
/// <remarks>
/// <para>
/// The only test that exercises the thing the feature actually is: a request
/// that returns before the work does, a background worker that brings the
/// recipes over several at a time, and a stream that reports each one. None of
/// that is provable from a handler in isolation — the interesting failures are
/// in the seams between them.
/// </para>
/// <para>
/// The Tandoor on the other end is a few hundred lines of nothing: it answers
/// the two endpoints the reader asks for, sleeps for a moment on each recipe so
/// that "in parallel" is observable, and fails one of them on purpose.
/// </para>
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
        // Arrange
        using var tandoor = FakeTandoor.Start(recipes: 4, broken: Broken);
        using var factory = Factory();
        using var client = await SignedInAsync(factory);
        var sourceId = await ConnectAsync(client, tandoor);

        // Act
        var started = await client.PostAsync(
            $"/api/v1/recipe-sources/{sourceId}/imports",
            new { externalIds = Four },
            Token);

        var importId = started.Json!.Value.GetProperty("importId").GetGuid();
        var events = await WatchAsync(client, sourceId, importId);

        // Assert
        // Accepted, not done: the answer comes back with a shelf and an id long
        // before the recipes do.
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

        // And the three that worked are on the shelf the request named, which
        // is the whole of "what just happened, and how do I undo it".
        var cookbookId = started.Json!.Value.GetProperty("cookbookId").GetGuid();
        var shelf = await client.GetAsync($"/api/v1/cookbooks/{cookbookId}", Token);

        Assert.Equal(3, shelf.Json!.Value.GetProperty("recipeCount").GetInt32());
    }

    [Fact]
    public async Task Import_ShouldFetchSeveralRecipesAtOnce_RatherThanOneAfterAnother()
    {
        // Arrange
        using var tandoor = FakeTandoor.Start(recipes: 4, broken: null, dwell: TimeSpan.FromMilliseconds(200));
        using var factory = Factory();
        using var client = await SignedInAsync(factory);
        var sourceId = await ConnectAsync(client, tandoor);

        // Act
        var started = await client.PostAsync(
            $"/api/v1/recipe-sources/{sourceId}/imports",
            new { externalIds = Four },
            Token);

        await WatchAsync(client, sourceId, started.Json!.Value.GetProperty("importId").GetGuid());

        // Assert
        // The whole reason the import moved to the server: four recipes are
        // four round trips to somebody else's instance, and waiting for each
        // one before starting the next is where the ten minutes went.
        Assert.True(
            tandoor.MostAtOnce > 1,
            $"Recipes were fetched one at a time: at most {tandoor.MostAtOnce} was in flight.");
    }

    [Fact]
    public async Task Import_ShouldBeANoOpTheSecondTime_SoAnInterruptedOneCanBeAskedForAgain()
    {
        // Arrange
        using var tandoor = FakeTandoor.Start(recipes: 2, broken: null);
        using var factory = Factory();
        using var client = await SignedInAsync(factory);
        var sourceId = await ConnectAsync(client, tandoor);

        // Act
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

        // Assert
        // Not an error and not a second copy: this is how somebody catches up
        // on what is new, and how an import that was interrupted is finished.
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
        // Arrange
        using var tandoor = FakeTandoor.Start(recipes: 2, broken: null);
        using var factory = Factory();
        using var client = await SignedInAsync(factory);
        var sourceId = await ConnectAsync(client, tandoor);
        var householdId = await HouseholdIdAsync(client);

        // Typed in by hand long before anybody connected Tandoor.
        var typed = await client.PostAsync("/api/v1/recipes", new { householdId, title = "Recipe 2" }, Token);
        var mine = typed.Json!.Value.GetProperty("recipeId").GetGuid();

        // Act
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

        // Assert
        Assert.Equal("imported", held["1"].GetProperty("outcome").GetString());

        // Not written, not dropped: held, with what it looks like.
        Assert.Equal("looks_like", held["2"].GetProperty("outcome").GetString());
        Assert.Equal(mine, held["2"].GetProperty("recipeId").GetGuid());
        Assert.Equal("Recipe 2", held["2"].GetProperty("looksLike").GetProperty("title").GetString());

        // Then brought over after all, onto the shelf the rest landed on.
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
        // Arrange
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

        // Act
        var started = await client.PostAsync(
            $"/api/v1/recipe-sources/{sourceId}/imports",
            new { externalIds = Two, cookbookId = theirs.Json!.Value.GetProperty("cookbookId").GetGuid() },
            Token);

        // Assert
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
    /// Reads the stream to its end.
    /// </summary>
    /// <remarks>
    /// The whole body at once, which works because the stream is finite: it
    /// ends with the run. A browser reads it event by event, which is the
    /// difference that matters to a person and not to an assertion.
    /// </remarks>
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

    private CulinaApiFactory Factory() => new(
        postgres,
        new Dictionary<string, string>
        {
            // The fake Tandoor is on loopback, which is exactly the case this
            // setting exists for: somebody's own recipe server, next door.
            ["Import:AllowPrivateSourceAddresses"] = "true",
            ["RateLimits:SourceRequestsPerHour"] = "10000"
        });

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

/// <summary>
/// Enough of Tandoor to be read: a list, a detail, and a way to be slow.
/// </summary>
/// <remarks>
/// A real socket rather than a stubbed handler, because what is being proved
/// includes that the server opens several connections at the same time — which
/// a fake at the wrong layer would answer by construction.
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

    /// <summary>Where it is listening.</summary>
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
