using Domain.Users;
using TestSupport;

namespace Domain.UnitTests.Users;

public class DisplayNameTests
{
    [Fact]
    public void Create_ShouldTrim_WhenThereIsSurroundingWhitespace()
    {
        // Arrange & Act
        var result = DisplayName.Create("  Ada  ");

        // Assert
        Assert.Equal("Ada", result.ShouldBeSuccess().Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ShouldFail_WhenTheNameIsBlank(string? value)
    {
        // Arrange & Act
        var result = DisplayName.Create(value);

        // Assert
        result.ShouldBeFailure(UserErrors.InvalidDisplayName);
    }

    [Fact]
    public void Create_ShouldFail_WhenTheNameExceedsTheColumnLength()
    {
        // Arrange
        var tooLong = new string('a', DisplayName.MaxLength + 1);

        // Act
        var result = DisplayName.Create(tooLong);

        // Assert
        result.ShouldBeFailure(UserErrors.InvalidDisplayName);
    }

    [Fact]
    public void Create_ShouldAcceptTheLongestAllowedName_AtTheBoundary()
    {
        // Arrange
        var longest = new string('a', DisplayName.MaxLength);

        // Act
        var result = DisplayName.Create(longest);

        // Assert
        Assert.Equal(DisplayName.MaxLength, result.ShouldBeSuccess().Value.Length);
    }
}
