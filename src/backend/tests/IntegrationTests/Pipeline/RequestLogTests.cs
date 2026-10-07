using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using IntegrationTests.Fixtures;
using Microsoft.AspNetCore.TestHost;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace IntegrationTests.Pipeline;

/// <summary>
/// The one line every API request leaves, which is what an operator with
/// nothing but the container's output reads.
/// </summary>
[Collection(RequiresDatabase.Name)]
public class RequestLogTests(PostgresFixture postgres)
{
    private const string Category = "Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware";

    [Fact]
    public async Task FailedRequest_ShouldBeLogged_WithItsRouteAndErrorCode()
    {
        // Arrange
        using var factory = new CulinaApiFactory(postgres);
        using var client = factory.NewApiClient();

        // Act
        var response = await client.GetAsync("/api/v1/users/me", Token);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var line = await factory.Logs.WaitForAsync(line =>
            line.Category == Category && line["Path"] == "/api/v1/users/me");

        Assert.NotNull(line);
        Assert.Equal("401", line["StatusCode"]);
        Assert.Equal("api/v1/users/me", line["Route"]?.TrimStart('/'));
        Assert.Equal(response.ProblemCode, line["ErrorCode"]);
        // The id the user is shown, under a name of its own: the host's scope
        // already says RequestId for something else.
        Assert.Equal(Assert.Single(response.Headers.GetValues("X-Request-Id")), line["TraceId"]);
        Assert.NotEqual(line["TraceId"], line.Scopes.GetValueOrDefault("RequestId") as string);
        Assert.NotNull(line["ClientAddress"]);
    }

    [Fact]
    public async Task HealthProbesAndTheAppShell_ShouldLeaveNoRequestLine()
    {
        // Arrange
        using var factory = new CulinaApiFactory(postgres);
        using var client = factory.NewApiClient();

        // Act
        await client.GetAsync("/health/live", Token);
        await client.GetAsync("/", Token);
        await client.GetAsync("/api/v1/users/me", Token);

        // Assert: the API request is logged, so the others had their chance.
        // Asserted on every line rather than by path, because a line the
        // request side switched off has no path to search for.
        Assert.NotNull(await factory.Logs.WaitForAsync(line =>
            line.Category == Category && line["Path"] == "/api/v1/users/me"));
        Assert.All(
            factory.Logs.Lines.Where(line => line.Category == Category),
            line => Assert.StartsWith("/api/", line["Path"], StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShareTokensAndInvitationCodes_ShouldReachNoLogLineAndNoSpan()
    {
        // Arrange
        var spans = new RecordedSpans();
        using var api = new CulinaApiFactory(postgres);
        using var host = api.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddProcessor(spans))));
        using var client = new ApiClient(host.CreateClient());
        var token = $"token{Guid.NewGuid():n}";
        var code = $"code{Guid.NewGuid():n}";

        // Act
        // The image request is refused for its query, which is a Warning of
        // its own that names the path.
        await client.GetAsync($"/api/v1/shared-recipes/{token}", Token);
        await client.GetAsync($"/api/v1/shared-recipes/{token}/image?unexpected=1", Token);
        await client.PostAsync($"/api/v1/invitations/{code}/redemptions", new { }, Token);
        await client.GetAsync($"/shared/{token}", Token);
        await client.GetAsync($"/join/{code}", Token);

        // Assert
        var requestLine = await api.Logs.WaitForAsync(line =>
            line.Category == Category && line["Path"] == "/api/v1/invitations/***/redemptions");
        var rejection = await api.Logs.WaitForAsync(line => line.EventId == 1801);
        Assert.NotNull(requestLine);
        Assert.Equal("/api/v1/shared-recipes/***/image", rejection?["Path"]);
        Assert.True(await spans.WaitForAsync("/join/***"), "The last request was never traced.");

        string[] secrets = [token, code];
        var logged = api.Logs.Lines.SelectMany(line =>
            new[] { line.Message, line.Exception }
                .Concat(line.Fields.Values.Select(value => $"{value}"))
                .Concat(line.Scopes.Values.Select(value => $"{value}")));

        Assert.DoesNotContain(logged, text => secrets.Any(secret => text?.Contains(secret, StringComparison.Ordinal) == true));
        Assert.DoesNotContain(spans.Texts, text => secrets.Any(secret => text.Contains(secret, StringComparison.Ordinal)));
        Assert.Contains("/api/v1/shared-recipes/***", spans.Texts);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Every name and tag value of every span the host finished.</summary>
    private sealed class RecordedSpans : BaseProcessor<Activity>
    {
        private readonly ConcurrentQueue<string> texts = new();

        public IReadOnlyList<string> Texts => [.. texts];

        public override void OnEnd(Activity data)
        {
            texts.Enqueue(data.DisplayName);

            foreach (var (_, value) in data.TagObjects)
            {
                texts.Enqueue($"{value}");
            }
        }

        /// <summary>Waits briefly for a span to end: it can end after the response arrived.</summary>
        public async Task<bool> WaitForAsync(string text)
        {
            for (var attempt = 0; attempt < 40 && !texts.Contains(text); attempt++)
            {
                await Task.Delay(50, Token);
            }

            return texts.Contains(text);
        }
    }
}
