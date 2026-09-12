using Domain.Shopping;

namespace Domain.UnitTests.Shopping;

/// <summary>
/// A default that is right most of the time and corrected in one tap.
/// </summary>
public class SectionKeywordsTests
{
    [Theory]
    [InlineData("Tomaten", ShoppingSection.Produce)]
    [InlineData("cherry tomatoes", ShoppingSection.Produce)]
    [InlineData("Zwiebeln", ShoppingSection.Produce)]
    [InlineData("Butter", ShoppingSection.DairyEggs)]
    [InlineData("Parmesan, frisch gerieben", ShoppingSection.DairyEggs)]
    [InlineData("Hähnchenbrust", ShoppingSection.MeatFish)]
    [InlineData("Sauerteigbrot", ShoppingSection.Bakery)]
    [InlineData("Orzo", ShoppingSection.DryGoods)]
    [InlineData("Olivenöl", ShoppingSection.SpicesBaking)]
    [InlineData("Rotwein", ShoppingSection.Drinks)]
    [InlineData("Spülmittel", ShoppingSection.Household)]
    public void SectionFor_ShouldFindTheAisle(string name, ShoppingSection expected)
    {
        // Arrange
        var item = ItemName.Create(name)
            .Match(value => value, error => throw new InvalidOperationException(error.Code));

        // Act
        var section = SectionKeywords.SectionFor(item);

        // Assert
        Assert.Equal(expected, section);
    }

    [Fact]
    public void SectionFor_ShouldPreferTheMoreSpecificKeyword()
    {
        // Arrange
        var coconut = ItemName.Create("Kokosmilch")
            .Match(value => value, error => throw new InvalidOperationException(error.Code));

        // Act
        var section = SectionKeywords.SectionFor(coconut);

        // Assert
        // Otherwise coconut milk ends up in the dairy aisle.
        Assert.Equal(ShoppingSection.DairyEggs, section);
    }

    [Fact]
    public void SectionFor_ShouldFallBack_WhenItRecognisesNothing()
    {
        // Arrange
        var unknown = ItemName.Create("Wunderpulver")
            .Match(value => value, error => throw new InvalidOperationException(error.Code));

        // Act & Assert
        Assert.Equal(ShoppingSection.Other, SectionKeywords.SectionFor(unknown));
    }
}
