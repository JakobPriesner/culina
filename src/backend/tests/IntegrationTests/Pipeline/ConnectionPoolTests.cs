using System.Net;
using Api.Endpoints;
using Api.Infrastructure;
using IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Pipeline;

/// <summary>A request that stays open (an import, a streamed draft) must not keep a connection out of the pool, or open tabs would drain it.</summary>
[Collection(RequiresDatabase.Name)]
public class ConnectionPoolTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task AnOpenResponse_ShouldLeaveThePoolToOtherRequests()
    {
        await postgres.ResetAsync(Token);

        var stream = new HeldOpenEndpoint();
        using var factory = new CulinaApiFactory(
            postgres,
            new Dictionary<string, string> { ["Database:MaxPoolSize"] = "1" },
            replace: services => services.AddSingleton<IEndpoint>(stream));
        using var client = factory.NewApiClient();

        await client.PostAsync(
            "/api/v1/users",
            new { email = "ada@example.com", displayName = "Ada", password = Password },
            Token);
        await client.PostAsync("/api/v1/sessions", new { email = "ada@example.com", password = Password }, Token);

        var held = client.GetAsync(HeldOpenEndpoint.Path, Token);
        await stream.Entered.WaitAsync(TimeSpan.FromSeconds(10), Token);

        // The only connection there is: free, though the first request is authenticated and still open.
        var other = await client.GetAsync("/api/v1/sessions", Token);
        stream.Close();

        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await held).StatusCode);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A signed-in response that stays open until the test closes it, and reads nothing.</summary>
    private sealed class HeldOpenEndpoint : IEndpoint
    {
        internal const string Path = $"{ApiPaths.V1}/test-only/held-open";

        private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task Entered => entered.Task;

        internal void Close() => closed.TrySetResult();

        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet(Path, async () =>
                {
                    entered.TrySetResult();
                    await closed.Task;

                    return Results.Ok();
                })
                .RequireAuthorization();
    }
}
