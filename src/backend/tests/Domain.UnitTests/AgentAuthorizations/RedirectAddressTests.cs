using Domain.AgentAuthorizations;

namespace Domain.UnitTests.AgentAuthorizations;

/// <summary>Where an app may ask the browser to be sent back with a code: https, or this computer for a native app.</summary>
public class RedirectAddressTests
{
    [Theory]
    [InlineData("https://claude.ai/api/mcp/auth_callback")]
    [InlineData("https://chatgpt.com/connector_platform_oauth_redirect")]
    [InlineData("http://localhost:3118/callback")]
    [InlineData("http://127.0.0.1/callback")]
    [InlineData("http://[::1]:8080/callback")]
    public void IsAcceptable_ShouldAccept_WhenHttpsOrLoopback(string address)
    {
        // Act
        var acceptable = RedirectAddress.IsAcceptable(address);

        // Assert
        Assert.True(acceptable);
    }

    [Theory]
    [InlineData("http://example.com/callback")]
    [InlineData("http://192.168.1.10/callback")]
    [InlineData("cursor://anysphere.cursor-mcp/oauth/callback")]
    [InlineData("https://example.com/callback#fragment")]
    [InlineData("https://user:pass@example.com/callback")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/callback")]
    [InlineData("")]
    public void IsAcceptable_ShouldRefuse_WhenNeitherHttpsNorLoopback(string address)
    {
        // Act
        var acceptable = RedirectAddress.IsAcceptable(address);

        // Assert
        Assert.False(acceptable);
    }

    [Theory]
    [InlineData("http://localhost/callback", "http://localhost:3118/callback")]
    [InlineData("http://127.0.0.1/callback", "http://127.0.0.1:50123/callback")]
    [InlineData("http://127.0.0.1:8000/callback", "http://127.0.0.1:9000/callback")]
    [InlineData("https://claude.ai/api/mcp/auth_callback", "https://claude.ai/api/mcp/auth_callback")]
    public void Matches_ShouldAccept_WhenOnlyALoopbackPortDiffers(string registered, string presented)
    {
        // Act
        var matches = RedirectAddress.Matches(registered, presented);

        // Assert
        Assert.True(matches);
    }

    [Theory]
    [InlineData("https://claude.ai/api/mcp/auth_callback", "https://claude.ai/api/mcp/auth_callback/")]
    [InlineData("https://claude.ai/api/mcp/auth_callback", "https://claude.ai:8443/api/mcp/auth_callback")]
    [InlineData("https://claude.ai/api/mcp/auth_callback", "https://evil.example/api/mcp/auth_callback")]
    [InlineData("http://localhost/callback", "http://127.0.0.1/callback")]
    [InlineData("http://localhost/callback", "http://localhost:3118/other")]
    [InlineData("http://localhost/callback", "https://localhost/callback")]
    public void Matches_ShouldRefuse_WhenAnythingButALoopbackPortDiffers(string registered, string presented)
    {
        // Act
        var matches = RedirectAddress.Matches(registered, presented);

        // Assert
        Assert.False(matches);
    }
}
