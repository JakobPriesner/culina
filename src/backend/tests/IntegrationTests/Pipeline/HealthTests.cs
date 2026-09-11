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
        // Arrange
        using var client = postgres.Api.CreateClient();

        // Act
        using var response = await client.GetAsync(
            new Uri("/health/live", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // Assert
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
        // Arrange
        using var client = postgres.Api.CreateClient();

        // Act
        using var response = await client.GetAsync(
            new Uri("/health/ready", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);
        Assert.Equal("ready", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Health_ShouldNotRequireAuthentication_SoAProbeCanReachIt()
    {
        // Arrange
        using var client = postgres.Api.CreateClient();

        // Act
        using var live = await client.GetAsync(
            new Uri("/health/live", UriKind.Relative),
            TestContext.Current.CancellationToken);
        using var ready = await client.GetAsync(
            new Uri("/health/ready", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(HttpStatusCode.Unauthorized, live.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, ready.StatusCode);
    }

    [Fact]
    public async Task NonApiRoute_ShouldFallBackToTheAppShell_RatherThanAProblemDocument()
    {
        // Arrange
        using var client = postgres.Api.CreateClient();

        // Act
        using var response = await client.GetAsync(
            new Uri("/recipes/some-client-route", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // Assert
        // The SPA owns client-side routes. There is no built shell in the test
        // host, so a 404 is expected — but it must not be the API's problem
        // document, which would mean the fallback never ran.
        Assert.NotEqual("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UnmatchedApiRoute_ShouldStillReturnAProblemDocument_NotTheAppShell()
    {
        // Arrange
        using var client = postgres.Api.CreateClient();

        // Act
        using var response = await client.GetAsync(
            new Uri("/api/v1/not-a-resource", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
