using System.Net;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Settings;

/// <summary>
/// Connecting a model over the wire, and the one value that must never come back over it.
/// </summary>
[Collection(RequiresDatabase.Name)]
public class AssistanceSettingsEndpointTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";
    private const string Key = "sk-a-very-secret-key-nobody-should-see";

    [Fact]
    public async Task Read_ShouldShowAnInstanceWithNoAssistant_OnAFreshInstall()
    {
        using var admin = await AdminAsync();

        var response = await admin.GetAsync("/api/v1/settings/assistance", Token);

        // Off and empty by default: an unconfigured instance behaves as before the feature existed.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Json!.Value.GetProperty("enabled").GetBoolean());

        // Every known provider is listed, none connected, so adding one fills a row in.
        Assert.Equal(3, response.Json!.Value.GetProperty("connections").GetArrayLength());
        Assert.All(
            response.Json!.Value.GetProperty("connections").EnumerateArray(),
            one => Assert.False(one.GetProperty("usable").GetBoolean()));
    }

    [Fact]
    public async Task TheKey_ShouldNeverAppearInAnyResponse_OnlyThatThereIsOne()
    {
        using var admin = await AdminAsync();

        var written = await admin.PutAsync("/api/v1/settings/assistance", Configured(), Token);
        var read = await admin.GetAsync("/api/v1/settings/assistance", Token);

        // Asserted against the raw body: a parsed named property could only prove the one it named.
        Assert.DoesNotContain(Key, written.Body ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(Key, read.Body ?? string.Empty, StringComparison.Ordinal);
        Assert.True(ConnectionFor(read, "openai").GetProperty("apiKeyConfigured").GetBoolean());
    }

    [Fact]
    public async Task Update_ShouldKeepTheKey_WhenTheFieldIsOmitted()
    {
        using var admin = await AdminAsync();
        await admin.PutAsync("/api/v1/settings/assistance", Configured(), Token);

        // Saved again to change a budget, with no key in it.
        await admin.PutAsync(
            "/api/v1/settings/assistance",
            Configured(apiKey: null, monthlyBudget: 25m),
            Token);

        var read = await admin.GetAsync("/api/v1/settings/assistance", Token);

        Assert.True(ConnectionFor(read, "openai").GetProperty("apiKeyConfigured").GetBoolean());
        Assert.Equal(25m, read.Json!.Value.GetProperty("monthlyBudget").GetDecimal());
    }

    [Fact]
    public async Task Update_ShouldTakeTheKeyAway_WhenAnEmptyOneIsSent()
    {
        using var admin = await AdminAsync();
        await admin.PutAsync("/api/v1/settings/assistance", Configured(), Token);

        await admin.PutAsync("/api/v1/settings/assistance", Configured(apiKey: ""), Token);
        var read = await admin.GetAsync("/api/v1/settings/assistance", Token);

        // Disconnecting: the one thing an empty string means and an omitted field does not.
        Assert.False(ConnectionFor(read, "openai").GetProperty("apiKeyConfigured").GetBoolean());
        Assert.False(read.Json!.Value.GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task Update_ShouldSurviveAReload_BecauseItIsPersistedNotJustMutated()
    {
        using var admin = await AdminAsync();

        await admin.PutAsync(
            "/api/v1/settings/assistance",
            Configured(composeModel: "gemini-2.5-flash", provider: "gemini"),
            Token);

        var read = await admin.GetAsync("/api/v1/settings/assistance", Token);

        Assert.True(ConnectionFor(read, "gemini").GetProperty("usable").GetBoolean());
        Assert.Equal(
            "gemini-2.5-flash",
            read.Json!.Value.GetProperty("uses").EnumerateArray()
                .Single(one => one.GetProperty("capability").GetString() == "improve")
                .GetProperty("model").GetString());
    }

    [Fact]
    public async Task Update_ShouldRejectAProviderThisCannotTalkTo_NamingTheField()
    {
        using var admin = await AdminAsync();

        var response = await admin.PutAsync(
            "/api/v1/settings/assistance",
            Configured(provider: "anthropic"),
            Token);

        // Wrong in two places (the connection and every job pointed at it), so the answer is the
        // aggregate.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("request.validation_failed", response.ProblemCode);
        Assert.Contains("assistance.unknown_provider", response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ollama_ShouldConnectWithAnAddressAndNoKey()
    {
        using var admin = await AdminAsync();

        await admin.PutAsync(
            "/api/v1/settings/assistance",
            Configured(provider: "ollama", apiKey: null, composeModel: "llama3.2", baseUrl: Local),
            Token);

        var read = await admin.GetAsync("/api/v1/settings/assistance", Token);

        // A household that already runs a model gets the assistant for free and enters no
        // credential.
        Assert.True(read.Json!.Value.GetProperty("enabled").GetBoolean());
        Assert.True(ConnectionFor(read, "ollama").GetProperty("usable").GetBoolean());
        Assert.False(ConnectionFor(read, "ollama").GetProperty("apiKeyConfigured").GetBoolean());
    }

    [Fact]
    public async Task Ollama_ShouldNotConnect_WithoutAnAddress()
    {
        using var admin = await AdminAsync();

        var response = await admin.PutAsync(
            "/api/v1/settings/assistance",
            Configured(provider: "ollama", apiKey: null, composeModel: "llama3.2"),
            Token);

        // Saved and not connected: a blank row is a provider nobody set up, and the screen sends
        // all three.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(ConnectionFor(response, "ollama").GetProperty("usable").GetBoolean());
        Assert.False(response.Json!.Value.GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task Models_ShouldListNothing_WhenNoProviderIsConnected()
    {
        using var admin = await AdminAsync();

        var response = await admin.GetAsync("/api/v1/settings/assistance/models", Token);

        // Only connected providers appear, so an unconfigured instance asks nobody anything.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, response.Json!.Value.GetProperty("providers").GetArrayLength());
    }

    [Fact]
    public async Task Models_ShouldSayAProviderIsUnreachable_RatherThanFailing()
    {
        // A key that will not work, at an address that will not answer.
        using var admin = await AdminAsync();
        await admin.PutAsync(
            "/api/v1/settings/assistance",
            Configured(provider: "ollama", apiKey: null, baseUrl: "http://127.0.0.1:9"),
            Token);

        var response = await admin.GetAsync("/api/v1/settings/assistance/models", Token);

        // A row with the reason, not a failure: one provider being down must not cost the other
        // two.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var ollama = response.Json!.Value.GetProperty("providers")
            .EnumerateArray()
            .Single(one => one.GetProperty("provider").GetString() == "ollama");

        Assert.False(ollama.GetProperty("reachable").GetBoolean());
        Assert.Equal("assistance.unavailable", ollama.GetProperty("problem").GetString());
    }

    [Fact]
    public async Task Usage_ShouldBeEmpty_BeforeAnybodyHasAskedForAnything()
    {
        using var admin = await AdminAsync();

        var response = await admin.GetAsync("/api/v1/settings/assistance/usage", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0m, response.Json!.Value.GetProperty("totalCost").GetDecimal());
        Assert.Equal(0, response.Json!.Value.GetProperty("byPerson").GetArrayLength());
    }

    [Theory]
    [InlineData("/api/v1/settings/assistance")]
    [InlineData("/api/v1/settings/assistance/usage")]
    [InlineData("/api/v1/settings/assistance/models")]
    public async Task Settings_ShouldBeForbidden_ForAnAccountThatIsNotTheAdministrator(string path)
    {
        using var admin = await AdminAsync();
        await admin.PutAsync(
            "/api/v1/settings/registration",
            new { openRegistration = true, requireInvitation = false, maxUsers = 100 },
            Token);

        using var ordinary = postgres.Api.NewApiClient();
        await ordinary.PostAsync(
            "/api/v1/users",
            new { email = "grace@example.com", displayName = "Grace", password = Password },
            Token);
        await ordinary.PostAsync(
            "/api/v1/sessions",
            new { email = "grace@example.com", password = Password },
            Token);

        var response = await ordinary.GetAsync(path, Token);

        // The connection is the instance's, not a household's: one key, one bill, one person who
        // may change it.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Settings_ShouldBeRefused_ForSomebodyWhoIsNotSignedInAtAll()
    {
        await postgres.ResetAsync(Token);
        using var stranger = postgres.Api.NewApiClient();

        var response = await stranger.GetAsync("/api/v1/settings/assistance", Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private const string Local = "http://localhost:11434";

    private static object Configured(
        string provider = "openai",
        string? apiKey = Key,
        string composeModel = "",
        decimal? monthlyBudget = null,
        string baseUrl = "") => new
        {
            enabled = true,
            connections = new[] { new { provider, apiKey, baseUrl } },
            uses = new[]
            {
                new { capability = "improve", enabled = true, provider, model = composeModel },
                new { capability = "draft", enabled = true, provider, model = composeModel },
                new { capability = "read", enabled = true, provider, model = composeModel }
            },
            monthlyBudget,
            personalBudget = (decimal?)null
        };

    private static System.Text.Json.JsonElement ConnectionFor(ApiResponse response, string provider) =>
        response.Json!.Value.GetProperty("connections")
            .EnumerateArray()
            .Single(one => one.GetProperty("provider").GetString() == provider);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ApiClient> AdminAsync()
    {
        await postgres.ResetAsync(Token);

        var client = postgres.Api.NewApiClient();

        await client.PostAsync(
            "/api/v1/users",
            new { email = "ada@example.com", displayName = "Ada", password = Password },
            Token);
        await client.PostAsync(
            "/api/v1/sessions",
            new { email = "ada@example.com", password = Password },
            Token);

        return client;
    }
}
