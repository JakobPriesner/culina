using Domain.AgentAuthorizations;

namespace Domain.UnitTests.AgentAuthorizations;

/// <summary>An app's own name, which the consent screen shows as text.</summary>
public class AgentClientTests
{
    [Theory]
    [InlineData("Claude", "Claude")]
    [InlineData("  Chat\tGPT \n", "Chat GPT")]
    [InlineData("Claude‮evil", "Claude evil")]
    [InlineData("", "claude.ai")]
    [InlineData(null, "claude.ai")]
    [InlineData("​​", "claude.ai")]
    public void CleanName_ShouldLeaveOnlyPrintableText(string? name, string expected)
    {
        // Act
        var cleaned = AgentClient.CleanName(name, "claude.ai");

        // Assert
        Assert.Equal(expected, cleaned);
    }

    [Fact]
    public void CleanName_ShouldShortenALongName()
    {
        // Act
        var cleaned = AgentClient.CleanName(new string('a', 500), "claude.ai");

        // Assert
        Assert.Equal(80, cleaned.Length);
    }
}
