using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Users;

/// <summary>
/// The installed apps: same session rows, carried as a bearer token from their own origin
/// (docs/native-app-design.md, part 3). Every browser path must stay exactly as it was.
/// </summary>
[Collection(RequiresDatabase.Name)]
public class NativeAppSessionTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";
    private const string AppOrigin = "capacitor://localhost";

    [Fact]
    public async Task SignIn_ShouldReturnTheTokenInTheBody_AndSetNoCookie_ForAnApp()
    {
        await RegisterAsync();
        using var app = postgres.Api.CreateDefaultClient();

        using var response = await app.PostAsJsonAsync(
            "/api/v1/sessions",
            new { email = "ada@example.com", password = Password, client = "ios" },
            Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Token);
        Assert.NotEmpty(body.GetProperty("sessionToken").GetString()!);
    }

    [Fact]
    public async Task SignIn_ShouldKeepTheTokenOutOfTheBody_ForABrowser()
    {
        using var browser = await RegisterAsync();

        var response = await browser.PostAsync(
            "/api/v1/sessions",
            new { email = "ada@example.com", password = Password, client = "browser" },
            Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(JsonValueKind.Null, response.Json!.Value.GetProperty("sessionToken").ValueKind);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), cookie => cookie.StartsWith("culina.session=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SignIn_ShouldRefuseAClientCulinaDoesNotHave()
    {
        using var browser = await RegisterAsync();

        var response = await browser.PostAsync(
            "/api/v1/sessions",
            new { email = "ada@example.com", password = Password, client = "watch" },
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BearerToken_ShouldAuthenticate_AndNeedNoCsrfToken()
    {
        await RegisterAsync();
        using var app = await SignedInAppAsync("android");

        using var me = await app.GetAsync(new Uri("/api/v1/users/me", UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        // An unsafe request with no CSRF header, no cookie and no Referer: the shape of every app call.
        using var signOut = await app.DeleteAsync(new Uri("/api/v1/sessions/current", UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.NoContent, signOut.StatusCode);

        using var afterwards = await app.GetAsync(new Uri("/api/v1/users/me", UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.Unauthorized, afterwards.StatusCode);
    }

    [Fact]
    public async Task CookieRequest_ShouldStillNeedTheCsrfToken_EvenWithABearerHeaderBesideIt()
    {
        using var browser = await RegisterAsync();
        await browser.PostAsync("/api/v1/sessions", new { email = "ada@example.com", password = Password }, Token);
        using var app = await SignedInAppAsync("ios");
        var appToken = app.DefaultRequestHeaders.Authorization!.Parameter;

        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/sessions/current");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", appToken);

        var response = await browser.SendAsync(request, Token, attachCsrf: false);

        // The cookie wins, so a page that can make the browser send a header cannot skip the token.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("auth.csrf_invalid", response.ProblemCode);
    }

    [Fact]
    public async Task Sessions_ShouldSayWhichKindOfDeviceEachIs()
    {
        using var browser = await RegisterAsync();
        await browser.PostAsync("/api/v1/sessions", new { email = "ada@example.com", password = Password }, Token);
        using var app = await SignedInAppAsync("ios");

        var devices = await browser.GetAsync("/api/v1/sessions", Token);

        var clients = devices.Json!.Value.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("client").GetString())
            .Order(StringComparer.Ordinal)
            .ToList();
        Assert.Equal(["browser", "ios"], clients);
    }

    [Fact]
    public async Task Preflight_ShouldBeAnswered_ForTheAppOrigin()
    {
        await postgres.ResetAsync(Token);
        using var app = postgres.Api.CreateDefaultClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/users/me");
        request.Headers.Add("Origin", AppOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "authorization, if-none-match");

        using var response = await app.SendAsync(request, Token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(AppOrigin, Single(response, "Access-Control-Allow-Origin"));
        Assert.Contains("Authorization", Single(response, "Access-Control-Allow-Headers"), StringComparison.Ordinal);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task Preflight_ShouldGetNoCorsHeaders_FromAnyOtherOrigin()
    {
        await postgres.ResetAsync(Token);
        using var app = postgres.Api.CreateDefaultClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/users/me");
        request.Headers.Add("Origin", "https://evil.example");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        using var response = await app.SendAsync(request, Token);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task AppResponses_ShouldExposeTheirValidators_ToTheAppOrigin()
    {
        await RegisterAsync();
        using var app = await SignedInAppAsync("ios");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/me");
        request.Headers.Add("Origin", AppOrigin);

        using var response = await app.SendAsync(request, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(AppOrigin, Single(response, "Access-Control-Allow-Origin"));
        // The ETag cache reads it; cross-origin it is invisible unless exposed.
        Assert.Contains("ETag", Single(response, "Access-Control-Expose-Headers"), StringComparison.Ordinal);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task Setup_ShouldSayWhichApiGenerationItSpeaks()
    {
        await postgres.ResetAsync(Token);
        using var app = postgres.Api.CreateDefaultClient();

        var setup = await app.GetFromJsonAsync<JsonElement>("/api/v1/setup", Token);

        Assert.Equal(Contracts.ApiContract.Version, setup.GetProperty("apiVersion").GetInt32());
    }

    private async Task<ApiClient> RegisterAsync()
    {
        await postgres.ResetAsync(Token);

        var browser = postgres.Api.NewApiClient();
        await browser.PostAsync(
            "/api/v1/users",
            new { email = "ada@example.com", displayName = "Ada", password = Password },
            Token);

        return browser;
    }

    private async Task<HttpClient> SignedInAppAsync(string client)
    {
        var app = postgres.Api.CreateDefaultClient();
        using var response = await app.PostAsJsonAsync(
            "/api/v1/sessions",
            new { email = "ada@example.com", password = Password, client },
            Token);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Token);

        app.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("sessionToken").GetString());

        return app;
    }

    private static string Single(HttpResponseMessage response, string header) =>
        Assert.Single(response.Headers.GetValues(header));

    private static CancellationToken Token => TestContext.Current.CancellationToken;
}
