using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Pipeline;

[Collection(RequiresDatabase.Name)]
public class RequestContextTests(PostgresFixture postgres)
{
    [Fact]
    public async Task EveryResponse_ShouldCarryARequestId_WhenTheRequestIsHandled()
    {
        // Arrange
        using var factory = new CulinaApiFactory(postgres);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync(
            new Uri("/health/live", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(response.Headers.TryGetValues("X-Request-Id", out var ids));
        Assert.NotEmpty(Assert.Single(ids));
    }

    [Fact]
    public async Task UnmatchedRoute_ShouldReturnAProblemDocument_RatherThanAnEmptyBody()
    {
        // Arrange
        using var factory = new CulinaApiFactory(postgres);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync(
            new Uri("/api/v1/nothing-here", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);
        // The frontend parses one error format; a routing 404 is not an
        // exception to that.
        Assert.Equal("request.no_such_endpoint", problem.GetProperty("code").GetString());
        Assert.NotEmpty(problem.GetProperty("requestId").GetString()!);
    }

    [Fact]
    public async Task Migrations_ShouldHaveRun_WhenTheHostStarted()
    {
        // Arrange
        using var factory = new CulinaApiFactory(postgres);

        // Act
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(
            new Uri("/health/live", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // Assert
        // Starting the host runs the migration hosted service, so a schema
        // failure would have prevented this response entirely.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
