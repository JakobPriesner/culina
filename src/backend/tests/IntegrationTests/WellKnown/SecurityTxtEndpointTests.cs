using System.Globalization;
using System.Net;
using IntegrationTests.Fixtures;

namespace IntegrationTests.WellKnown;

[Collection(RequiresDatabase.Name)]
public class SecurityTxtEndpointTests(PostgresFixture postgres)
{
    [Fact]
    public async Task SecurityTxt_ShouldNameTheConfiguredContact_WithAFutureExpiryAndItsCanonicalAddress()
    {
        // Arrange
        using var factory = new CulinaApiFactory(postgres, new Dictionary<string, string>
        {
            ["Site:Url"] = "https://culina.example.com",
            ["Site:SecurityContact"] = "mailto:security@example.com"
        });
        using var client = factory.NewApiClient();

        // Act
        var response = await client.GetAsync("/.well-known/security.txt", Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain; charset=utf-8", response.ContentHeaders.ContentType?.ToString());

        var fields = Fields(response.Body);
        Assert.Equal("mailto:security@example.com", fields["Contact"]);
        Assert.Equal("en, de", fields["Preferred-Languages"]);
        Assert.Equal("https://culina.example.com/.well-known/security.txt", fields["Canonical"]);

        // RFC 9116: required, in the future, and less than a year away.
        var expires = DateTimeOffset.Parse(fields["Expires"], CultureInfo.InvariantCulture);
        Assert.InRange(expires, DateTimeOffset.UtcNow.AddDays(30), DateTimeOffset.UtcNow.AddYears(1));
    }

    [Fact]
    public async Task SecurityTxt_ShouldLeaveOutTheCanonicalAddress_WhenTheSiteHasNone()
    {
        // Arrange
        using var factory = new CulinaApiFactory(postgres, new Dictionary<string, string>
        {
            ["Site:SecurityContact"] = "https://example.com/report"
        });
        using var client = factory.NewApiClient();

        // Act
        var response = await client.GetAsync("/.well-known/security.txt", Token);

        // Assert
        var fields = Fields(response.Body);
        Assert.Equal("https://example.com/report", fields["Contact"]);
        Assert.False(fields.ContainsKey("Canonical"));
    }

    [Fact]
    public async Task SecurityTxt_ShouldBeNotFound_WhenNoContactIsConfigured()
    {
        // Arrange
        using var client = postgres.Api.NewApiClient();

        // Act
        var response = await client.GetAsync("/.well-known/security.txt", Token);

        // Assert
        // Not the app shell: a finder must not take HTML for a policy.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("<html", response.Body, StringComparison.OrdinalIgnoreCase);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Dictionary<string, string> Fields(string body) =>
        body.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split(": ", 2))
            .ToDictionary(parts => parts[0], parts => parts[1]);
}
