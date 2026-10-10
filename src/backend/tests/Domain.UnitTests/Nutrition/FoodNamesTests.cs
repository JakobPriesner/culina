using Domain.Nutrition;
using Domain.Search;

namespace Domain.UnitTests.Nutrition;

public class FoodNamesTests
{
    [Fact]
    public void All_ShouldNotGiveAFoldedFormToTwoEntries()
    {
        // Act
        foreach (var fold in new Func<string, string>[] { SearchText.FoldAe, SearchText.FoldA })
        {
            var owners = FoodNames.All
                .SelectMany(entry => entry.De.Concat(entry.En).Select(form => (Form: fold(form), entry.Code)))
                .GroupBy(pair => pair.Form)
                .Where(group => group.Select(pair => pair.Code).Distinct().Count() > 1)
                .Select(group => group.Key)
                .ToList();

            // Assert
            Assert.Empty(owners);
        }
    }

    [Fact]
    public void All_ShouldHaveOneEntryPerCode_AndForms()
    {
        // Assert
        Assert.Equal(FoodNames.All.Count, FoodNames.All.Select(entry => entry.Code).Distinct().Count());
        Assert.All(FoodNames.All, entry => Assert.NotEmpty(entry.De.Concat(entry.En)));
    }

    [Fact]
    public void All_ShouldHaveALabelInBothLanguages()
    {
        // Assert
        Assert.All(FoodNames.All, entry =>
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.LabelDe), entry.Code);
            Assert.False(string.IsNullOrWhiteSpace(entry.LabelEn), entry.Code);

            if (entry.Liquid is { } liquid)
            {
                Assert.False(string.IsNullOrWhiteSpace(liquid.LabelDe), entry.Code);
                Assert.False(string.IsNullOrWhiteSpace(liquid.LabelEn), entry.Code);
            }
        });
    }

    [Fact]
    public void All_ShouldGiveALiquidAFoodOfItsOwn_AndAPlausibleDensity()
    {
        // Assert
        var withLiquid = FoodNames.All.Where(entry => entry.Liquid is not null).ToList();

        Assert.NotEmpty(withLiquid);

        foreach (var entry in withLiquid)
        {
            Assert.NotEqual(entry.Code, entry.Liquid!.Code);
            Assert.StartsWith("R", entry.Code, StringComparison.Ordinal);
            Assert.InRange(entry.Liquid.Density, 0.9m, 1.1m);
            Assert.Null(entry.Density);
        }
    }

    [Fact]
    public void All_ShouldGiveADensityOnlyWithinWhatALiquidWeighs_AndNeverToAnEgg()
    {
        // Assert
        foreach (var entry in FoodNames.All.Where(entry => entry.Density is not null))
        {
            Assert.InRange(entry.Density!.Value, 0.6m, 1.5m);
            Assert.Equal(EggPart.None, entry.Egg);
        }
    }

    [Theory]
    [InlineData("Kokosmilch", "H154000")]
    [InlineData("coconut milk", "H154000")]
    [InlineData("Erdnussbutter", "H880200")]
    [InlineData("Tomate(n)", "G561100")]
    [InlineData("tomatoes", "G561100")]
    [InlineData("Hähnchenbrustfilet(s)", "V416100")]
    [InlineData("Paprika, rot", "G543100")]
    [InlineData("Paprika rot", "G543100")]
    [InlineData("MILCH", "M111300")]
    [InlineData("milk", "M111300")]
    [InlineData("Eier", "E111100")]
    public void Match_ShouldFindTheFood_WhenTheWholeNameIsAForm(string name, string code)
    {
        // Act & Assert
        Assert.Equal(code, FoodNames.Match(name)?.Code);
    }

    [Theory]
    [InlineData("Kidneybohnen")]
    [InlineData("Kichererbsen")]
    [InlineData("Mais")]
    [InlineData("Quark")]
    [InlineData("Kochsahne")]
    [InlineData("Fond")]
    [InlineData("Brühe")]
    [InlineData("Bouillon")]
    [InlineData("stock")]
    [InlineData("Kokosfett")]
    [InlineData("Balsamicoessig (bianco)")]
    [InlineData("Eis")]
    [InlineData("Eisen")]
    [InlineData("Milchpulver")]
    [InlineData("")]
    [InlineData("   ")]
    public void Match_ShouldFindNothing_WhenNoFormIsTheWholeName(string name)
    {
        // Act & Assert
        Assert.Null(FoodNames.Match(name));
    }

    [Theory]
    [InlineData("Kidneybohnen aus der Dose", "H742902")]
    [InlineData("canned chickpeas", "H720902")]
    [InlineData("Dosenmais", "G570902")]
    [InlineData("Erdnüsse", "H110600")]
    [InlineData("gesalzene Erdnüsse", "H110700")]
    [InlineData("Quark 20 %", "M713300")]
    [InlineData("Speisequark 40 %", "M713500")]
    [InlineData("Crème double", "M172900")]
    [InlineData("Kokosöl", "Q550000")]
    [InlineData("Gemüsebrühe", "R821000")]
    [InlineData("Hühnerbrühe", "R822000")]
    [InlineData("Rinderbrühe", "R811000")]
    [InlineData("Brühpulver", "R810000")]
    public void Match_ShouldFindTheFood_OfTheEntriesAddedForLabelsAndBroth(string name, string code)
    {
        // Act & Assert
        Assert.Equal(code, FoodNames.Match(name)?.Code);
    }

    [Theory]
    [InlineData("Kokosmilch", "M111300")]
    [InlineData("Erdnussbutter", "Q611000")]
    [InlineData("Kokosöl", "Q940000")]
    public void Match_ShouldNotSplitACompound(string name, string notThis)
    {
        // Act & Assert
        Assert.NotEqual(notThis, FoodNames.Match(name)?.Code);
    }

    [Fact]
    public void Match_ShouldNotReadMangoldAsMango()
    {
        // Act & Assert
        Assert.Equal("G230100", FoodNames.Match("Mangold")?.Code);
        Assert.Equal("F516100", FoodNames.Match("Mango")?.Code);
    }

    [Theory]
    [InlineData("Möhren")]
    [InlineData("Moehren")]
    [InlineData("Mohren")]
    [InlineData("möhre")]
    public void Match_ShouldFoldUmlautsEitherWay(string name)
    {
        // Act & Assert
        Assert.Equal("G620100", FoodNames.Match(name)?.Code);
    }

    [Fact]
    public void Match_ShouldAllowAShortEndingOnTheLastWordOnly()
    {
        // Act & Assert
        Assert.Equal("G480100", FoodNames.Match("Zwiebeln")?.Code);
        Assert.Equal("F603100", FoodNames.Match("Orangen")?.Code);
        Assert.Null(FoodNames.Match("Orangenxyz"));
        Assert.Null(FoodNames.Match("Orangenschale"));
    }

    [Fact]
    public void Version_ShouldFeedTheNutritionVersion()
    {
        // Assert
        Assert.True(NutritionData.Version > FoodNames.Version);
    }
}
