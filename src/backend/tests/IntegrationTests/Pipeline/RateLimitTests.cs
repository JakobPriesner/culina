using System.Net;
using System.Net.Http.Json;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Pipeline;

/// <summary>
/// The limiter runs before authentication, so brute force is refused before a hash is computed or a
/// connection taken.
/// </summary>
[Collection(RequiresDatabase.Name)]
public class RateLimitTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Registration_ShouldBeRefused_OnceTheHourlyLimitIsReached()
    {
        await postgres.ResetAsync(Token);
        using var factory = new CulinaApiFactory(
            postgres,
            new Dictionary<string, string> { ["RateLimits:RegisterPerIpPerHour"] = "2" });
        using var client = factory.NewApiClient();

        await client.PostAsync("/api/v1/users", Body("one@example.com"), Token);
        await client.PostAsync("/api/v1/users", Body("two@example.com"), Token);
        var third = await client.PostAsync("/api/v1/users", Body("three@example.com"), Token);

        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.Equal("request.rate_limited", third.ProblemCode);
    }

    [Fact]
    public async Task Registration_ShouldStillBeAllowed_AfterTheSignUpPageReadThePolicyMoreTimesThanTheLimit()
    {
        await postgres.ResetAsync(Token);
        using var factory = new CulinaApiFactory(
            postgres,
            new Dictionary<string, string> { ["RateLimits:RegisterPerIpPerHour"] = "2" });
        using var client = factory.NewApiClient();

        await client.GetAsync("/api/v1/registration/policy", Token);
        await client.GetAsync("/api/v1/registration/policy", Token);
        await client.GetAsync("/api/v1/registration/policy", Token);
        var registration = await client.PostAsync("/api/v1/users", Body("one@example.com"), Token);

        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
    }

    [Fact]
    public async Task Rejection_ShouldSayWhenToRetry_SoAClientCanBackOffCorrectly()
    {
        await postgres.ResetAsync(Token);
        using var factory = new CulinaApiFactory(
            postgres,
            new Dictionary<string, string> { ["RateLimits:RegisterPerIpPerHour"] = "1" });
        using var client = factory.NewApiClient();

        await client.PostAsync("/api/v1/users", Body("one@example.com"), Token);
        var second = await client.PostAsync("/api/v1/users", Body("two@example.com"), Token);

        Assert.True(second.Headers.TryGetValues("Retry-After", out var retryAfter));
        Assert.NotEmpty(Assert.Single(retryAfter));
    }

    [Fact]
    public async Task SignIn_ShouldBeRefused_WhenEachAttemptCarriesADifferentMadeUpSessionCookie()
    {
        await postgres.ResetAsync(Token);
        using var factory = new CulinaApiFactory(
            postgres,
            new Dictionary<string, string> { ["RateLimits:LoginPerIpPerMinute"] = "2" });
        using var client = factory.NewApiClient();

        await SignInWithMadeUpCookie(client);
        await SignInWithMadeUpCookie(client);
        var third = await SignInWithMadeUpCookie(client);

        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.Equal("request.rate_limited", third.ProblemCode);
    }

    [Fact]
    public async Task Import_ShouldBeRefused_WhenThePersonSignsInAgainForAFreshBudget()
    {
        await postgres.ResetAsync(Token);
        using var factory = new CulinaApiFactory(
            postgres,
            new Dictionary<string, string> { ["RateLimits:ImportsPerHour"] = "1" });
        using var first = await SignedInAsync(factory, register: true);
        using var second = await SignedInAsync(factory, register: false);

        await first.PostAsync("/api/v1/recipe-imports", new { url = "not a web address" }, Token);
        var again = await second.PostAsync("/api/v1/recipe-imports", new { url = "not a web address" }, Token);

        // Counted per session, a second sign-in was a second allowance, making the fetch limit
        // unbounded.
        Assert.Equal(HttpStatusCode.TooManyRequests, again.StatusCode);
        Assert.Equal("request.rate_limited", again.ProblemCode);
    }

    [Fact]
    public async Task ArchiveExport_ShouldBeLimitedPerPerson_AcrossTheirSessions()
    {
        await postgres.ResetAsync(Token);
        using var factory = new CulinaApiFactory(
            postgres,
            new Dictionary<string, string> { ["RateLimits:ArchiveExportsPerHour"] = "1" });
        using var first = await SignedInAsync(factory, register: true);
        using var second = await SignedInAsync(factory, register: false);
        var me = await first.GetAsync("/api/v1/users/me", Token);
        var householdId = me.Json!.Value.GetProperty("households")[0].GetProperty("householdId").GetGuid();

        var exported = await first.GetAsync($"/api/v1/households/{householdId}/archive", Token);
        var again = await second.GetAsync($"/api/v1/households/{householdId}/archive", Token);

        // Every photograph inline: the heaviest read there is, and it had no limit.
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

        // The test host's cookies are not Secure, so the session cookie has no __Host- prefix.
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

    [Fact]
    public async Task ArchiveRestore_ShouldShareTheArchiveBudget_WithTakingOne()
    {
        await postgres.ResetAsync(Token);
        using var factory = new CulinaApiFactory(
            postgres,
            new Dictionary<string, string> { ["RateLimits:ArchiveExportsPerHour"] = "1" });
        using var client = await SignedInAsync(factory, register: true);
        var me = await client.GetAsync("/api/v1/users/me", Token);
        var householdId = me.Json!.Value.GetProperty("households")[0].GetProperty("householdId").GetGuid();

        var exported = await client.GetAsync($"/api/v1/households/{householdId}/archive", Token);
        var restored = await RestoreAsync(client, householdId);

        Assert.Equal(HttpStatusCode.OK, exported.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, restored.StatusCode);
    }

    private static async Task<ApiResponse> RestoreAsync(ApiClient client, Guid householdId)
    {
        using var content = new MultipartFormDataContent();
        using var file = new ByteArrayContent("{}"u8.ToArray());

        content.Add(file, "file", "culina.json");

        return await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, $"/api/v1/households/{householdId}/archive")
            {
                Content = content
            },
            Token);
    }
}
