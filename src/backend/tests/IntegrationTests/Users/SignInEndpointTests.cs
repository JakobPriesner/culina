using System.Diagnostics;
using System.Net;
using Application.Abstractions.Settings;
using IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Users;

/// <summary>
/// Every rejection path, because a sign-in endpoint tested only on its happy
/// path is one nobody knows is safe.
/// </summary>
[Collection(RequiresDatabase.Name)]
public class SignInEndpointTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task SignIn_ShouldStartASession_WhenTheCredentialsAreCorrect()
    {
        // Arrange
        using var client = await RegisteredClientAsync("ada@example.com");

        // Act
        var response = await client.PostAsync("/api/v1/sessions", Credentials("ada@example.com"), Token);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotEmpty(response.Json!.Value.GetProperty("csrfToken").GetString()!);
        Assert.NotNull(client.CsrfToken);
    }

    [Fact]
    public async Task SignIn_ShouldNeverPutTheSessionTokenInTheBody_OnlyInAnHttpOnlyCookie()
    {
        // Arrange
        using var client = await RegisteredClientAsync("ada@example.com");

        // Act
        var response = await client.PostAsync("/api/v1/sessions", Credentials("ada@example.com"), Token);

        // Assert
        var cookies = response.Headers.GetValues("Set-Cookie").ToList();
        var sessionCookie = Assert.Single(cookies, cookie => cookie.StartsWith("culina.session=", StringComparison.Ordinal));
        Assert.Contains("httponly", sessionCookie, StringComparison.OrdinalIgnoreCase);

        var sessionToken = sessionCookie["culina.session=".Length..].Split(';', 2)[0];
        Assert.DoesNotContain(sessionToken, response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SignIn_ShouldMakeTheCsrfCookieReadable_BecauseTheClientMustEchoIt()
    {
        // Arrange
        using var client = await RegisteredClientAsync("ada@example.com");

        // Act
        var response = await client.PostAsync("/api/v1/sessions", Credentials("ada@example.com"), Token);

        // Assert
        var csrfCookie = Assert.Single(
            response.Headers.GetValues("Set-Cookie"),
            cookie => cookie.StartsWith("culina.csrf=", StringComparison.Ordinal));
        Assert.DoesNotContain("httponly", csrfCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SignIn_ShouldSayInvalidCredentials_WhenThePasswordIsWrong()
    {
        // Arrange
        using var client = await RegisteredClientAsync("ada@example.com");

        // Act
        var response = await client.PostAsync(
            "/api/v1/sessions",
            new { email = "ada@example.com", password = "not the password" },
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("auth.invalid_credentials", response.ProblemCode);
    }

    [Fact]
    public async Task SignIn_ShouldAnswerIdentically_WhetherOrNotTheAccountExists()
    {
        // Arrange
        using var client = await RegisteredClientAsync("ada@example.com");

        // Act
        var wrongPassword = await client.PostAsync(
            "/api/v1/sessions",
            new { email = "ada@example.com", password = "not the password" },
            Token);
        var unknownAccount = await client.PostAsync(
            "/api/v1/sessions",
            new { email = "nobody@example.com", password = "not the password" },
            Token);

        // Assert
        // Telling the two apart would turn the sign-in form into a way to
        // discover which addresses are registered.
        Assert.Equal(wrongPassword.StatusCode, unknownAccount.StatusCode);
        Assert.Equal(wrongPassword.ProblemCode, unknownAccount.ProblemCode);
        Assert.Equal(
            wrongPassword.Json!.Value.GetProperty("detail").GetString(),
            unknownAccount.Json!.Value.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task SignIn_ShouldCostTheSame_WhetherOrNotTheAccountExists()
    {
        // Arrange
        using var client = await RegisteredClientAsync("ada@example.com");
        await Warm(client);

        // Act
        var known = await TimeAsync(() => client.PostAsync(
            "/api/v1/sessions",
            new { email = "ada@example.com", password = "not the password" },
            Token));
        var unknown = await TimeAsync(() => client.PostAsync(
            "/api/v1/sessions",
            new { email = "nobody@example.com", password = "not the password" },
            Token));

        // Assert
        // Skipping the hash for an unknown address would make that response
        // dramatically faster and leak which addresses are registered. The
        // bound is loose on purpose — this asserts the work happens, not that
        // the timings match to the microsecond.
        var ratio = known.TotalMilliseconds / Math.Max(unknown.TotalMilliseconds, 1);
        Assert.InRange(ratio, 0.2, 5.0);
    }

    [Fact]
    public async Task SignedInRequest_ShouldBeRejected_WhenItOmitsTheCsrfToken()
    {
        // Arrange
        using var client = await SignedInClientAsync("ada@example.com");

        // Act
        // Cookies but no CSRF header: exactly the shape of a cross-site request
        // riding this browser's cookie jar.
        var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Delete, "/api/v1/sessions/current"),
            Token,
            attachCsrf: false);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("auth.csrf_invalid", response.ProblemCode);
    }

    [Fact]
    public async Task SignOut_ShouldEndTheSession_SoTheCookieStopsWorking()
    {
        // Arrange
        using var client = await SignedInClientAsync("ada@example.com");

        // Act
        var signedOut = await client.DeleteAsync("/api/v1/sessions/current", Token);
        var afterwards = await client.GetAsync("/api/v1/sessions", Token);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, signedOut.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterwards.StatusCode);
    }

    [Fact]
    public async Task Sessions_ShouldListTheCallersDevices_AndMarkTheCurrentOne()
    {
        // Arrange
        using var client = await SignedInClientAsync("ada@example.com");

        // Act
        var response = await client.GetAsync("/api/v1/sessions", Token);

        // Assert
        var items = response.Json!.Value.GetProperty("items").EnumerateArray().ToList();
        Assert.Single(items, item => item.GetProperty("isCurrent").GetBoolean());
    }

    [Fact]
    public async Task Revoke_ShouldNotSeeAnotherUsersSession_EvenWithItsExactId()
    {
        // Arrange
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

        // Act
        var response = await stranger.DeleteAsync($"/api/v1/sessions/{ownerSessionId}", Token);

        // Assert
        // Ownership is part of the SQL WHERE, so another user's session is not
        // refused — it simply does not exist for this caller.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static object Credentials(string email) => new { email, password = Password };

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
    /// Lets a second account be created; registration is closed by default
    /// after the first.
    /// </summary>
    /// <remarks>
    /// Mutates the live singleton rather than writing the row, because that is
    /// exactly what an update does: instance settings are held in memory and a
    /// row written behind the process's back would not be seen until a restart.
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
