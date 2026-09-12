using System.Net;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Pipeline;

/// <summary>
/// The limiter runs before authentication, so a brute-force attempt is refused
/// before a password hash is computed or a connection is taken from the pool.
/// </summary>
[Collection(RequiresDatabase.Name)]
public class RateLimitTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Registration_ShouldBeRefused_OnceTheHourlyLimitIsReached()
    {
        // Arrange
        await postgres.ResetAsync(Token);
        using var factory = new CulinaApiFactory(
            postgres,
            new Dictionary<string, string> { ["RateLimits:RegisterPerIpPerHour"] = "2" });
        using var client = factory.NewApiClient();

        // Act
        await client.PostAsync("/api/v1/users", Body("one@example.com"), Token);
        await client.PostAsync("/api/v1/users", Body("two@example.com"), Token);
        var third = await client.PostAsync("/api/v1/users", Body("three@example.com"), Token);

        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.Equal("request.rate_limited", third.ProblemCode);
    }

    [Fact]
    public async Task Rejection_ShouldSayWhenToRetry_SoAClientCanBackOffCorrectly()
    {
        // Arrange
        await postgres.ResetAsync(Token);
        using var factory = new CulinaApiFactory(
            postgres,
            new Dictionary<string, string> { ["RateLimits:RegisterPerIpPerHour"] = "1" });
        using var client = factory.NewApiClient();

        // Act
        await client.PostAsync("/api/v1/users", Body("one@example.com"), Token);
        var second = await client.PostAsync("/api/v1/users", Body("two@example.com"), Token);

        // Assert
        Assert.True(second.Headers.TryGetValues("Retry-After", out var retryAfter));
        Assert.NotEmpty(Assert.Single(retryAfter));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static object Body(string email) => new
    {
        email,
        displayName = "Ada",
        password = "correct horse battery staple"
    };
}
