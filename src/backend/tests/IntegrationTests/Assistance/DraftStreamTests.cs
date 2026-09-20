using System.Net;
using System.Text;
using System.Text.Json;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Assistance;

/// <summary>
/// A recipe arriving a field at a time, over the wire, from a provider.
/// </summary>
/// <remarks>
/// <para>
/// The one test that exercises the whole chain: the endpoint, the budget
/// checks, the adapter, the lenient reading of JSON that has not finished
/// arriving, and the framing on the way out. Each of those has a test of its
/// own; none of them proves that a recipe typed by a model reaches a browser as
/// several drafts, and that is the feature.
/// </para>
/// <para>
/// The provider is a stub on localhost answering in OpenAI's streaming shape,
/// which is exactly what "anything that answers in their shape" means — the
/// adapter is pointed at it by an administrator setting a base address, the
/// same way somebody points this at a gateway of their own.
/// </para>
/// </remarks>
[Collection(RequiresDatabase.Name)]
public class DraftStreamTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    /// <summary>
    /// The recipe, cut where a model would pause for breath.
    /// </summary>
    /// <remarks>
    /// Deliberately cut mid-value in places — <c>"Auber</c> and <c>"Alles ko</c>
    /// — because a provider does not send whole fields and the reading has to
    /// survive halves.
    /// </remarks>
    private static readonly string[] Written =
    [
        "{\"title\":\"Auber",
        "ginenauflauf\",\"description\":\"Warm und einfach\",",
        "\"groups\":[{\"ingredients\":[{\"quantity\":2,\"name\":\"Auber",
        "ginen\"},{\"quantity\":200,\"unit\":\"g\",\"name\":\"Feta\"}]}],",
        "\"steps\":[{\"text\":\"Alles ko",
        "chen\"}]}"
    ];

    [Fact]
    public async Task Draft_ShouldArriveInPieces_EachOneFullerThanTheLast()
    {
        // Arrange
        using var provider = new StubProvider(Written);
        var world = await ConnectedAsync(provider);

        // Act
        var response = await world.Client.PostAsync(
            "/api/v1/recipe-drafts",
            new
            {
                kind = "idea",
                householdId = world.HouseholdId,
                material = "something with aubergines",
                language = "de"
            },
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.ContentHeaders.ContentType?.MediaType);

        var events = Events(response.Body!);

        // More than one, or this is a request that answers once wearing a
        // stream's clothes.
        Assert.True(events.Count > 1, $"The draft arrived as {events.Count} event(s).");

        // The title lands while the steps are still being written, which is the
        // whole reason for any of this.
        var first = events[0];
        Assert.Equal("Auberginenauflauf", first.GetProperty("draft").GetProperty("title").GetString());
        Assert.False(first.GetProperty("finished").GetBoolean());
        Assert.Empty(first.GetProperty("draft").GetProperty("steps").EnumerateArray());

        // One draft arriving, not several drafts.
        var draftIds = events
            .Select(one => one.GetProperty("draft").GetProperty("draftId").GetString())
            .Distinct()
            .ToList();

        Assert.Single(draftIds);

        var last = events[^1];

        Assert.True(last.GetProperty("finished").GetBoolean());
        Assert.False(last.TryGetProperty("problem", out var problem) && problem.ValueKind is not JsonValueKind.Null);

        var draft = last.GetProperty("draft");

        Assert.Equal("Warm und einfach", draft.GetProperty("description").GetString());
        Assert.Equal(2, draft.GetProperty("groups")[0].GetProperty("ingredients").GetArrayLength());
        Assert.Equal("Alles kochen", draft.GetProperty("steps")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task Draft_ShouldBeAskedForAsAStructuredOutput_NotAsARequestForJson()
    {
        // Arrange
        using var provider = new StubProvider(Written);
        var world = await ConnectedAsync(provider);

        // Act
        await world.Client.PostAsync(
            "/api/v1/recipe-drafts",
            new
            {
                kind = "idea",
                householdId = world.HouseholdId,
                material = "something with aubergines",
                language = "en"
            },
            Token);

        // Assert
        // Asserted on the wire rather than on the schema object, because the
        // client library rewrites what it is given on the way out — and it was
        // the rewriting that went wrong: it marked every property required
        // while leaving the request unstrict, so the answer was asked for in a
        // shape nothing held it to.
        var asked = JsonDocument.Parse(provider.LastRequest).RootElement
            .GetProperty("response_format")
            .GetProperty("json_schema");

        Assert.True(asked.GetProperty("strict").GetBoolean());

        var schema = asked.GetProperty("schema");

        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());

        // Every property required, which is what strict demands — and every
        // optional one a union with null, which is what makes that honest. A
        // required `prepMinutes` that could not be null is a model inventing a
        // cooking time, and an invented time on a recipe read out of a
        // photograph is the one failure of this feature nobody would catch.
        Assert.Equal(
            ["title", "description", "yieldAmount", "yieldLabel", "prepMinutes", "cookMinutes", "groups", "steps", "tags"],
            schema.GetProperty("required").EnumerateArray().Select(one => one.GetString()));

        Assert.Equal(
            ["integer", "null"],
            schema.GetProperty("properties").GetProperty("prepMinutes").GetProperty("type")
                .EnumerateArray().Select(one => one.GetString()));

        // The one that must not be null: a line with an amount and no
        // ingredient is not a shorter line, it is a mistake.
        Assert.Equal(
            "string",
            schema.GetProperty("properties").GetProperty("groups")
                .GetProperty("items").GetProperty("properties").GetProperty("ingredients")
                .GetProperty("items").GetProperty("properties").GetProperty("name")
                .GetProperty("type").GetString());
    }

    [Fact]
    public async Task Draft_ShouldReadANullAsSilence_NotAsAValue()
    {
        // Arrange
        // What a strict answer looks like when the recipe does not say: every
        // property present, and the ones it has nothing for set to null.
        using var provider = new StubProvider(
        [
            "{\"title\":\"Linsensuppe\",\"description\":null,\"yieldAmount\":null,",
            "\"yieldLabel\":null,\"prepMinutes\":null,\"cookMinutes\":null,",
            "\"groups\":[{\"name\":null,\"ingredients\":[",
            "{\"quantity\":null,\"unit\":null,\"name\":\"Linsen\",\"note\":null}]}],",
            "\"steps\":[{\"title\":null,\"text\":\"Alles kochen\",\"durationSeconds\":null}],",
            "\"tags\":[]}"
        ]);

        var world = await ConnectedAsync(provider);

        // Act
        var response = await world.Client.PostAsync(
            "/api/v1/recipe-drafts",
            new
            {
                kind = "idea",
                householdId = world.HouseholdId,
                material = "lentil soup",
                language = "de"
            },
            Token);

        // Assert
        var draft = Events(response.Body!)[^1].GetProperty("draft");

        // A null arrives as an absence, not as the string "null" and not as a
        // zero. The editor this opens in must show an empty cooking time.
        Assert.False(draft.TryGetProperty("prepMinutes", out var prep) && prep.ValueKind is not JsonValueKind.Null);
        Assert.False(draft.TryGetProperty("description", out var about) && about.ValueKind is not JsonValueKind.Null);

        var line = draft.GetProperty("groups")[0].GetProperty("ingredients")[0];

        Assert.Equal("Linsen", line.GetProperty("name").GetString());
        Assert.False(line.TryGetProperty("quantity", out var amount) && amount.ValueKind is not JsonValueKind.Null);
    }

    [Fact]
    public async Task Draft_ShouldSayWhyOnTheLastEvent_WhenTheModelAnswersWithNonsense()
    {
        // Arrange
        using var provider = new StubProvider(["I am afraid I cannot help with that."]);
        var world = await ConnectedAsync(provider);

        // Act
        var response = await world.Client.PostAsync(
            "/api/v1/recipe-drafts",
            new
            {
                kind = "idea",
                householdId = world.HouseholdId,
                material = "something with aubergines",
                language = "en"
            },
            Token);

        // Assert
        // A 200 that has already begun cannot become a 422, so the failure
        // travels as an event. This is the expected failure of the whole
        // feature, not an exceptional one.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var last = Events(response.Body!)[^1];

        Assert.True(last.GetProperty("finished").GetBoolean());
        Assert.Equal(
            "assistance.unusable_answer",
            last.GetProperty("problem").GetProperty("code").GetString());
    }

    /// <summary>Every event of a server-sent stream, decoded.</summary>
    private static List<JsonElement> Events(string body) =>
    [
        .. body
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(block => block.Split('\n'))
            .Where(line => line.StartsWith("data:", StringComparison.Ordinal))
            .Select(line => JsonDocument.Parse(line[5..].Trim()).RootElement)
    ];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed record World(ApiClient Client, Guid HouseholdId) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }

    /// <summary>An instance with the stub connected and drafting switched on.</summary>
    private async Task<World> ConnectedAsync(StubProvider provider)
    {
        await postgres.ResetAsync(Token);

        var client = postgres.Api.NewApiClient();

        await client.PostAsync(
            "/api/v1/users",
            new { email = "ada@example.com", displayName = "Ada", password = Password },
            Token);
        await client.PostAsync(
            "/api/v1/sessions",
            new { email = "ada@example.com", password = Password },
            Token);

        var settings = await client.PutAsync(
            "/api/v1/settings/assistance",
            new
            {
                enabled = true,
                connections = new[]
                {
                    new { provider = "openai", apiKey = "sk-stub", baseUrl = provider.Address }
                },
                uses = new[]
                {
                    new { capability = "draft", enabled = true, provider = "openai", model = "stub-one" }
                },
                monthlyBudget = (decimal?)null,
                personalBudget = (decimal?)null
            },
            Token);

        Assert.Equal(HttpStatusCode.OK, settings.StatusCode);

        var householdId = (await client.GetAsync("/api/v1/households", Token))
            .Json!.Value.GetProperty("items")[0].GetProperty("householdId").GetGuid();

        return new World(client, householdId);
    }

    /// <summary>
    /// A model provider, as far as the adapter can tell.
    /// </summary>
    /// <remarks>
    /// Answers every request with the chunks it was given, in OpenAI's
    /// streaming shape. A stub rather than a mocked client because what is
    /// being proved includes the adapter and the SDK underneath it — a double
    /// of the SDK would only prove that the double agrees with itself.
    /// </remarks>
    private sealed class StubProvider : IDisposable
    {
        private readonly HttpListener listener = new();
        private readonly CancellationTokenSource stopping = new();

        internal StubProvider(IReadOnlyList<string> chunks)
        {
            Address = $"http://localhost:{FreePort()}";

            listener.Prefixes.Add(Address + "/");
            listener.Start();

            _ = Task.Run(() => ServeAsync(chunks));
        }

        /// <summary>Where an administrator would point the connection.</summary>
        internal string Address { get; }

        /// <summary>The body of the last request, so a test can read what was asked.</summary>
        internal string LastRequest { get; private set; } = string.Empty;

        public void Dispose()
        {
            stopping.Cancel();
            listener.Close();
            stopping.Dispose();
        }

        private async Task ServeAsync(IReadOnlyList<string> chunks)
        {
            while (!stopping.IsCancellationRequested)
            {
                HttpListenerContext call;

                try
                {
                    call = await listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (HttpListenerException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                using (var body = new StreamReader(call.Request.InputStream))
                {
                    LastRequest = await body.ReadToEndAsync().ConfigureAwait(false);
                }

                await AnswerAsync(call, chunks).ConfigureAwait(false);
            }
        }

#pragma warning disable CA1822
        private async Task AnswerAsync(HttpListenerContext call, IReadOnlyList<string> chunks)
#pragma warning restore CA1822
        {
            call.Response.StatusCode = 200;
            call.Response.ContentType = "text/event-stream";
            call.Response.SendChunked = true;

            var body = call.Response.OutputStream;

            await using (body.ConfigureAwait(false))
            {
                foreach (var chunk in chunks)
                {
                    await WriteAsync(body, Delta(chunk)).ConfigureAwait(false);
                }

                await WriteAsync(body, Finished()).ConfigureAwait(false);
                await WriteAsync(body, "[DONE]").ConfigureAwait(false);
            }
        }

        private static async Task WriteAsync(Stream body, string data)
        {
            await body.WriteAsync(Encoding.UTF8.GetBytes($"data: {data}\n\n")).ConfigureAwait(false);
            await body.FlushAsync().ConfigureAwait(false);
        }

        private static string Delta(string text) => JsonSerializer.Serialize(new
        {
            id = "stub",
            @object = "chat.completion.chunk",
            created = 0,
            model = "stub-one",
            choices = new[] { new { index = 0, delta = new { content = text } } }
        });

        private static string Finished() => JsonSerializer.Serialize(new
        {
            id = "stub",
            @object = "chat.completion.chunk",
            created = 0,
            model = "stub-one",
            choices = new[] { new { index = 0, delta = new { }, finish_reason = "stop" } },
            usage = new { prompt_tokens = 11, completion_tokens = 22, total_tokens = 33 }
        });

        /// <summary>A port nobody is on, as far as the operating system knows.</summary>
        private static int FreePort()
        {
            using var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);

            probe.Start();

            var port = ((IPEndPoint)probe.LocalEndpoint).Port;

            probe.Stop();

            return port;
        }
    }
}
