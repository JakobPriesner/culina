using System.Net;
using System.Net.Http.Json;
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
    public async Task Registration_ShouldStillBeAllowed_AfterTheSignUpPageReadThePolicyMoreTimesThanTheLimit()
    {
        // Arrange
        await postgres.ResetAsync(Token);
        using var factory = new CulinaApiFactory(
            postgres,
            new Dictionary<string, string> { ["RateLimits:RegisterPerIpPerHour"] = "2" });
        using var client = factory.NewApiClient();

        // Act
        await client.GetAsync("/api/v1/registration/policy", Token);
        await client.GetAsync("/api/v1/registration/policy", Token);
        await client.GetAsync("/api/v1/registration/policy", Token);
        var registration = await client.PostAsync("/api/v1/users", Body("one@example.com"), Token);

        // Assert
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
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

    [Fact]
    public async Task SignIn_ShouldBeRefused_WhenEachAttemptCarriesADifferentMadeUpSessionCookie()
    {
        // Arrange
        await postgres.ResetAsync(Token);
        using var factory = new CulinaApiFactory(
            postgres,
            new Dictionary<string, string> { ["RateLimits:LoginPerIpPerMinute"] = "2" });
        using var client = factory.NewApiClient();

        // Act
        await SignInWithMadeUpCookie(client);
        await SignInWithMadeUpCookie(client);
        var third = await SignInWithMadeUpCookie(client);

        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.Equal("request.rate_limited", third.ProblemCode);
    }

    [Fact]
    public async Task Import_ShouldBeRefused_WhenThePersonSignsInAgainForAFreshBudget()
    {
        // Arrange
        await postgres.ResetAsync(Token);
        using var factory = new CulinaApiFactory(
            postgres,
            new Dictionary<string, string> { ["RateLimits:ImportsPerHour"] = "1" });
        using var first = await SignedInAsync(factory, register: true);
        using var second = await SignedInAsync(factory, register: false);

        // Act
        await first.PostAsync("/api/v1/recipe-imports", new { url = "not a web address" }, Token);
        var again = await second.PostAsync("/api/v1/recipe-imports", new { url = "not a web address" }, Token);

        // Assert
        // Counted per session, a second sign-in was a second allowance, and
        // the limit on making this server fetch was as many sign-ins as
        // anybody cared to make.
        Assert.Equal(HttpStatusCode.TooManyRequests, again.StatusCode);
        Assert.Equal("request.rate_limited", again.ProblemCode);
    }

    [Fact]
    public async Task ArchiveExport_ShouldBeLimitedPerPerson_AcrossTheirSessions()
    {
        // Arrange
        await postgres.ResetAsync(Token);
        using var factory = new CulinaApiFactory(
            postgres,
            new Dictionary<string, string> { ["RateLimits:ArchiveExportsPerHour"] = "1" });
        using var first = await SignedInAsync(factory, register: true);
        using var second = await SignedInAsync(factory, register: false);
        var me = await first.GetAsync("/api/v1/users/me", Token);
        var householdId = me.Json!.Value.GetProperty("households")[0].GetProperty("householdId").GetGuid();

        // Act
        var exported = await first.GetAsync($"/api/v1/households/{householdId}/archive", Token);
        var again = await second.GetAsync($"/api/v1/households/{householdId}/archive", Token);

        // Assert
        // Every photograph in the household, inline: the heaviest read there
        // is, and it had no limit of its own.
        Assert.Equal(HttpStatusCode.OK, exported.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, again.StatusCode);
        Assert.True(again.Headers.Contains("Retry-After"));
    }

    private static async Task<ApiClient> SignedInAsync(CulinaApiFactory factory, bool register)
    {
        var client = factory.NewApiClient();

        if (register)
        {
            await client.PostAsync("/api/v1/users", Body("ada@example.com"), Token);
        }

        await client.PostAsync(
            "/api/v1/sessions",
            new { email = "ada@example.com", password = "correct horse battery staple" },
            Token);

        return client;
    }

    private static Task<ApiResponse> SignInWithMadeUpCookie(ApiClient client)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/sessions")
        {
            Content = JsonContent.Create(new { email = "ada@example.com", password = "a guess" })
        };

        // The name the test host uses: its cookies are not Secure, so the
        // session cookie goes without its __Host- prefix.
        request.Headers.TryAddWithoutValidation("Cookie", $"culina.session={Guid.NewGuid():n}");

        return client.SendAsync(request, Token);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static object Body(string email) => new
    {
        email,
        displayName = "Ada",
        password = "correct horse battery staple"
    };
}
