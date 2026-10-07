using System.Net;
using System.Text.Json.Nodes;
using Api.Infrastructure;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Setup;

/// <summary>The host a fresh container runs: no database configured, so it serves only the setup screen and the database settings.</summary>
[Collection(RequiresDatabase.Name)]
public class SetupHostTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Setup_ShouldBeAtTheDatabaseStep_WhenNoneIsConfigured()
    {
        using var factory = new SetupApiFactory();
        using var client = factory.NewApiClient();

        var response = await client.GetAsync("/api/v1/setup", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("database", response.Json!.Value.GetProperty("stage").GetString());
    }

    [Fact]
    public async Task EveryOtherApiRoute_ShouldSaySetupIsRequired_RatherThanNotFound()
    {
        using var factory = new SetupApiFactory();
        using var client = factory.NewApiClient();

        var response = await client.GetAsync("/api/v1/users/me", Token);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("settings.setup_required", response.ProblemCode);
    }

    [Fact]
    public async Task Readiness_ShouldBeReady_SoAProxyRoutesToTheSetupScreen()
    {
        using var factory = new SetupApiFactory();
        using var client = factory.NewApiClient();

        var response = await client.GetAsync("/health/ready", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Database_ShouldBeSavedAndARestartAskedFor_WhenItCanBeReached()
    {
        using var factory = new SetupApiFactory();
        using var client = factory.NewApiClient();
        var settings = postgres.Settings;

        var response = await client.PutAsync(
            "/api/v1/settings/database",
            new
            {
                host = settings.Host,
                port = settings.Port,
                name = settings.Name,
                username = settings.Username,
                password = settings.Password,
                requireSsl = false,
                maxPoolSize = 20
            },
            Token);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(1, factory.Restarts.Scheduled);

        var saved = JsonNode.Parse(await File.ReadAllTextAsync(factory.ServerSettingsFile, Token))!;
        Assert.Equal(settings.Host, saved["Database"]!["Host"]!.GetValue<string>());
        Assert.Equal(settings.Password, saved["Database"]!["Password"]!.GetValue<string>());
    }

    [Fact]
    public async Task Database_ShouldBeRefusedWithoutTheServersOwnWords_WhenThePasswordIsWrong()
    {
        using var factory = new SetupApiFactory();
        using var client = factory.NewApiClient();
        var settings = postgres.Settings;

        var response = await client.PutAsync(
            "/api/v1/settings/database",
            new
            {
                host = settings.Host,
                port = settings.Port,
                name = settings.Name,
                username = settings.Username,
                password = "not the password",
                requireSsl = false,
                maxPoolSize = 20
            },
            Token);

        // Which kind of failure and nothing the server said: anybody may ask this during setup, of any address.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("settings.database_login_refused", response.ProblemCode);
        Assert.DoesNotContain("password authentication failed", response.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, factory.Restarts.Scheduled);
        Assert.False(File.Exists(factory.ServerSettingsFile));
    }

    [Fact]
    public async Task Database_ShouldBeAccepted_WhenTheExtensionsAreMissingButTheRoleMayInstallThem()
    {
        // Owning the database is enough (the production init script arranges it); the first migration installs the extensions.
        var fresh = $"fresh_{Guid.CreateVersion7():n}";
        await postgres.ExecuteAsSuperuserAsync($"create database {fresh} owner {postgres.Settings.Username};", Token);

        using var factory = new SetupApiFactory();
        using var client = factory.NewApiClient();

        var response = await client.PutAsync("/api/v1/settings/database", DatabaseRequest(fresh), Token);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(1, factory.Restarts.Scheduled);
    }

    [Fact]
    public async Task Database_ShouldBeRefused_WhenTheRoleMayNotInstallTheExtensionsTheSchemaNeeds()
    {
        // Created by someone else, so the application role has no CREATE on it.
        var foreign = $"foreign_{Guid.CreateVersion7():n}";
        await postgres.ExecuteAsSuperuserAsync($"create database {foreign};", Token);

        using var factory = new SetupApiFactory();
        using var client = factory.NewApiClient();

        var response = await client.PutAsync("/api/v1/settings/database", DatabaseRequest(foreign), Token);

        // Found now, while setup can still say so, not by migrations after the restart.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("settings.database_unsuitable", response.ProblemCode);
        Assert.Contains(
            $"GRANT CREATE ON DATABASE {foreign} TO {postgres.Settings.Username};",
            response.Body,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Database_ShouldBeRefusedWith429_OnceOneAddressHasTriedTenTimesInAMinute()
    {
        // Every attempt connects to the address it names, and anybody may ask while the instance is unclaimed.
        using var factory = new SetupApiFactory();
        using var client = factory.NewApiClient();
        var nowhere = new
        {
            host = "127.0.0.1",
            port = ScriptedServer.ClosedPort(),
            name = "culina",
            username = "culina_app",
            password = "a guess",
            requireSsl = false,
            maxPoolSize = 20
        };

        for (var attempt = 0; attempt < DatabaseCheckLimit.AttemptsPerMinute; attempt++)
        {
            var tried = await client.PutAsync("/api/v1/settings/database", nowhere, Token);
            Assert.Equal(HttpStatusCode.BadRequest, tried.StatusCode);
        }

        var response = await client.PutAsync("/api/v1/settings/database", nowhere, Token);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("request.rate_limited", response.ProblemCode);
        Assert.NotNull(response.Headers.RetryAfter);
    }

    private object DatabaseRequest(string name)
    {
        var settings = postgres.Settings;

        return new
        {
            host = settings.Host,
            port = settings.Port,
            name,
            username = settings.Username,
            password = settings.Password,
            requireSsl = false,
            maxPoolSize = 20
        };
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;
}
