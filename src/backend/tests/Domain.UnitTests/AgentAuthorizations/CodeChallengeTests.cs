using Domain.AgentAuthorizations;

namespace Domain.UnitTests.AgentAuthorizations;

/// <summary>PKCE S256, checked against the example in RFC 7636, appendix B.</summary>
public class CodeChallengeTests
{
    private const string Verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
    private const string Challenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

    [Fact]
    public void Verifies_ShouldAccept_WhenTheVerifierHashesToTheChallenge()
    {
        // Act
        var verified = CodeChallenge.Verifies(Verifier, Challenge);

        // Assert
        Assert.True(verified);
    }

    [Theory]
    [InlineData("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXK")]
    [InlineData(Challenge)]
    [InlineData("short")]
    [InlineData(null)]
    public void Verifies_ShouldRefuse_WhenTheVerifierIsWrongOrMalformed(string? verifier)
    {
        // Act
        var verified = CodeChallenge.Verifies(verifier, Challenge);

        // Assert
        Assert.False(verified);
    }

    [Theory]
    [InlineData(Challenge, true)]
    [InlineData("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-c", false)]
    [InlineData("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw+cM", false)]
    [InlineData(null, false)]
    public void IsWellFormed_ShouldAcceptOnlyAnS256Digest(string? challenge, bool expected)
    {
        // Act
        var wellFormed = CodeChallenge.IsWellFormed(challenge);

        // Assert
        Assert.Equal(expected, wellFormed);
    }
}
