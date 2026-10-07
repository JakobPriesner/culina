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
        using var client = postgres.Api.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/health/live", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.True(response.Headers.TryGetValues("X-Request-Id", out var ids));
        Assert.NotEmpty(Assert.Single(ids));
    }

    [Fact]
    public async Task UnmatchedRoute_ShouldReturnAProblemDocument_RatherThanAnEmptyBody()
    {
        using var client = postgres.Api.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/api/v1/nothing-here", UriKind.Relative),
            TestContext.Current.CancellationToken);

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
        using var client = postgres.Api.CreateClient();
        using var response = await client.GetAsync(
            new Uri("/health/live", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // Starting the host runs the migration hosted service, so a schema
        // failure would have prevented this response entirely.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
