using System.Net;
using Api.Infrastructure;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Settings;

[Collection(RequiresDatabase.Name)]
public class DatabaseSettingsEndpointTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task Read_ShouldSayAPasswordIsSet_WithoutEverReturningIt()
    {
        // Arrange
        using var factory = new CulinaApiFactory(postgres);
        using var admin = await AdminAsync(factory);

        // Act
        var response = await admin.GetAsync("/api/v1/settings/database", Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Json!.Value.GetProperty("passwordConfigured").GetBoolean());
        Assert.False(response.Json!.Value.TryGetProperty("password", out _));
        Assert.DoesNotContain(postgres.Settings.Password, response.Body, StringComparison.Ordinal);
        Assert.Contains(
            "Database__Host",
            response.Json!.Value.GetProperty("pinned").EnumerateArray().Select(entry => entry.GetString()));
    }

    [Fact]
    public async Task Update_ShouldChangeNothing_WhenEveryValueIsPinnedByTheEnvironment()
    {
        // Arrange
        using var factory = new CulinaApiFactory(postgres);
        using var admin = await AdminAsync(factory);
        var settings = postgres.Settings;

        // Act
        var response = await admin.PutAsync(
            "/api/v1/settings/database",
            new
            {
                host = settings.Host,
                port = settings.Port,
                name = settings.Name,
                username = settings.Username,
                password = (string?)null,
                requireSsl = false,
                maxPoolSize = 20
            },
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, factory.Restarts.Scheduled);
    }

    [Fact]
    public async Task Update_ShouldBeRefusedWith429_OnceOneAddressHasTriedTenTimesInAMinute()
    {
        // Arrange
        // Nobody has an account yet, so anybody may ask — and the limit does not
        // come from the rate limit settings, which are themselves editable here.
        await postgres.ResetAsync(Token);
        using var factory = new CulinaApiFactory(postgres);
        using var stranger = factory.NewApiClient();
        var settings = postgres.Settings;
        var same = new
        {
            host = settings.Host,
            port = settings.Port,
            name = settings.Name,
            username = settings.Username,
            password = (string?)null,
            requireSsl = false,
            maxPoolSize = 20
        };

        for (var attempt = 0; attempt < DatabaseCheckLimit.AttemptsPerMinute; attempt++)
        {
            var tried = await stranger.PutAsync("/api/v1/settings/database", same, Token);
            Assert.Equal(HttpStatusCode.NoContent, tried.StatusCode);
        }

        // Act
        var response = await stranger.PutAsync("/api/v1/settings/database", same, Token);

        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("request.rate_limited", response.ProblemCode);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

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
