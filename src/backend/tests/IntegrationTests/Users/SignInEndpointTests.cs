using System.Diagnostics;
using System.Net;
using Application.Abstractions.Settings;
using IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Users;

/// <summary>
/// Every rejection path: a sign-in endpoint tested only on its happy path is one nobody knows is
/// safe.
/// </summary>
[Collection(RequiresDatabase.Name)]
public class SignInEndpointTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task SignIn_ShouldStartASession_WhenTheCredentialsAreCorrect()
    {
        using var client = await RegisteredClientAsync("ada@example.com");

        var response = await client.PostAsync("/api/v1/sessions", Credentials("ada@example.com"), Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotEmpty(response.Json!.Value.GetProperty("csrfToken").GetString()!);
        Assert.NotNull(client.CsrfToken);
    }

    [Fact]
    public async Task SignIn_ShouldNeverPutTheSessionTokenInTheBody_OnlyInAnHttpOnlyCookie()
    {
        using var client = await RegisteredClientAsync("ada@example.com");

        var response = await client.PostAsync("/api/v1/sessions", Credentials("ada@example.com"), Token);

        var cookies = response.Headers.GetValues("Set-Cookie").ToList();
        var sessionCookie = Assert.Single(cookies, cookie => cookie.StartsWith("culina.session=", StringComparison.Ordinal));
        Assert.Contains("httponly", sessionCookie, StringComparison.OrdinalIgnoreCase);

        var sessionToken = sessionCookie["culina.session=".Length..].Split(';', 2)[0];
        Assert.DoesNotContain(sessionToken, response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SignIn_ShouldMakeTheCsrfCookieReadable_BecauseTheClientMustEchoIt()
    {
        using var client = await RegisteredClientAsync("ada@example.com");

        var response = await client.PostAsync("/api/v1/sessions", Credentials("ada@example.com"), Token);

        var csrfCookie = Assert.Single(
            response.Headers.GetValues("Set-Cookie"),
            cookie => cookie.StartsWith("culina.csrf=", StringComparison.Ordinal));
        Assert.DoesNotContain("httponly", csrfCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SignIn_ShouldSayInvalidCredentials_WhenThePasswordIsWrong()
    {
        using var client = await RegisteredClientAsync("ada@example.com");

        var response = await client.PostAsync(
            "/api/v1/sessions",
            new { email = "ada@example.com", password = "not the password" },
            Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("auth.invalid_credentials", response.ProblemCode);
    }

    [Fact]
    public async Task SignIn_ShouldAnswerIdentically_WhetherOrNotTheAccountExists()
    {
        using var client = await RegisteredClientAsync("ada@example.com");

        var wrongPassword = await client.PostAsync(
            "/api/v1/sessions",
            new { email = "ada@example.com", password = "not the password" },
            Token);
        var unknownAccount = await client.PostAsync(
            "/api/v1/sessions",
            new { email = "nobody@example.com", password = "not the password" },
            Token);

        // Telling the two apart would let the form discover which addresses are registered.
        Assert.Equal(wrongPassword.StatusCode, unknownAccount.StatusCode);
        Assert.Equal(wrongPassword.ProblemCode, unknownAccount.ProblemCode);
        Assert.Equal(
            wrongPassword.Json!.Value.GetProperty("detail").GetString(),
            unknownAccount.Json!.Value.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task SignIn_ShouldCostTheSame_WhetherOrNotTheAccountExists()
    {
        using var client = await RegisteredClientAsync("ada@example.com");
        await Warm(client);

        var known = await TimeAsync(() => client.PostAsync(
            "/api/v1/sessions",
            new { email = "ada@example.com", password = "not the password" },
            Token));
        var unknown = await TimeAsync(() => client.PostAsync(
            "/api/v1/sessions",
            new { email = "nobody@example.com", password = "not the password" },
            Token));

        // Skipping the hash for an unknown address would make that response far faster and leak
        // which addresses are registered. The bound is loose: it asserts the work happens, not that
        // timings match.
        var ratio = known.TotalMilliseconds / Math.Max(unknown.TotalMilliseconds, 1);
        Assert.InRange(ratio, 0.2, 5.0);
    }

    [Theory]
    [InlineData("application/x-www-form-urlencoded", "email=ada%40example.com&password=correct+horse+battery+staple")]
    [InlineData("text/plain", """{"email":"ada@example.com","password":"correct horse battery staple"}""")]
    [InlineData("multipart/form-data; boundary=x", "--x\r\nContent-Disposition: form-data; name=\"email\"\r\n\r\nada@example.com\r\n--x--\r\n")]
    public async Task SignIn_ShouldRefuseEveryBodyACrossSiteFormCanSend(string contentType, string body)
    {
        using var client = await RegisteredClientAsync("ada@example.com");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/sessions")
        {
            Content = new StringContent(body)
        };
        request.Content.Headers.Remove("Content-Type");
        request.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
        request.Headers.TryAddWithoutValidation("Origin", "https://attacker.example");

        var response = await client.SendAsync(request, Token);

        // This, not the same-origin guard, stops login CSRF: that guard only sees requests with a
        // session cookie, and a cross-site login carries none. A form can send only three content
        // types and the endpoint reads only application/json, so routing never reaches it (the app
        // shell's catch-all answers 404, not 415).
        Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.UnsupportedMediaType, $"Expected a refusal, got {response.StatusCode}.");
        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.Equal(0L, await postgres.QuerySingleAsync<long>("select count(*) from sessions;", Token));
    }

    [Fact]
    public async Task SignedInRequest_ShouldBeRejected_WhenItOmitsTheCsrfToken()
    {
        using var client = await SignedInClientAsync("ada@example.com");

        // Cookies but no CSRF header: the shape of a cross-site request riding this browser's
        // cookie jar.
        var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Delete, "/api/v1/sessions/current"),
            Token,
            attachCsrf: false);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("auth.csrf_invalid", response.ProblemCode);
    }

    [Fact]
    public async Task SignOut_ShouldEndTheSession_SoTheCookieStopsWorking()
    {
        using var client = await SignedInClientAsync("ada@example.com");

        var signedOut = await client.DeleteAsync("/api/v1/sessions/current", Token);
        var afterwards = await client.GetAsync("/api/v1/sessions", Token);

        Assert.Equal(HttpStatusCode.NoContent, signedOut.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterwards.StatusCode);
    }

    [Fact]
    public async Task SignIn_ShouldEndTheSessionTheBrowserHeld_WhenItSignsInAgain()
    {
        using var client = await RegisteredClientAsync("ada@example.com");
        var first = await client.PostAsync("/api/v1/sessions", Credentials("ada@example.com"), Token);
        var replaced = SessionTokenFrom(first);

        await client.PostAsync("/api/v1/sessions", Credentials("ada@example.com"), Token);

        // The old row used to stay alive beside the new one, usable by anybody who had copied its
        // cookie.
        var devices = await client.GetAsync("/api/v1/sessions", Token);
        Assert.Single(devices.Json!.Value.GetProperty("items").EnumerateArray());

        using var holderOfTheOldCookie = postgres.Api.NewApiClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/sessions");
        request.Headers.TryAddWithoutValidation("Cookie", $"culina.session={replaced}");
        var reused = await holderOfTheOldCookie.SendAsync(request, Token);
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
    }

    [Fact]
    public async Task SignIn_ShouldLeaveTheEarlierSession_WhenThePasswordIsWrong()
    {
        using var client = await SignedInClientAsync("ada@example.com");

        await client.PostAsync(
            "/api/v1/sessions",
            new { email = "ada@example.com", password = "not the password" },
            Token);

        var stillSignedIn = await client.GetAsync("/api/v1/sessions", Token);
        Assert.Equal(HttpStatusCode.OK, stillSignedIn.StatusCode);
    }

    [Fact]
    public async Task Sessions_ShouldListTheCallersDevices_AndMarkTheCurrentOne()
    {
        using var client = await SignedInClientAsync("ada@example.com");

        var response = await client.GetAsync("/api/v1/sessions", Token);

        var items = response.Json!.Value.GetProperty("items").EnumerateArray().ToList();
        Assert.Single(items, item => item.GetProperty("isCurrent").GetBoolean());
    }

    [Fact]
    public async Task Revoke_ShouldNotSeeAnotherUsersSession_EvenWithItsExactId()
    {
        await postgres.ResetAsync(Token);
        using var owner = postgres.Api.NewApiClient();
        await Register(owner, "owner@example.com");
        await owner.PostAsync("/api/v1/sessions", Credentials("owner@example.com"), Token);
        var ownerSessionId = (await owner.GetAsync("/api/v1/sessions", Token))
            .Json!.Value.GetProperty("items")[0].GetProperty("sessionId").GetGuid();

        using var stranger = postgres.Api.NewApiClient();
        OpenRegistration();
        await Register(stranger, "mallory@example.com");
        await stranger.PostAsync("/api/v1/sessions", Credentials("mallory@example.com"), Token);

        var response = await stranger.DeleteAsync($"/api/v1/sessions/{ownerSessionId}", Token);

        // Ownership is in the SQL WHERE, so another user's session is not refused, it does not
        // exist for this caller.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static object Credentials(string email) => new { email, password = Password };

    private static string SessionTokenFrom(ApiResponse signedIn) =>
        signedIn.Headers.GetValues("Set-Cookie")
            .Single(cookie => cookie.StartsWith("culina.session=", StringComparison.Ordinal))
            ["culina.session=".Length..]
            .Split(';', 2)[0];

    private static async Task Register(ApiClient client, string email) =>
        await client.PostAsync(
            "/api/v1/users",
            new { email, displayName = "Ada", password = Password },
            Token);

    private async Task<ApiClient> RegisteredClientAsync(string email)
    {
        await postgres.ResetAsync(Token);

        var client = postgres.Api.NewApiClient();
        await Register(client, email);

        return client;
    }

    private async Task<ApiClient> SignedInClientAsync(string email)
    {
        var client = await RegisteredClientAsync(email);
        await client.PostAsync("/api/v1/sessions", Credentials(email), Token);

        return client;
    }

    /// <summary>
    /// Lets a second account be created, as registration is closed after the first.
    /// </summary>
    /// <remarks>
    /// Mutates the live singleton rather than the row, as an update does: a row written behind the
    /// process's back is unseen until restart.
    /// </remarks>
    private void OpenRegistration()
    {
        var settings = postgres.Api.Services.GetRequiredService<RegistrationSettings>();

        settings.OpenRegistration = true;
        settings.RequireInvitation = false;
    }

    private static async Task Warm(ApiClient client) =>
        await client.PostAsync(
            "/api/v1/sessions",
            new { email = "warm@example.com", password = Password },
            Token);

    private static async Task<TimeSpan> TimeAsync(Func<Task<ApiResponse>> work)
    {
        var started = Stopwatch.GetTimestamp();

        await work();

        return Stopwatch.GetElapsedTime(started);
    }
}
