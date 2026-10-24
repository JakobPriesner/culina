using Domain.AgentAuthorizations;
using Domain.Shared;

namespace Domain.UnitTests.AgentAuthorizations;

/// <summary>A request to connect an AI agent: answered once, and its code redeemed once, by the app that asked, in time.</summary>
public class AgentAuthorizationTests
{
    private const string Verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
    private const string Challenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";
    private const string RedirectUri = "https://claude.ai/api/mcp/auth_callback";
    private const string Resource = "https://culina.example/api/mcp";

    private static readonly DateTimeOffset AskedAt = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
    private static readonly AgentClient Claude = new("https://claude.ai/oauth/claude-client.json", "Claude", "claude.ai");
    private static readonly Guid Ada = Guid.NewGuid();

    [Fact]
    public void Approve_ShouldRefuse_WhenTheRequestWasAnsweredAlready()
    {
        // Arrange
        var authorization = Approved();

        // Act
        var again = authorization.Approve(Ada, new byte[] { 2 }, null, null, AskedAt.AddMinutes(1));

        // Assert
        Assert.Equal(AgentAuthorizationErrors.NotFound.Code, Code(again));
    }

    [Fact]
    public void Approve_ShouldRefuse_WhenNobodyAnsweredInTime()
    {
        // Arrange
        var authorization = Pending();

        // Act
        var late = authorization.Approve(Ada, new byte[] { 1 }, null, null, AskedAt.Add(AgentAuthorization.PendingLifetime));

        // Assert
        Assert.Equal(AgentAuthorizationErrors.NotFound.Code, Code(late));
    }

    [Fact]
    public void Redeem_ShouldSucceedOnce_WhenTheSameAppAnswersThePkceChallengeInTime()
    {
        // Arrange
        var authorization = Approved();

        // Act
        var first = authorization.Redeem(Claude.ClientId, RedirectUri, Verifier, Resource, AskedAt.AddMinutes(1));
        var second = authorization.Redeem(Claude.ClientId, RedirectUri, Verifier, Resource, AskedAt.AddMinutes(1));

        // Assert
        Assert.Null(Code(first));
        Assert.Equal(AgentAuthorizationErrors.InvalidGrant.Code, Code(second));
    }

    [Theory]
    [InlineData("https://evil.example/client.json", RedirectUri, Verifier)]
    [InlineData("https://claude.ai/oauth/claude-client.json", "https://claude.ai/other", Verifier)]
    [InlineData("https://claude.ai/oauth/claude-client.json", RedirectUri, "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXK")]
    public void Redeem_ShouldRefuse_WhenTheAppTheAddressOrTheVerifierDiffers(string clientId, string redirectUri, string verifier)
    {
        // Arrange
        var authorization = Approved();

        // Act
        var redeemed = authorization.Redeem(clientId, redirectUri, verifier, null, AskedAt.AddMinutes(1));

        // Assert
        Assert.Equal(AgentAuthorizationErrors.InvalidGrant.Code, Code(redeemed));
    }

    [Fact]
    public void Redeem_ShouldRefuse_WhenTheCodeExpired()
    {
        // Arrange
        var authorization = Approved();

        // Act
        var redeemed = authorization.Redeem(
            Claude.ClientId, RedirectUri, Verifier, null, AskedAt.AddMinutes(1).Add(AgentAuthorization.CodeLifetime));

        // Assert
        Assert.Equal(AgentAuthorizationErrors.InvalidGrant.Code, Code(redeemed));
    }

    [Fact]
    public void Redeem_ShouldRefuse_WhenTheCodeWasDenied()
    {
        // Arrange
        var authorization = Pending();
        authorization.Deny(Ada, AskedAt.AddMinutes(1));

        // Act
        var redeemed = authorization.Redeem(Claude.ClientId, RedirectUri, Verifier, null, AskedAt.AddMinutes(1));

        // Assert
        Assert.Equal(AgentAuthorizationErrors.InvalidGrant.Code, Code(redeemed));
    }

    [Theory]
    [InlineData("https://culina.example/api/mcp", true)]
    [InlineData("HTTPS://Culina.Example/api/mcp/", true)]
    [InlineData("https://culina.example/api/MCP", false)]
    [InlineData("https://culina.example", false)]
    [InlineData("https://elsewhere.example/api/mcp", false)]
    [InlineData("https://culina.example/api/mcp?x=1", false)]
    [InlineData("https://culina.example/api/mcp#x", false)]
    public void Names_ShouldMatchOnlyThisMcpServer(string presented, bool expected)
    {
        // Act
        var names = AgentAuthorization.Names(presented, Resource);

        // Assert
        Assert.Equal(expected, names);
    }

    private static AgentAuthorization Pending() =>
        AgentAuthorization.Start(Claude, RedirectUri, Challenge, "xyz", Resource, AskedAt);

    private static AgentAuthorization Approved()
    {
        var authorization = Pending();
        authorization.Approve(Ada, new byte[] { 1 }, "203.0.113.7", "Firefox", AskedAt.AddMinutes(1));

        return authorization;
    }

    private static string? Code(Result result) => result.Match<string?>(() => null, error => error.Code);
}
