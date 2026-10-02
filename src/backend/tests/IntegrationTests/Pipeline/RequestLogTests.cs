using System.Net;
using IntegrationTests.Fixtures;

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

    private static CancellationToken Token => TestContext.Current.CancellationToken;
}
