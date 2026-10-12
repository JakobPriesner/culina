using Domain.Shopping;

namespace Domain.UnitTests.Shopping;

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
        var item = ItemName.Create(name)
            .Match(value => value, error => throw new InvalidOperationException(error.Code));

        var section = SectionKeywords.SectionFor(item);

        Assert.Equal(expected, section);
    }

    [Theory]
    [InlineData("Kokosmilch")]
    [InlineData("Kokosmilch light")]
    [InlineData("coconut milk")]
    public void SectionFor_ShouldPreferTheMoreSpecificKeyword(string name)
    {
        var coconut = ItemName.Create(name)
            .Match(value => value, error => throw new InvalidOperationException(error.Code));

        var section = SectionKeywords.SectionFor(coconut);

        // Otherwise canned coconut milk ends up in the dairy aisle.
        Assert.Equal(ShoppingSection.CannedJars, section);
    }

    [Fact]
    public void SectionFor_ShouldFallBack_WhenItRecognisesNothing()
    {
        var unknown = ItemName.Create("Wunderpulver")
            .Match(value => value, error => throw new InvalidOperationException(error.Code));

        Assert.Equal(ShoppingSection.Other, SectionKeywords.SectionFor(unknown));
    }
}
