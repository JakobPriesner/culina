using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Api.Agents;
using Application.Abstractions;
using Domain.Shared;
using IntegrationTests.Fixtures;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;

namespace IntegrationTests.Agents;

/// <summary>
/// OAuth 2.1 for the MCP server, driven as Claude's or ChatGPT's connector drives it: discovery from the 401, client
/// registration, the authorization endpoint, the consent screen's API in a signed-in browser, the token endpoint, and
/// the token on /api/mcp. Then every way the flow must refuse.
/// </summary>
[Collection(RequiresDatabase.Name)]
public class AgentAuthorizationTests(PostgresFixture postgres)
{
    private const string Issuer = "http://localhost";
    private const string Resource = "http://localhost/api/mcp";
    private const string Callback = "https://claude.ai/api/mcp/auth_callback";

    // RFC 7636, appendix B.
    private const string Verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
    private const string Challenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

    private const string JsonRpcListTools = """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""";

    private static readonly string[] GrantTypes = ["authorization_code", "refresh_token"];
    private static readonly string[] ResponseTypes = ["code"];

    [Fact]
    public async Task Discovery_ShouldLeadFromTheMcpServersChallengeToTheAuthorizationServer()
    {
        // Arrange
        await postgres.ResetAsync(Token);
        using var anonymous = postgres.Api.CreateDefaultClient();

        // Act
        using var challenged = await anonymous.SendAsync(ListToolsRequest(), Token);
        var resourceMetadataUrl = ResourceMetadataUrl(challenged);
        var resource = await anonymous.GetFromJsonAsync<JsonElement>(resourceMetadataUrl, Token);
        var server = await anonymous.GetFromJsonAsync<JsonElement>(
            $"{resource.GetProperty("authorization_servers")[0].GetString()}/.well-known/oauth-authorization-server", Token);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, challenged.StatusCode);
        Assert.Equal($"{Issuer}/.well-known/oauth-protected-resource/api/mcp", resourceMetadataUrl);
        Assert.Equal(Resource, resource.GetProperty("resource").GetString());
        Assert.Equal(Issuer, server.GetProperty("issuer").GetString());
        Assert.Equal($"{Issuer}/oauth/authorize", server.GetProperty("authorization_endpoint").GetString());
        Assert.Equal($"{Issuer}/oauth/token", server.GetProperty("token_endpoint").GetString());
        Assert.Equal($"{Issuer}/oauth/register", server.GetProperty("registration_endpoint").GetString());
        Assert.Equal(["S256"], Strings(server, "code_challenge_methods_supported"));
        Assert.Equal(["none"], Strings(server, "token_endpoint_auth_methods_supported"));
        Assert.True(server.GetProperty("client_id_metadata_document_supported").GetBoolean());
        Assert.True(server.GetProperty("authorization_response_iss_parameter_supported").GetBoolean());
    }

    [Fact]
    public async Task Flow_ShouldGiveTheAppAnAgentSession_ThatOpensTheMcpServerAndIsRevokedLikeAnyOther()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var clientId = await RegisterAsync(Callback);

        // Act
        var consent = await AuthorizeAsync(clientId);
        var shown = await kitchen.Client.GetAsync($"/api/v1/agent-authorizations/{RequestId(consent)}", Token);
        var back = await DecideAsync(kitchen, RequestId(consent), approved: true);
        using var token = await RedeemAsync(clientId, Parameter(back, "code"));
        var accessToken = (await token.Content.ReadFromJsonAsync<JsonElement>(Token)).GetProperty("access_token").GetString()!;
        await using var agent = await ConnectAsync(accessToken);
        var tools = await agent.ListToolsAsync(cancellationToken: Token);
        var devices = await kitchen.Client.GetAsync("/api/v1/sessions", Token);
        var agentSession = devices.Json!.Value.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("client").GetString() == "agent");
        await kitchen.Client.DeleteAsync($"/api/v1/sessions/{agentSession.GetProperty("sessionId").GetGuid()}", Token);
        using var afterRevoking = await BearerClient(accessToken).SendAsync(ListToolsRequest(), Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, shown.StatusCode);
        Assert.Equal("Claude", shown.Json!.Value.GetProperty("clientName").GetString());
        Assert.Equal("claude.ai", shown.Json!.Value.GetProperty("redirectHost").GetString());
        Assert.False(shown.Json!.Value.GetProperty("redirectsToThisComputer").GetBoolean());
        Assert.Equal(JsonValueKind.Null, shown.Json!.Value.GetProperty("verifiedHost").ValueKind);
        Assert.StartsWith($"{Callback}?", back.ToString(), StringComparison.Ordinal);
        Assert.Equal("xyz", Parameter(back, "state"));
        Assert.Equal(Issuer, Parameter(back, "iss"));
        Assert.Equal(HttpStatusCode.OK, token.StatusCode);
        Assert.Equal("no-store", token.Headers.CacheControl?.ToString());
        Assert.Equal(AgentOperations.Allowed.Count, tools.Count);
        Assert.Equal(HttpStatusCode.Unauthorized, afterRevoking.StatusCode);
    }

    [Fact]
    public async Task Flow_ShouldUseTheAppsMetadataDocument_WhenItsClientIdIsAnAddress()
    {
        // Arrange
        const string ClientId = "https://claude.ai/oauth/claude-code-client-metadata";
        var documents = new MetadataDocuments(new Dictionary<string, ClientMetadataDocument>
        {
            [ClientId] = new(ClientId, "Claude Code", ["http://localhost/callback", "http://127.0.0.1/callback"]),
            ["https://evil.example/client.json"] = new(ClientId, "Claude Code", ["http://localhost/callback"])
        });
        await postgres.ResetAsync(Token);
        using var api = new CulinaApiFactory(
            postgres,
            replace: services => services.AddSingleton<IClientMetadataDocuments>(documents));
        var browser = await SignedInAsync(api);

        // Act
        var consent = await AuthorizeAsync(ClientId, "http://localhost:3118/callback", api: api);
        var shown = await browser.GetAsync($"/api/v1/agent-authorizations/{RequestId(consent)}", Token);
        var back = await DecideAsync(browser, RequestId(consent), approved: true);
        using var token = await RedeemAsync(ClientId, Parameter(back, "code"), "http://localhost:3118/callback", api: api);
        var impostor = await AuthorizeAsync("https://evil.example/client.json", "http://localhost:3118/callback", api: api);

        // Assert
        Assert.Equal("Claude Code", shown.Json!.Value.GetProperty("clientName").GetString());
        Assert.Equal("claude.ai", shown.Json!.Value.GetProperty("verifiedHost").GetString());
        Assert.Equal("localhost", shown.Json!.Value.GetProperty("redirectHost").GetString());
        Assert.True(shown.Json!.Value.GetProperty("redirectsToThisComputer").GetBoolean());
        Assert.StartsWith("http://localhost:3118/callback?code=", back.ToString(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, token.StatusCode);
        Assert.Equal("/authorize?problem=oauth.invalid_client", impostor.OriginalString);
    }

    [Fact]
    public async Task Token_ShouldRefuseAWrongPkceVerifier_AndStillTakeTheRightOne()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var clientId = await RegisterAsync(Callback);
        var code = await ApprovedCodeAsync(kitchen, clientId);

        // Act
        using var wrong = await RedeemAsync(clientId, code, verifier: "a-verifier-that-is-long-enough-but-is-not-the-right-one-at-all");
        using var right = await RedeemAsync(clientId, code);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Equal("invalid_grant", await OAuthErrorAsync(wrong));
        Assert.Equal(HttpStatusCode.OK, right.StatusCode);
    }

    [Fact]
    public async Task Token_ShouldRefuseAReusedCode_AndEndTheSessionItBecame()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var clientId = await RegisterAsync(Callback);
        var code = await ApprovedCodeAsync(kitchen, clientId);
        using var first = await RedeemAsync(clientId, code);
        var accessToken = (await first.Content.ReadFromJsonAsync<JsonElement>(Token)).GetProperty("access_token").GetString()!;

        // Act
        using var second = await RedeemAsync(clientId, code);
        using var mcp = await BearerClient(accessToken).SendAsync(ListToolsRequest(), Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("invalid_grant", await OAuthErrorAsync(second));
        Assert.Equal(HttpStatusCode.Unauthorized, mcp.StatusCode);
    }

    [Fact]
    public async Task Token_ShouldRefuseAnExpiredCode()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var clientId = await RegisterAsync(Callback);
        var code = await ApprovedCodeAsync(kitchen, clientId);
        await postgres.ExecuteAsync("update agent_authorizations set expires_at = now() - interval '1 second';", Token);

        // Act
        using var expired = await RedeemAsync(clientId, code);

        // Assert
        Assert.Equal("invalid_grant", await OAuthErrorAsync(expired));
    }

    [Fact]
    public async Task Token_ShouldRefuse_WhenTheRedirectUriDiffersFromTheOneTheCodeWasSentTo()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var clientId = await RegisterAsync(Callback);
        var code = await ApprovedCodeAsync(kitchen, clientId);

        // Act
        using var redeemed = await RedeemAsync(clientId, code, redirectUri: "https://claude.ai/elsewhere");

        // Assert
        Assert.Equal("invalid_grant", await OAuthErrorAsync(redeemed));
    }

    [Fact]
    public async Task Authorize_ShouldNotRedirect_WhenTheRedirectUriWasNotRegistered()
    {
        // Arrange
        await postgres.ResetAsync(Token);
        var clientId = await RegisterAsync(Callback);

        // Act
        var location = await AuthorizeAsync(clientId, "https://evil.example/steal");

        // Assert
        Assert.Equal("/authorize?problem=oauth.invalid_redirect_uri", location.OriginalString);
    }

    [Theory]
    [InlineData("http://example.com/callback")]
    [InlineData("cursor://anysphere.cursor-mcp/oauth/callback")]
    [InlineData("https://example.com/callback#fragment")]
    public async Task Register_ShouldRefuseARedirectUri_ThatIsNeitherHttpsNorLoopback(string redirectUri)
    {
        // Arrange
        await postgres.ResetAsync(Token);
        using var anonymous = postgres.Api.CreateDefaultClient();

        // Act
        using var response = await anonymous.PostAsJsonAsync(
            "/oauth/register",
            new Dictionary<string, object> { ["redirect_uris"] = new[] { redirectUri }, ["client_name"] = "Mallory" },
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_redirect_uri", await OAuthErrorAsync(response));
    }

    [Theory]
    [InlineData("token", "S256", "unsupported_response_type")]
    [InlineData("code", "plain", "invalid_request")]
    public async Task Authorize_ShouldSendTheErrorBackToTheApp_WhenItAsksForSomethingCulinaDoesNotDo(
        string responseType,
        string method,
        string expected)
    {
        // Arrange
        await postgres.ResetAsync(Token);
        var clientId = await RegisterAsync(Callback);

        // Act
        var back = await AuthorizeAsync(clientId, responseType: responseType, method: method);

        // Assert
        Assert.StartsWith($"{Callback}?", back.ToString(), StringComparison.Ordinal);
        Assert.Equal(expected, Parameter(back, "error"));
        Assert.Equal("xyz", Parameter(back, "state"));
        Assert.Equal(Issuer, Parameter(back, "iss"));
    }

    [Fact]
    public async Task Decide_ShouldSendAccessDeniedBack_WhenThePersonSaysNo()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var clientId = await RegisterAsync(Callback);
        var consent = await AuthorizeAsync(clientId);

        // Act
        var back = await DecideAsync(kitchen, RequestId(consent), approved: false);
        var again = await kitchen.Client.GetAsync($"/api/v1/agent-authorizations/{RequestId(consent)}", Token);

        // Assert
        Assert.Equal("access_denied", Parameter(back, "error"));
        Assert.Equal(Issuer, Parameter(back, "iss"));
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
    }

    [Fact]
    public async Task Decide_ShouldRefuseAnApproval_FromAForeignOriginOrWithoutTheCsrfToken()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var clientId = await RegisterAsync(Callback);
        var authorizationId = RequestId(await AuthorizeAsync(clientId));

        // Act: a page elsewhere making the signed-in browser send the approval, with and without the token.
        var foreign = Approval(authorizationId);
        foreign.Headers.TryAddWithoutValidation("Origin", "https://evil.example");
        var fromElsewhere = await kitchen.Client.SendAsync(foreign, Token);
        var withoutToken = await kitchen.Client.SendAsync(Approval(authorizationId), Token, attachCsrf: false);
        var stillPending = await kitchen.Client.GetAsync($"/api/v1/agent-authorizations/{authorizationId}", Token);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, fromElsewhere.StatusCode);
        Assert.Equal("auth.foreign_origin", fromElsewhere.ProblemCode);
        Assert.Equal(HttpStatusCode.Forbidden, withoutToken.StatusCode);
        Assert.Equal("auth.csrf_invalid", withoutToken.ProblemCode);
        Assert.Equal(HttpStatusCode.OK, stillPending.StatusCode);
    }

    [Fact]
    public async Task Decide_ShouldRefuseAnAgentsOwnToken()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var clientId = await RegisterAsync(Callback);
        using var token = await RedeemAsync(clientId, await ApprovedCodeAsync(kitchen, clientId));
        var accessToken = (await token.Content.ReadFromJsonAsync<JsonElement>(Token)).GetProperty("access_token").GetString()!;
        var another = RequestId(await AuthorizeAsync(clientId));

        // Act: an agent approving more agents for itself.
        using var response = await BearerClient(accessToken).PutAsJsonAsync(
            $"/api/v1/agent-authorizations/{another}/decision", new { approved = true }, Token);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Authorize_ShouldRefuseAResource_ThatIsNotThisMcpServer()
    {
        // Arrange
        await postgres.ResetAsync(Token);
        var clientId = await RegisterAsync(Callback);

        // Act
        var back = await AuthorizeAsync(clientId, resource: "https://elsewhere.example/api/mcp");

        // Assert
        Assert.Equal("invalid_target", Parameter(back, "error"));
    }

    [Fact]
    public async Task Token_ShouldRefuseAResource_ThatIsNotThisMcpServer()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var clientId = await RegisterAsync(Callback);
        var code = await ApprovedCodeAsync(kitchen, clientId);

        // Act
        using var redeemed = await RedeemAsync(clientId, code, resource: "https://elsewhere.example/api/mcp");

        // Assert
        Assert.Equal("invalid_target", await OAuthErrorAsync(redeemed));
    }

    [Fact]
    public async Task McpEndpoint_ShouldRefuseAToken_WhoseAudienceIsNotTheMcpServer()
    {
        // Arrange: an app's session, whose token is for the REST API.
        await Kitchen.OpenAsync(postgres);
        using var app = postgres.Api.CreateDefaultClient();
        using var signedIn = await app.PostAsJsonAsync(
            "/api/v1/sessions",
            new { email = "ada@example.com", password = "correct horse battery staple", client = "ios" },
            Token);
        var appToken = (await signedIn.Content.ReadFromJsonAsync<JsonElement>(Token)).GetProperty("sessionToken").GetString()!;

        // Act
        using var response = await BearerClient(appToken).SendAsync(ListToolsRequest(), Token);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal($"{Issuer}/.well-known/oauth-protected-resource/api/mcp", ResourceMetadataUrl(response));
    }

    private async Task<string> RegisterAsync(string redirectUri)
    {
        using var anonymous = postgres.Api.CreateDefaultClient();

        // What Claude sends, refresh_token included: Culina registers it for what it supports.
        using var response = await anonymous.PostAsJsonAsync(
            "/oauth/register",
            new Dictionary<string, object>
            {
                ["redirect_uris"] = new[] { redirectUri },
                ["client_name"] = "Claude",
                ["grant_types"] = GrantTypes,
                ["response_types"] = ResponseTypes,
                ["token_endpoint_auth_method"] = "none"
            },
            Token);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("none", body.GetProperty("token_endpoint_auth_method").GetString());

        return body.GetProperty("client_id").GetString()!;
    }

    /// <summary>Opens the authorization endpoint as the app sends the browser there; answers where it redirects.</summary>
    private async Task<Uri> AuthorizeAsync(
        string clientId,
        string redirectUri = Callback,
        string? resource = Resource,
        string responseType = "code",
        string method = "S256",
        CulinaApiFactory? api = null)
    {
        var query = new Dictionary<string, string?>
        {
            ["response_type"] = responseType,
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["code_challenge"] = Challenge,
            ["code_challenge_method"] = method,
            ["state"] = "xyz",
            ["scope"] = "mcp offline_access claudeai",
            ["resource"] = resource
        };
        using var browser = (api ?? postgres.Api).CreateDefaultClient();

        using var response = await browser.GetAsync(
            new Uri(QueryHelpers.AddQueryString("/oauth/authorize", query), UriKind.Relative), Token);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        return response.Headers.Location!;
    }

    private static async Task<Uri> DecideAsync(Kitchen kitchen, Guid authorizationId, bool approved) =>
        await DecideAsync(kitchen.Client, authorizationId, approved);

    private static async Task<Uri> DecideAsync(ApiClient browser, Guid authorizationId, bool approved)
    {
        var answered = await browser.PutAsync(
            $"/api/v1/agent-authorizations/{authorizationId}/decision", new { approved }, Token);

        Assert.Equal(HttpStatusCode.OK, answered.StatusCode);

        return new Uri(answered.Json!.Value.GetProperty("redirectUri").GetString()!);
    }

    private async Task<string> ApprovedCodeAsync(Kitchen kitchen, string clientId) =>
        Parameter(await DecideAsync(kitchen, RequestId(await AuthorizeAsync(clientId)), approved: true), "code");

    private async Task<HttpResponseMessage> RedeemAsync(
        string clientId,
        string code,
        string redirectUri = Callback,
        string verifier = Verifier,
        string resource = Resource,
        CulinaApiFactory? api = null)
    {
        using var app = (api ?? postgres.Api).CreateDefaultClient();
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = clientId,
            ["code_verifier"] = verifier,
            ["resource"] = resource
        });

        return await app.PostAsync(new Uri("/oauth/token", UriKind.Relative), form, Token);
    }

    private static async Task<ApiClient> SignedInAsync(CulinaApiFactory api)
    {
        var browser = api.NewApiClient();
        await browser.PostAsync(
            "/api/v1/users",
            new { email = "ada@example.com", displayName = "Ada", password = "correct horse battery staple" },
            Token);
        await browser.PostAsync(
            "/api/v1/sessions",
            new { email = "ada@example.com", password = "correct horse battery staple" },
            Token);

        return browser;
    }

    private async Task<McpClient> ConnectAsync(string accessToken)
    {
        var http = BearerClient(accessToken);
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(http.BaseAddress!, AgentServerExtensions.Path),
                TransportMode = HttpTransportMode.StreamableHttp
            },
            http,
            ownsHttpClient: true);

        return await McpClient.CreateAsync(transport, cancellationToken: Token);
    }

    private HttpClient BearerClient(string token)
    {
        var http = postgres.Api.CreateDefaultClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return http;
    }

    private static HttpRequestMessage Approval(Guid authorizationId) =>
        new(HttpMethod.Put, $"/api/v1/agent-authorizations/{authorizationId}/decision")
        {
            Content = JsonContent.Create(new { approved = true })
        };

    private static Guid RequestId(Uri consent) =>
        Guid.Parse(Parameter(consent, "request"));

    /// <summary>One query parameter of an absolute or relative address.</summary>
    private static string Parameter(Uri address, string name)
    {
        var query = address.IsAbsoluteUri ? address.Query : address.OriginalString[address.OriginalString.IndexOf('?', StringComparison.Ordinal)..];

        return QueryHelpers.ParseQuery(query)[name].Single()!;
    }

    private static string ResourceMetadataUrl(HttpResponseMessage response)
    {
        var challenge = Assert.Single(response.Headers.WwwAuthenticate);

        Assert.Equal("Bearer", challenge.Scheme);

        const string Prefix = "resource_metadata=\"";
        var start = challenge.Parameter!.IndexOf(Prefix, StringComparison.Ordinal) + Prefix.Length;

        return challenge.Parameter[start..challenge.Parameter.IndexOf('"', start)];
    }

    private static async Task<string?> OAuthErrorAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Token)).GetProperty("error").GetString();

    private static string[] Strings(JsonElement element, string name) =>
        [.. element.GetProperty(name).EnumerateArray().Select(item => item.GetString()!)];

    private static HttpRequestMessage ListToolsRequest()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, AgentServerExtensions.Path)
        {
            Content = new StringContent(JsonRpcListTools, Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");

        return request;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Client ID Metadata Documents served from memory: no test reaches a real app's server.</summary>
    private sealed class MetadataDocuments(IReadOnlyDictionary<string, ClientMetadataDocument> documents)
        : IClientMetadataDocuments
    {
        public Task<Result<ClientMetadataDocument>> FetchAsync(Uri address, CancellationToken cancellationToken) =>
            Task.FromResult(documents.TryGetValue(address.OriginalString, out var document)
                ? Result<ClientMetadataDocument>.Success(document)
                : Result<ClientMetadataDocument>.Failure(
                    Domain.AgentAuthorizations.AgentAuthorizationErrors.InvalidClient("No such document.")));
    }
}
