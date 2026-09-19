using System.Net;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Settings;

/// <summary>
/// Connecting a model over the wire — and the one value that must never come
/// back over it.
/// </summary>
[Collection(RequiresDatabase.Name)]
public class AssistanceSettingsEndpointTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";
    private const string Key = "sk-a-very-secret-key-nobody-should-see";

    [Fact]
    public async Task Read_ShouldShowAnInstanceWithNoAssistant_OnAFreshInstall()
    {
        // Arrange
        using var admin = await AdminAsync();

        // Act
        var response = await admin.GetAsync("/api/v1/settings/assistance", Token);

        // Assert
        // Off and empty by default. An instance nobody configures behaves
        // exactly as Culina behaved before any of this existed.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Json!.Value.GetProperty("enabled").GetBoolean());

        // Every provider this build knows is listed, none of them connected —
        // so adding one is filling a row in rather than finding a button.
        Assert.Equal(3, response.Json!.Value.GetProperty("connections").GetArrayLength());
        Assert.All(
            response.Json!.Value.GetProperty("connections").EnumerateArray(),
            one => Assert.False(one.GetProperty("usable").GetBoolean()));
    }

    [Fact]
    public async Task TheKey_ShouldNeverAppearInAnyResponse_OnlyThatThereIsOne()
    {
        // Arrange
        using var admin = await AdminAsync();

        // Act
        var written = await admin.PutAsync("/api/v1/settings/assistance", Configured(), Token);
        var read = await admin.GetAsync("/api/v1/settings/assistance", Token);

        // Assert
        // Asserted against the raw body rather than a parsed field, because the
        // thing being proved is that no field carries it — a test that read a
        // named property could only prove the property it thought to name.
        Assert.DoesNotContain(Key, written.Body ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(Key, read.Body ?? string.Empty, StringComparison.Ordinal);
        Assert.True(ConnectionFor(read, "openai").GetProperty("apiKeyConfigured").GetBoolean());
    }

    [Fact]
    public async Task Update_ShouldKeepTheKey_WhenTheFieldIsOmitted()
    {
        // Arrange
        using var admin = await AdminAsync();
        await admin.PutAsync("/api/v1/settings/assistance", Configured(), Token);

        // Act
        // The same form saved again to change a budget, with no key in it.
        await admin.PutAsync(
            "/api/v1/settings/assistance",
            Configured(apiKey: null, monthlyBudget: 25m),
            Token);

        var read = await admin.GetAsync("/api/v1/settings/assistance", Token);

        // Assert
        Assert.True(ConnectionFor(read, "openai").GetProperty("apiKeyConfigured").GetBoolean());
        Assert.Equal(25m, read.Json!.Value.GetProperty("monthlyBudget").GetDecimal());
    }

    [Fact]
    public async Task Update_ShouldTakeTheKeyAway_WhenAnEmptyOneIsSent()
    {
        // Arrange
        using var admin = await AdminAsync();
        await admin.PutAsync("/api/v1/settings/assistance", Configured(), Token);

        // Act
        await admin.PutAsync("/api/v1/settings/assistance", Configured(apiKey: ""), Token);
        var read = await admin.GetAsync("/api/v1/settings/assistance", Token);

        // Assert
        // Disconnecting, which is the one thing an empty string means and the
        // omitted field does not.
        Assert.False(ConnectionFor(read, "openai").GetProperty("apiKeyConfigured").GetBoolean());
        Assert.False(read.Json!.Value.GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task Update_ShouldSurviveAReload_BecauseItIsPersistedNotJustMutated()
    {
        // Arrange
        using var admin = await AdminAsync();

        // Act
        await admin.PutAsync(
            "/api/v1/settings/assistance",
            Configured(composeModel: "gemini-2.5-flash", provider: "gemini"),
            Token);

        var read = await admin.GetAsync("/api/v1/settings/assistance", Token);

        // Assert
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
        // Arrange
        using var admin = await AdminAsync();

        // Act
        var response = await admin.PutAsync(
            "/api/v1/settings/assistance",
            Configured(provider: "anthropic"),
            Token);

        // Assert
        // An unknown provider is wrong in two places at once — the connection
        // and every job pointed at it — so the answer is the aggregate, with
        // the reason on each field.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("request.validation_failed", response.ProblemCode);
        Assert.Contains("assistance.unknown_provider", response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ollama_ShouldConnectWithAnAddressAndNoKey()
    {
        // Arrange
        using var admin = await AdminAsync();

        // Act
        await admin.PutAsync(
            "/api/v1/settings/assistance",
            Configured(provider: "ollama", apiKey: null, composeModel: "llama3.2", baseUrl: Local),
            Token);

        var read = await admin.GetAsync("/api/v1/settings/assistance", Token);

        // Assert
        // The whole point of supporting it: a household that already runs a
        // model gets the assistant for nothing, sends nothing anywhere, and
        // never enters a credential.
        Assert.True(read.Json!.Value.GetProperty("enabled").GetBoolean());
        Assert.True(ConnectionFor(read, "ollama").GetProperty("usable").GetBoolean());
        Assert.False(ConnectionFor(read, "ollama").GetProperty("apiKeyConfigured").GetBoolean());
    }

    [Fact]
    public async Task Ollama_ShouldNotConnect_WithoutAnAddress()
    {
        // Arrange
        using var admin = await AdminAsync();

        // Act
        var response = await admin.PutAsync(
            "/api/v1/settings/assistance",
            Configured(provider: "ollama", apiKey: null, composeModel: "llama3.2"),
            Token);

        // Assert
        // Saved, and not connected. A blank row is a provider nobody set up
        // rather than a mistake — this screen sends all three every time.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(ConnectionFor(response, "ollama").GetProperty("usable").GetBoolean());
        Assert.False(response.Json!.Value.GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task Usage_ShouldBeEmpty_BeforeAnybodyHasAskedForAnything()
    {
        // Arrange
        using var admin = await AdminAsync();

        // Act
        var response = await admin.GetAsync("/api/v1/settings/assistance/usage", Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0m, response.Json!.Value.GetProperty("totalCost").GetDecimal());
        Assert.Equal(0, response.Json!.Value.GetProperty("byPerson").GetArrayLength());
    }

    [Theory]
    [InlineData("/api/v1/settings/assistance")]
    [InlineData("/api/v1/settings/assistance/usage")]
    public async Task Settings_ShouldBeForbidden_ForAnAccountThatIsNotTheAdministrator(string path)
    {
        // Arrange
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

        // Act
        var response = await ordinary.GetAsync(path, Token);

        // Assert
        // The connection is the instance's, not a household's: one key, one
        // bill, and one person who may change either.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Settings_ShouldBeRefused_ForSomebodyWhoIsNotSignedInAtAll()
    {
        // Arrange
        await postgres.ResetAsync(Token);
        using var stranger = postgres.Api.NewApiClient();

        // Act
        var response = await stranger.GetAsync("/api/v1/settings/assistance", Token);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private const string Local = "http://localhost:11434";

    /// <summary>One provider connected, and every job pointed at it.</summary>
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

    /// <summary>A named connection out of the response, whatever order they came in.</summary>
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
