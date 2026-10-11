using Domain.Nutrition;

namespace Domain.UnitTests.Nutrition;

public class FoodVariantsTests
{
    [Fact]
    public void All_ShouldHaveAtLeastTwoMembersInEveryGroup()
    {
        // Act & Assert
        Assert.NotEmpty(FoodVariants.All);
        Assert.All(FoodVariants.All, group => Assert.True(group.Count >= 2));
    }

    [Fact]
    public void All_ShouldPutNoFoodInTwoGroups_OrTwiceInOne()
    {
        // Act
        var codes = FoodVariants.All.SelectMany(group => group.Select(member => member.Code)).ToList();

        // Assert
        Assert.Equal(codes.Count, codes.Distinct().Count());
    }

    [Fact]
    public void All_ShouldLabelEveryMemberInBothLanguages()
    {
        // Act & Assert
        Assert.All(
            FoodVariants.All.SelectMany(group => group),
            member =>
            {
                Assert.False(string.IsNullOrWhiteSpace(member.LabelDe));
                Assert.False(string.IsNullOrWhiteSpace(member.LabelEn));
            });
    }

    [Fact]
    public void For_ShouldReturnTheWholeGroupInOrder_TheFoodItselfIncluded()
    {
        // Act
        var fromHalf = FoodVariants.For("M111200");
        var fromWhole = FoodVariants.For("M111300");

        // Assert
        Assert.Equal(fromWhole, fromHalf);
        Assert.Equal("M111300", fromHalf[0].Code);
        Assert.Contains(fromHalf, member => member.Code == "M111200");
        Assert.Contains(fromHalf, member => member.Code == "M111100");
    }

    [Fact]
    public void For_ShouldBeEmpty_ForAFoodWithoutAGroup()
    {
        // Act & Assert
        Assert.Empty(FoodVariants.For("NOPE"));
    }
}
