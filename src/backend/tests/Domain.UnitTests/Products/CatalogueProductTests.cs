using Domain.Nutrition;
using Domain.Products;
using TestSupport;

namespace Domain.UnitTests.Products;

public class CatalogueProductTests
{
    private static readonly Gtin Skyr = Gtin.Create("4002971243802").ShouldBeSuccess();

    private static CatalogueProduct With(Nutrients values) =>
        new(Skyr, "Skyr Natur", "Arla", "450 g", 450m, "g", 150m, values);

    [Fact]
    public void Doubts_ShouldBeNone_WhenTheLabelAddsUp()
    {
        // 9 × 0.2 + 4 × 4 + 4 × 11 = 61.8 kcal against 63, and 63 kcal is 264 kJ.
        var product = With(new Nutrients(266m, 63m, 0.2m, 0.1m, 4m, 4m, 11m, 0.1m));

        Assert.Empty(product.Doubts());
    }

    [Fact]
    public void Doubts_ShouldNameAnEnergyMismatch_WhenPerServingValuesWereTypedAsPer100Grams()
    {
        // A chocolate bar's 45 g serving typed as 100 g: the macros still say about 530 kcal.
        var product = With(new Nutrients(null, 240m, 30m, 18m, 57m, 50m, 7m, 0.2m));

        Assert.Equal([CatalogueDoubts.EnergyMismatch], product.Doubts());
    }

    [Fact]
    public void Doubts_ShouldNameAUnitMismatch_WhenKilojoulesWereTypedAsKilocalories()
    {
        var product = With(new Nutrients(266m, 266m, 0.2m, 0.1m, 4m, 4m, 11m, 0.1m));

        Assert.Contains(CatalogueDoubts.UnitMismatch, product.Doubts());
    }

    [Fact]
    public void Doubts_ShouldNameMissingMacros_WithoutReadingThemAsZero()
    {
        var product = With(new Nutrients(null, 63m, null, null, 4m, null, 11m, null));

        Assert.Equal([CatalogueDoubts.MacrosMissing], product.Doubts());
    }

    [Fact]
    public void Doubts_ShouldSayThereIsNoEnergy_WhenNobodyEnteredTheKilocalories()
    {
        var product = With(new Nutrients(null, null, 0.2m, null, 4m, null, 11m, null));

        Assert.Equal([CatalogueDoubts.NoEnergy], product.Doubts());
    }
}
