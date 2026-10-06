using System.Net;
using Application.Abstractions.Settings;
using Infrastructure.Settings;
using IntegrationTests.Fixtures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IntegrationTests.Settings;

/// <summary>
/// Secure cookies are off only where somebody decided they may be: in
/// Development, or on a deployment that says so by name. Anywhere else the
/// process refuses to start, because a session cookie in cleartext is not a
/// thing to find out about from a packet capture.
/// </summary>
[Collection(RequiresDatabase.Name)]
public class CookieSettingsStartupTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public void AddCookieSettings_ShouldAllowInsecureCookies_InDevelopment()
    {
        // Arrange
        var configuration = Configuration(new() { ["Cookies:Secure"] = "false" });

        // Act
        var settings = Register(configuration, Environments.Development);

        // Assert
        Assert.False(settings.Secure);
        Assert.True(settings.InsecureAllowed);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void AddCookieSettings_ShouldRefuseInsecureCookies_WhenOutsideDevelopmentWithoutTheOptIn(string environment)
    {
        // Arrange
        var configuration = Configuration(new() { ["Cookies:Secure"] = "false" });

        // Act
        void Act() => Register(configuration, environment);

        // Assert
        var exception = Assert.Throws<InvalidOperationException>(Act);
        Assert.Contains("Cookies__AllowInsecureOutsideDevelopment", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddCookieSettings_ShouldAllowInsecureCookies_WhenTheDeploymentOptsIn()
    {
        // Arrange
        var configuration = Configuration(new()
        {
            ["Cookies:Secure"] = "false",
            ["Cookies:AllowInsecureOutsideDevelopment"] = "true"
        });

        // Act
        var settings = Register(configuration, Environments.Production);

        // Assert
        Assert.True(settings.InsecureAllowed);
    }

    [Fact]
    public void AddCookieSettings_ShouldNotAllowInsecureCookies_WhenOutsideDevelopmentAndCookiesAreSecure()
    {
        // Arrange
        var configuration = Configuration([]);

        // Act
        var settings = Register(configuration, Environments.Production);

        // Assert
        // So a later save from the settings screen cannot turn them off.
        Assert.True(settings.Secure);
        Assert.False(settings.InsecureAllowed);
    }

    [Fact]
    public void Startup_ShouldFail_WhenCookiesAreInsecureOutsideDevelopmentWithoutTheOptIn()
    {
        // Arrange
        using var factory = new CulinaApiFactory(
            postgres,
            new Dictionary<string, string> { ["Cookies:AllowInsecureOutsideDevelopment"] = "false" });

        // Act
        void Act() => factory.CreateClient().Dispose();

        // Assert
        var exception = Assert.ThrowsAny<Exception>(Act);
        Assert.Contains("Cookies__AllowInsecureOutsideDevelopment", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Startup_ShouldWarn_WhenCookiesAreInsecure()
    {
        // Arrange
        using var factory = new CulinaApiFactory(postgres);
        using var client = factory.NewApiClient();

        // Act
        await client.GetAsync("/health/live", Token);

        // Assert
        var line = Assert.Single(factory.Logs.Lines, line => line.EventId == 1603);
        Assert.Equal(LogLevel.Warning, line.Level);
        Assert.Equal("Production", line["Environment"]);
    }

    /// <summary>
    /// What compose.prod.yaml produces from a copied .env.example: the
    /// variables passed through, and empty.
    /// </summary>
    [Fact]
    public async Task SignIn_ShouldSetASecureHostPrefixedCookie_WhenTheDeploymentLeavesCookiesUnset()
    {
        // Arrange
        await postgres.ResetAsync(Token);
        using var factory = new CulinaApiFactory(
            postgres,
            new Dictionary<string, string>
            {
                ["Cookies:Secure"] = string.Empty,
                ["Cookies:AllowInsecureOutsideDevelopment"] = string.Empty
            });
        using var client = factory.NewApiClient();
        await client.PostAsync("/api/v1/users", new { email = "ada@example.com", displayName = "Ada", password = Password }, Token);

        // Act
        var response = await client.PostAsync("/api/v1/sessions", new { email = "ada@example.com", password = Password }, Token);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var session = Assert.Single(
            response.Headers.GetValues("Set-Cookie"),
            cookie => cookie.StartsWith($"{CookieSettings.SessionCookieName}=", StringComparison.Ordinal));
        Assert.Contains("; secure", session, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(factory.Logs.Lines, line => line.EventId == 1603);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static CookieSettings Register(IConfiguration configuration, string environment)
    {
        using var provider = new ServiceCollection()
            .AddCookieSettings(configuration, new HostEnvironment(environment))
            .BuildServiceProvider();

        return provider.GetRequiredService<CookieSettings>();
    }

    private sealed class HostEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "Culina";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
