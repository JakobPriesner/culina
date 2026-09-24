using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Settings;

[Collection(RequiresDatabase.Name)]
public class ServerSettingsEndpointTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task Read_ShouldReportWhatTheProcessRunsWith_AndWhatTheEnvironmentPins()
    {
        // Arrange
        using var factory = new CulinaApiFactory(postgres);
        using var admin = await AdminAsync(factory);

        // Act
        var response = await admin.GetAsync("/api/v1/settings/server", Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = response.Json!.Value;
        Assert.Equal(120, body.GetProperty("rateLimits").GetProperty("sharedRecipesPerIpPerMinute").GetInt32());
        Assert.Equal("grpc", body.GetProperty("telemetry").GetProperty("otlpProtocol").GetString());
        Assert.True(body.GetProperty("writable").GetBoolean());

        // The test host sets these the way a deployment would, above the file.
        var pinned = body.GetProperty("pinned").EnumerateArray().Select(entry => entry.GetString()).ToList();
        Assert.Contains("Cookies__Secure", pinned);
        Assert.Contains("RateLimits__LoginPerIpPerMinute", pinned);
        Assert.DoesNotContain("RateLimits__SharedRecipesPerIpPerMinute", pinned);
    }

    [Fact]
    public async Task Update_ShouldSaveTheChangeAndAskForARestart_WhenAValueDiffers()
    {
        // Arrange
        using var factory = new CulinaApiFactory(postgres);
        using var admin = await AdminAsync(factory);
        var proposal = await ProposalAsync(admin);
        proposal["rateLimits"]!["sharedRecipesPerIpPerMinute"] = 200;
        proposal["telemetry"]!["otlpEndpoint"] = "http://collector:4318";
        proposal["telemetry"]!["otlpProtocol"] = "http_protobuf";

        // Act
        var response = await admin.PutAsync("/api/v1/settings/server", proposal, Token);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(1, factory.Restarts.Scheduled);

        var saved = JsonNode.Parse(await File.ReadAllTextAsync(factory.ServerSettingsFile, Token))!;
        Assert.Equal("200", saved["RateLimits"]!["SharedRecipesPerIpPerMinute"]!.GetValue<string>());
        Assert.Equal("http://collector:4318", saved["OTEL_EXPORTER_OTLP_ENDPOINT"]!.GetValue<string>());
        Assert.Equal("http/protobuf", saved["OTEL_EXPORTER_OTLP_PROTOCOL"]!.GetValue<string>());
    }

    [Fact]
    public async Task Update_ShouldChangeNothing_WhenTheFormComesBackAsItWasRead()
    {
        // Arrange
        using var factory = new CulinaApiFactory(postgres);
        using var admin = await AdminAsync(factory);
        var proposal = await ProposalAsync(admin);

        // Act
        var response = await admin.PutAsync("/api/v1/settings/server", proposal, Token);

        // Assert
        // Saving without an edit must not take the server away for a moment.
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, factory.Restarts.Scheduled);
        Assert.False(File.Exists(factory.ServerSettingsFile));
    }

    [Fact]
    public async Task Update_ShouldNotSaveAPinnedValue_BecauseTheEnvironmentWouldOverrideIt()
    {
        // Arrange
        using var factory = new CulinaApiFactory(postgres);
        using var admin = await AdminAsync(factory);
        var proposal = await ProposalAsync(admin);
        proposal["cookies"]!["secure"] = true;
        proposal["rateLimits"]!["importsPerHour"] = 31;

        // Act
        await admin.PutAsync("/api/v1/settings/server", proposal, Token);

        // Assert
        var saved = JsonNode.Parse(await File.ReadAllTextAsync(factory.ServerSettingsFile, Token))!;
        Assert.Null(saved["Cookies"]);
        Assert.Equal("31", saved["RateLimits"]!["ImportsPerHour"]!.GetValue<string>());
    }

    [Fact]
    public async Task Update_ShouldReportEveryValueTheStartupWouldRefuse_AndSaveNothing()
    {
        // Arrange
        using var factory = new CulinaApiFactory(postgres);
        using var admin = await AdminAsync(factory);
        var proposal = await ProposalAsync(admin);
        proposal["rateLimits"]!["importsPerHour"] = 0;
        proposal["forwardedHeaders"]!["knownProxies"] = new JsonArray("not-an-address");
        proposal["telemetry"]!["otlpEndpoint"] = "not a url";

        // Act
        var response = await admin.PutAsync("/api/v1/settings/server", proposal, Token);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(3, response.Json!.Value.GetProperty("errors").GetArrayLength());
        Assert.Equal(0, factory.Restarts.Scheduled);
        Assert.False(File.Exists(factory.ServerSettingsFile));
    }

    [Fact]
    public async Task Settings_ShouldBeOpen_WhileNobodyHasAnAccount()
    {
        // Arrange
        await postgres.ResetAsync(Token);
        using var factory = new CulinaApiFactory(postgres);
        using var stranger = factory.NewApiClient();

        // Act
        var setup = await stranger.GetAsync("/api/v1/setup", Token);
        var settings = await stranger.GetAsync("/api/v1/settings/server", Token);

        // Assert
        // Whoever creates the first account administers the instance anyway,
        // so the setup screen may fill these in before it exists.
        Assert.Equal("account", setup.Json!.Value.GetProperty("stage").GetString());
        Assert.Equal(HttpStatusCode.OK, settings.StatusCode);
    }

    [Fact]
    public async Task Settings_ShouldBeClosed_OnceSomebodyAdministersTheInstance()
    {
        // Arrange
        using var factory = new CulinaApiFactory(postgres);
        using var admin = await AdminAsync(factory);
        using var stranger = factory.NewApiClient();

        // Act
        var setup = await stranger.GetAsync("/api/v1/setup", Token);
        var settings = await stranger.GetAsync("/api/v1/settings/server", Token);
        var database = await stranger.PutAsync(
            "/api/v1/settings/database",
            new { host = "elsewhere", port = 5432, name = "x", username = "x", password = "x", requireSsl = false, maxPoolSize = 20 },
            Token);

        // Assert
        Assert.Equal("complete", setup.Json!.Value.GetProperty("stage").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, settings.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, database.StatusCode);
    }

    [Fact]
    public async Task Settings_ShouldBeForbidden_ForAnAccountThatIsNotTheAdministrator()
    {
        // Arrange
        using var factory = new CulinaApiFactory(postgres);
        using var admin = await AdminAsync(factory);
        await admin.PutAsync(
            "/api/v1/settings/registration",
            new { openRegistration = true, requireInvitation = false, maxUsers = 100 },
            Token);

        using var ordinary = factory.NewApiClient();
        await ordinary.PostAsync(
            "/api/v1/users",
            new { email = "grace@example.com", displayName = "Grace", password = Password },
            Token);
        await ordinary.PostAsync("/api/v1/sessions", new { email = "grace@example.com", password = Password }, Token);

        // Act
        var response = await ordinary.GetAsync("/api/v1/settings/server", Token);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>What the settings screen would send back unedited: the groups it read.</summary>
    private static async Task<JsonObject> ProposalAsync(ApiClient admin)
    {
        var read = JsonNode.Parse((await admin.GetAsync("/api/v1/settings/server", Token)).Body)!.AsObject();

        return new JsonObject
        {
            ["cookies"] = read["cookies"]!.DeepClone(),
            ["forwardedHeaders"] = read["forwardedHeaders"]!.DeepClone(),
            ["rateLimits"] = read["rateLimits"]!.DeepClone(),
            ["telemetry"] = read["telemetry"]!.DeepClone()
        };
    }

    private async Task<ApiClient> AdminAsync(CulinaApiFactory factory)
    {
        await postgres.ResetAsync(Token);

        var client = factory.NewApiClient();

        await client.PostAsync(
            "/api/v1/users",
            new { email = "ada@example.com", displayName = "Ada", password = Password },
            Token);
        await client.PostAsync("/api/v1/sessions", new { email = "ada@example.com", password = Password }, Token);

        return client;
    }
}
