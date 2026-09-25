using System.Net;
using System.Text.Json.Nodes;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Setup;

/// <summary>
/// The host a fresh container runs: no database configured anywhere, so it
/// serves the setup screen and the database settings, and nothing else.
/// </summary>
[Collection(RequiresDatabase.Name)]
public class SetupHostTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Setup_ShouldBeAtTheDatabaseStep_WhenNoneIsConfigured()
    {
        // Arrange
        using var factory = new SetupApiFactory();
        using var client = factory.NewApiClient();

        // Act
        var response = await client.GetAsync("/api/v1/setup", Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("database", response.Json!.Value.GetProperty("stage").GetString());
    }

    [Fact]
    public async Task EveryOtherApiRoute_ShouldSaySetupIsRequired_RatherThanNotFound()
    {
        // Arrange
        using var factory = new SetupApiFactory();
        using var client = factory.NewApiClient();

        // Act
        var response = await client.GetAsync("/api/v1/users/me", Token);

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("settings.setup_required", response.ProblemCode);
    }

    [Fact]
    public async Task Readiness_ShouldBeReady_SoAProxyRoutesToTheSetupScreen()
    {
        // Arrange
        using var factory = new SetupApiFactory();
        using var client = factory.NewApiClient();

        // Act
        var response = await client.GetAsync("/health/ready", Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Database_ShouldBeSavedAndARestartAskedFor_WhenItCanBeReached()
    {
        // Arrange
        using var factory = new SetupApiFactory();
        using var client = factory.NewApiClient();
        var settings = postgres.Settings;

        // Act
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

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(1, factory.Restarts.Scheduled);

        var saved = JsonNode.Parse(await File.ReadAllTextAsync(factory.ServerSettingsFile, Token))!;
        Assert.Equal(settings.Host, saved["Database"]!["Host"]!.GetValue<string>());
        Assert.Equal(settings.Password, saved["Database"]!["Password"]!.GetValue<string>());
    }

    [Fact]
    public async Task Database_ShouldBeRefusedWithTheServersReason_WhenThePasswordIsWrong()
    {
        // Arrange
        using var factory = new SetupApiFactory();
        using var client = factory.NewApiClient();
        var settings = postgres.Settings;

        // Act
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

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("settings.database_unreachable", response.ProblemCode);
        Assert.Equal(0, factory.Restarts.Scheduled);
        Assert.False(File.Exists(factory.ServerSettingsFile));
    }

    [Fact]
    public async Task Database_ShouldBeAccepted_WhenTheExtensionsAreMissingButTheRoleMayInstallThem()
    {
        // Arrange
        // Owning the database is what the production init script arranges, and
        // it is enough: the first migration installs the extensions itself.
        var fresh = $"fresh_{Guid.CreateVersion7():n}";
        await postgres.ExecuteAsSuperuserAsync($"create database {fresh} owner {postgres.Settings.Username};", Token);

        using var factory = new SetupApiFactory();
        using var client = factory.NewApiClient();

        // Act
        var response = await client.PutAsync("/api/v1/settings/database", DatabaseRequest(fresh), Token);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(1, factory.Restarts.Scheduled);
    }

    [Fact]
    public async Task Database_ShouldBeRefused_WhenTheRoleMayNotInstallTheExtensionsTheSchemaNeeds()
    {
        // Arrange
        // Created by someone else, so the application role has no CREATE on it.
        var foreign = $"foreign_{Guid.CreateVersion7():n}";
        await postgres.ExecuteAsSuperuserAsync($"create database {foreign};", Token);

        using var factory = new SetupApiFactory();
        using var client = factory.NewApiClient();

        // Act
        var response = await client.PutAsync("/api/v1/settings/database", DatabaseRequest(foreign), Token);

        // Assert
        // Found now, while the setup screen can still say so — not by the
        // migrations after the restart, as a process that stops on every start.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("settings.database_unsuitable", response.ProblemCode);
        Assert.Contains(
            $"GRANT CREATE ON DATABASE {foreign} TO {postgres.Settings.Username};",
            response.Body,
            StringComparison.Ordinal);
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
