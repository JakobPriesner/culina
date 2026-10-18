using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Api.Agents;
using IntegrationTests.Fixtures;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace IntegrationTests.Agents;

/// <summary>
/// The MCP server at /api/mcp, driven by the SDK's own client as an agent would: an agent's token opens it and its
/// tools, and nothing else; nothing else opens it.
/// </summary>
[Collection(RequiresDatabase.Name)]
public class AgentServerTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";
    private const string JsonRpcListTools = """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""";

    [Fact]
    public async Task ListTools_ShouldOfferExactlyTheAgentOperations()
    {
        // Arrange
        await Kitchen.OpenAsync(postgres);
        await using var agent = await ConnectAgentAsync();

        // Act
        var tools = await agent.ListToolsAsync(cancellationToken: Token);

        // Assert
        Assert.Equal(AgentOperations.Allowed.Order(StringComparer.Ordinal), tools.Select(tool => tool.Name).Order(StringComparer.Ordinal));
        var update = tools.Single(tool => tool.Name == "updateRecipeV1").ProtocolTool;
        Assert.Contains("ifMatch", update.InputSchema.GetProperty("required").EnumerateArray().Select(name => name.GetString()));
    }

    [Fact]
    public async Task CallTool_ShouldCreateReadAndChangeARecipe_AsTheAgent()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        await using var agent = await ConnectAgentAsync();

        // Act
        var created = await CallAsync(agent, "createRecipeV1", new { body = new { householdId = kitchen.HouseholdId, title = "Dal" } });
        var recipeId = created.GetProperty("body").GetProperty("recipeId").GetString()!;
        var read = await CallAsync(agent, "getRecipeByIdV1", new { recipeId });
        var unconditional = await CallAsync(agent, "updateRecipeV1", new { recipeId, body = Recipe("Red lentil dal") });
        var updated = await CallAsync(agent, "updateRecipeV1", new
        {
            recipeId,
            ifMatch = read.GetProperty("etag").GetString(),
            body = Recipe("Red lentil dal")
        });

        // Assert
        Assert.Equal(201, created.GetProperty("status").GetInt32());
        Assert.Equal("Dal", read.GetProperty("body").GetProperty("title").GetString());
        Assert.Equal(428, unconditional.GetProperty("status").GetInt32());
        Assert.Equal(200, updated.GetProperty("status").GetInt32());
        var seen = await kitchen.Client.GetAsync($"/api/v1/recipes/{recipeId}", Token);
        Assert.Equal("Red lentil dal", seen.Json!.Value.GetProperty("title").GetString());
    }

    [Theory]
    [InlineData("../../sessions")]
    [InlineData("x/../../users/me")]
    [InlineData("%2e%2e")]
    [InlineData("..")]
    public async Task CallTool_ShouldRefuseAPathArgument_WhenItWouldLeaveItsSegment(string recipeId)
    {
        // Arrange
        await Kitchen.OpenAsync(postgres);
        await using var agent = await ConnectAgentAsync();

        // Act
        var result = await agent.CallToolAsync("getRecipeByIdV1", Arguments(new { recipeId }), cancellationToken: Token);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal("agent.invalid_argument", Answer(result).GetProperty("body").GetProperty("code").GetString());
    }

    [Fact]
    public async Task CallTool_ShouldRefuseAnOperation_WhenItIsNotOffered()
    {
        // Arrange
        await Kitchen.OpenAsync(postgres);
        await using var agent = await ConnectAgentAsync();

        // Act
        var result = await agent.CallToolAsync("getSessionsV1", Arguments(new { }), cancellationToken: Token);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal("agent.unknown_tool", Answer(result).GetProperty("body").GetProperty("code").GetString());
    }

    [Fact]
    public async Task AgentToken_ShouldReachTheAgentOperations_AndNothingElse()
    {
        // Arrange
        await Kitchen.OpenAsync(postgres);
        using var agent = await AgentHttpClientAsync();

        // Act
        using var households = await agent.GetAsync(new Uri("/api/v1/households", UriKind.Relative), Token);
        using var sessions = await agent.GetAsync(new Uri("/api/v1/sessions", UriKind.Relative), Token);
        using var password = await agent.PutAsJsonAsync(
            "/api/v1/users/me/password",
            new { currentPassword = Password, newPassword = "a new sentence nobody would pick" },
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, households.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, sessions.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, password.StatusCode);
    }

    [Fact]
    public async Task McpEndpoint_ShouldRefuseAnonymousRequests()
    {
        // Arrange
        await postgres.ResetAsync(Token);
        using var anonymous = postgres.Api.CreateDefaultClient();

        // Act
        using var response = await anonymous.SendAsync(ListToolsRequest(), Token);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task McpEndpoint_ShouldRefuseABrowsersCookie()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);

        // Act: the cookie, the CSRF token and our own origin, everything a page could make a browser send.
        var response = await kitchen.Client.SendAsync(ListToolsRequest(), Token);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task McpEndpoint_ShouldRefuseAnAppsToken()
    {
        // Arrange
        await Kitchen.OpenAsync(postgres);
        using var app = await SignedInAsync("ios");

        // Act
        using var response = await app.SendAsync(ListToolsRequest(), Token);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SignIn_ShouldKeepTheBrowserSignedIn_WhenItConnectsAnAgent()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);

        // Act
        var connected = await kitchen.Client.PostAsync(
            "/api/v1/sessions",
            new { email = "ada@example.com", password = Password, client = "agent" },
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.Created, connected.StatusCode);
        Assert.NotEmpty(connected.Json!.Value.GetProperty("sessionToken").GetString()!);
        var devices = await kitchen.Client.GetAsync("/api/v1/sessions", Token);
        Assert.Equal(HttpStatusCode.OK, devices.StatusCode);
        Assert.Equal(
            ["agent", "browser"],
            devices.Json!.Value.GetProperty("items").EnumerateArray()
                .Select(item => item.GetProperty("client").GetString())
                .Order(StringComparer.Ordinal));
    }

    private async Task<McpClient> ConnectAgentAsync()
    {
        var http = await AgentHttpClientAsync();
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

    private Task<HttpClient> AgentHttpClientAsync() => SignedInAsync("agent");

    private async Task<HttpClient> SignedInAsync(string client)
    {
        var http = postgres.Api.CreateDefaultClient();
        using var response = await http.PostAsJsonAsync(
            "/api/v1/sessions",
            new { email = "ada@example.com", password = Password, client },
            Token);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Token);

        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("sessionToken").GetString());

        return http;
    }

    private static async Task<JsonElement> CallAsync(McpClient agent, string tool, object arguments) =>
        Answer(await agent.CallToolAsync(tool, Arguments(arguments), cancellationToken: Token));

    private static Dictionary<string, object?> Arguments(object arguments) =>
        JsonSerializer.SerializeToElement(arguments).EnumerateObject()
            .ToDictionary(property => property.Name, property => (object?)property.Value.Clone(), StringComparer.Ordinal);

    private static JsonElement Answer(CallToolResult result) =>
        JsonDocument.Parse(Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text).RootElement.Clone();

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

    private static object Recipe(string title) => new
    {
        title,
        language = "en",
        yieldAmount = 4,
        yieldKind = "servings",
        prepMinutes = 10,
        cookMinutes = 30,
        groups = new[] { new { name = (string?)null, ingredients = new[] { new { name = "Rote Linsen", unit = "g" } } } },
        steps = new[] { new { segments = new[] { new { type = "text", value = "Simmer." } } } },
        tags = Array.Empty<string>()
    };

    private static CancellationToken Token => TestContext.Current.CancellationToken;
}
