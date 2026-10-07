using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Pipeline;

[Collection(RequiresDatabase.Name)]
public class HealthTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Live_ShouldAnswer_WithoutTouchingAnyDependency()
    {
        using var client = postgres.Api.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/health/live", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // Liveness must not depend on the database: a dependency failure here
        // would make an orchestrator restart a process that is working fine.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);
        Assert.Equal("live", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Ready_ShouldAnswer_WhenTheDatabaseIsReachable()
    {
        using var client = postgres.Api.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/health/ready", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);
        Assert.Equal("ready", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Health_ShouldNotRequireAuthentication_SoAProbeCanReachIt()
    {
        using var client = postgres.Api.CreateClient();

        using var live = await client.GetAsync(
            new Uri("/health/live", UriKind.Relative),
            TestContext.Current.CancellationToken);
        using var ready = await client.GetAsync(
            new Uri("/health/ready", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.NotEqual(HttpStatusCode.Unauthorized, live.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, ready.StatusCode);
    }

    [Fact]
    public async Task NonApiRoute_ShouldFallBackToTheAppShell_RatherThanAProblemDocument()
    {
        using var client = postgres.Api.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/recipes/some-client-route", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // The SPA owns client-side routes. There is no built shell in the test
        // host, so a 404 is expected — but it must not be the API's problem
        // document, which would mean the fallback never ran.
        Assert.NotEqual("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UnmatchedApiRoute_ShouldStillReturnAProblemDocument_NotTheAppShell()
    {
        using var client = postgres.Api.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/api/v1/not-a-resource", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AuthenticationChallenge_ShouldStayA401_WhenItIsGivenAProblemBody()
    {
        using var client = postgres.Api.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/api/v1/sessions", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // Regression: the status-code-pages handler used to derive the status
        // from the error type, which turned every framework-generated status
        // into a 500.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);
        Assert.Equal("auth.not_authenticated", problem.GetProperty("code").GetString());
    }
}
