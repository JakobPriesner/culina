using System.Net;
using System.Text.Json;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Import;

/// <summary>The stream of a person's imports: a snapshot first, then each change as it happens, and nobody else's.</summary>
[Collection(RequiresDatabase.Name)]
public class RecipeIntakeStreamTests(PostgresFixture postgres)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Events_ShouldRequireASession()
    {
        await postgres.ResetAsync(Token);
        using var anonymous = postgres.Api.NewApiClient();

        var response = await anonymous.GetAsync("/api/v1/recipe-intakes/events", Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Events_ShouldSendASnapshot_ThenPushAChangeWithoutAsking()
    {
        var kitchen = await Kitchen.OpenAsync(postgres);
        using var client = kitchen.Client;
        using var stream = await client.OpenStreamAsync("/api/v1/recipe-intakes/events", Token);

        Assert.Equal(HttpStatusCode.OK, stream.StatusCode);
        Assert.Equal("text/event-stream", stream.Content.Headers.ContentType?.MediaType);

        using var reader = new StreamReader(await stream.Content.ReadAsStreamAsync(Token));
        var snapshot = await NextAsync(reader);

        Assert.True(snapshot.GetProperty("snapshot").GetBoolean());
        Assert.Empty(snapshot.GetProperty("jobs").EnumerateArray());

        var id = Guid.NewGuid();
        using var form = new MultipartFormDataContent { { new StringContent("120 g beans"), "material" } };
        var started = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, $"/api/v1/recipe-intakes?id={id}&householdId={kitchen.HouseholdId}&language=en") { Content = form },
            Token);
        Assert.Equal(HttpStatusCode.Accepted, started.StatusCode);

        // The worker takes it and, with no assistant set up, ends it: both arrive on this one connection.
        var stages = new List<string>();
        while (stages.LastOrDefault() != "failed")
        {
            var next = await NextAsync(reader);

            Assert.False(next.GetProperty("snapshot").GetBoolean());
            stages.AddRange(next.GetProperty("jobs").EnumerateArray()
                .Where(job => job.GetProperty("id").GetGuid() == id)
                .Select(job => job.GetProperty("stage").GetString()!));
        }

        // A reconnect starts over from what the database says now.
        using var again = await client.OpenStreamAsync("/api/v1/recipe-intakes/events", Token);
        using var second = new StreamReader(await again.Content.ReadAsStreamAsync(Token));
        var restored = await NextAsync(second);

        Assert.True(restored.GetProperty("snapshot").GetBoolean());
        Assert.Equal("failed", Assert.Single(restored.GetProperty("jobs").EnumerateArray()).GetProperty("stage").GetString());
    }

    [Fact]
    public async Task Events_ShouldNotShowAnotherPersonsImports()
    {
        var kitchen = await Kitchen.OpenAsync(postgres);
        using var client = kitchen.Client;
        using var stranger = await Kitchen.StrangerAsync(postgres);
        using var stream = await stranger.OpenStreamAsync("/api/v1/recipe-intakes/events", Token);
        using var reader = new StreamReader(await stream.Content.ReadAsStreamAsync(Token));
        Assert.Empty((await NextAsync(reader)).GetProperty("jobs").EnumerateArray());

        using var form = new MultipartFormDataContent { { new StringContent("120 g beans"), "material" } };
        var started = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, $"/api/v1/recipe-intakes?id={Guid.NewGuid()}&householdId={kitchen.HouseholdId}&language=en") { Content = form },
            Token);
        Assert.Equal(HttpStatusCode.Accepted, started.StatusCode);
        await Task.Delay(TimeSpan.FromSeconds(3), Token);

        // Nothing arrives for the stranger: the next data line would be the 15 s heartbeat, which carries no imports.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(1));
        try
        {
            while (await reader.ReadLineAsync(timeout.Token) is { } line)
            {
                Assert.False(line.StartsWith("data:", StringComparison.Ordinal), line);
            }
        }
        catch (OperationCanceledException)
        {
            // Quiet, as expected.
        }
    }

    /// <summary>Reads one event's data, skipping blank separators.</summary>
    private static async Task<JsonElement> NextAsync(StreamReader reader)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (await reader.ReadLineAsync(timeout.Token) is { } line)
        {
            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                return JsonDocument.Parse(line[5..].Trim()).RootElement.Clone();
            }
        }

        throw new InvalidOperationException("The stream ended.");
    }
}
