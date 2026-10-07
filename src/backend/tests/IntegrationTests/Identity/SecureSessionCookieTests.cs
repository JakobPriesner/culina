using System.Net;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Identity;

/// <summary>
/// The cookies exactly as production writes them.
/// </summary>
/// <remarks>
/// Every other host in this suite runs with <c>Cookies:Secure=false</c>,
/// because the test client speaks plain HTTP, and so sees the development
/// cookie without its <c>__Host-</c> prefix. This one turns Secure on and reads
/// the raw <c>Set-Cookie</c> headers, which is the only place the attributes
/// the browser enforces can be checked.
/// </remarks>
[Collection(RequiresDatabase.Name)]
public class SecureSessionCookieTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";
    private const string SessionCookie = "__Host-culina.session";
    private const string CsrfCookie = "culina.csrf";

    [Fact]
    public async Task SignIn_ShouldIssueAHostPrefixedSessionCookie_ThatOnlyThisOriginCanSetOrRead()
    {
        // Arrange
        using var factory = await SecureFactoryAsync();
        using var client = await RegisteredClientAsync(factory);

        // Act
        var response = await SignInAsync(client);

        // Assert
        // __Host- is refused by the browser unless the cookie is Secure, has
        // Path=/ and no Domain; HttpOnly keeps it from scripts, and Lax keeps
        // it off cross-site POSTs.
        var attributes = Attributes(response, SessionCookie);
        Assert.Contains("secure", attributes);
        Assert.Contains("httponly", attributes);
        Assert.Contains("samesite=lax", attributes);
        Assert.Contains("path=/", attributes);
        Assert.DoesNotContain(attributes, attribute => attribute.StartsWith("domain=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SignIn_ShouldIssueAReadableCsrfCookie_WithTheSameProtections()
    {
        // Arrange
        using var factory = await SecureFactoryAsync();
        using var client = await RegisteredClientAsync(factory);

        // Act
        var response = await SignInAsync(client);

        // Assert
        // Readable, because the client echoes it in a header; otherwise held
        // to the same rules as the session cookie it travels with.
        var attributes = Attributes(response, CsrfCookie);
        Assert.DoesNotContain("httponly", attributes);
        Assert.Contains("secure", attributes);
        Assert.Contains("samesite=lax", attributes);
        Assert.Contains("path=/", attributes);
        Assert.DoesNotContain(attributes, attribute => attribute.StartsWith("domain=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SignOut_ShouldClearTheSessionCookie_WithAttributesTheBrowserWillAccept()
    {
        // Arrange
        using var factory = await SecureFactoryAsync();
        using var client = await RegisteredClientAsync(factory);
        var signedIn = await SignInAsync(client);

        // The test client's jar will not send a Secure cookie over plain
        // HTTP, so this browser's cookies are presented by hand.
        using var browser = factory.NewApiClient();
        var signOut = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/sessions/current");
        signOut.Headers.TryAddWithoutValidation(
            "Cookie",
            $"{SessionCookie}={Value(signedIn, SessionCookie)}; {CsrfCookie}={Value(signedIn, CsrfCookie)}");
        signOut.Headers.TryAddWithoutValidation("X-Culina-CSRF", Value(signedIn, CsrfCookie));

        // Act
        var response = await browser.SendAsync(signOut, Token);

        // Assert
        // A browser ignores a __Host- cookie that is not Secure with Path=/,
        // and that includes the one meant to delete it: the old session
        // cookie would then stay in the jar after signing out.
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var attributes = Attributes(response, SessionCookie);
        Assert.Contains("secure", attributes);
        Assert.Contains("path=/", attributes);
        Assert.Contains(attributes, attribute => attribute.StartsWith("expires=thu, 01 jan 1970", StringComparison.Ordinal));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<CulinaApiFactory> SecureFactoryAsync()
    {
        await postgres.ResetAsync(Token);

        return new CulinaApiFactory(
            postgres,
            new Dictionary<string, string> { ["Cookies:Secure"] = "true" });
    }

    private static async Task<ApiClient> RegisteredClientAsync(CulinaApiFactory factory)
    {
        var client = factory.NewApiClient();

        await client.PostAsync(
            "/api/v1/users",
            new { email = "ada@example.com", displayName = "Ada", password = Password },
            Token);

        return client;
    }

    private static async Task<ApiResponse> SignInAsync(ApiClient client)
    {
        var response = await client.PostAsync(
            "/api/v1/sessions",
            new { email = "ada@example.com", password = Password },
            Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return response;
    }

    /// <summary>
    /// The attributes of one cookie as the server wrote them, lower-cased,
    /// without the name and value.
    /// </summary>
    private static IReadOnlyList<string> Attributes(ApiResponse response, string name) =>
        [.. SetCookie(response, name).Split(';').Skip(1).Select(part => part.Trim().ToLowerInvariant())];

    private static string Value(ApiResponse response, string name) =>
        SetCookie(response, name)[(name.Length + 1)..].Split(';', 2)[0];

    private static string SetCookie(ApiResponse response, string name) =>
        Assert.Single(
            response.Headers.GetValues("Set-Cookie"),
            cookie => cookie.StartsWith($"{name}=", StringComparison.Ordinal));
}
