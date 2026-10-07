using Domain.Users;
using TestSupport;

namespace Domain.UnitTests.Users;

public class DisplayNameTests
{
    [Fact]
    public void Create_ShouldTrim_WhenThereIsSurroundingWhitespace()
    {
        var result = DisplayName.Create("  Ada  ");

        Assert.Equal("Ada", result.ShouldBeSuccess().Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ShouldFail_WhenTheNameIsBlank(string? value)
    {
        var result = DisplayName.Create(value);

        result.ShouldBeFailure(UserErrors.InvalidDisplayName);
    }

    [Fact]
    public void Create_ShouldFail_WhenTheNameExceedsTheColumnLength()
    {
        var tooLong = new string('a', DisplayName.MaxLength + 1);

        var result = DisplayName.Create(tooLong);

        result.ShouldBeFailure(UserErrors.InvalidDisplayName);
    }

    [Fact]
    public void Create_ShouldAcceptTheLongestAllowedName_AtTheBoundary()
    {
        var longest = new string('a', DisplayName.MaxLength);

        var result = DisplayName.Create(longest);

        Assert.Equal(DisplayName.MaxLength, result.ShouldBeSuccess().Value.Length);
    }
}
