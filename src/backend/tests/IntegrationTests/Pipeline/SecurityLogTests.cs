using System.Net;
using IntegrationTests.Fixtures;
using Microsoft.Extensions.Logging;

namespace IntegrationTests.Pipeline;

/// <summary>
/// Refusals an operator bans on: each under its own event id, with the client address and without
/// the address tried.
/// </summary>
[Collection(RequiresDatabase.Name)]
public class SecurityLogTests(PostgresFixture postgres)
{
    [Fact]
    public async Task RefusedSignIn_ShouldBeLogged_WithTheClientAddressAndWithoutTheEmail()
    {
        await postgres.ResetAsync(Token);
        using var factory = new CulinaApiFactory(postgres);
        using var client = factory.NewApiClient();

        var response = await client.PostAsync(
            "/api/v1/sessions",
            new { email = "ada@example.com", password = "a guess" },
            Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var line = Assert.Single(factory.Logs.Lines, line => line.EventId == 1001);
        Assert.Equal(LogLevel.Warning, line.Level);
        Assert.NotNull(line["ClientAddress"]);
        Assert.DoesNotContain(factory.Logs.Lines, logged => logged.Message.Contains("ada@", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RateLimitRejection_ShouldBeLogged_WithTheClientAddress()
    {
        await postgres.ResetAsync(Token);
        using var factory = new CulinaApiFactory(
            postgres,
            new Dictionary<string, string> { ["RateLimits:LoginPerIpPerMinute"] = "1" });
        using var client = factory.NewApiClient();
        var body = new { email = "ada@example.com", password = "a guess" };

        await client.PostAsync("/api/v1/sessions", body, Token);
        var refused = await client.PostAsync("/api/v1/sessions", body, Token);

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);

        var line = Assert.Single(factory.Logs.Lines, line => line["Reason"] == "request.rate_limited");
        Assert.Equal(1801, line.EventId);
        Assert.NotNull(line["ClientAddress"]);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;
}
