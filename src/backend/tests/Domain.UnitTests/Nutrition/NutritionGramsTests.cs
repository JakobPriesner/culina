using Domain.Nutrition;
using Domain.Recipes;

namespace Domain.UnitTests.Nutrition;

public class NutritionGramsTests
{
    private static readonly FoodName Flour = new("C214100", ["Mehl"], ["flour"], Density: null, EggPart.None);
    private static readonly FoodName Onion = new("G480100", ["Zwiebel"], ["onion"], Density: null, EggPart.None);
    private static readonly FoodName Salt = new("R111000", ["Salz"], ["salt"], Density: null, EggPart.None);
    private static readonly FoodName Milk = new("M111300", ["Milch"], ["milk"], Density: 1.031m, EggPart.None);
    private static readonly FoodName Oil = new("Q120000", ["Olivenöl"], ["olive oil"], Density: 0.913m, EggPart.None);
    private static readonly FoodName Egg = new("E111100", ["Ei"], ["egg"], Density: null, EggPart.Whole);
    private static readonly FoodName Yolk = new("E112100", ["Eigelb"], ["egg yolk"], Density: null, EggPart.Yolk);
    private static readonly FoodName White = new("E113100", ["Eiweiß"], ["egg white"], Density: null, EggPart.White);

    [Fact]
    public void Read_ShouldCountGrams_AsMass()
    {
        // Act
        var reading = NutritionGrams.Read(Measured(250m, "g"), Flour);

        // Assert
        Assert.Equal(250m, reading.Grams);
        Assert.Equal(GramsBasis.Mass, reading.Basis);
    }

    [Fact]
    public void Read_ShouldCountAKilogram_AsAThousandGrams()
    {
        // Act
        var reading = NutritionGrams.Read(Measured(1m, "kg"), Flour);

        // Assert
        Assert.Equal(1000m, reading.Grams);
        Assert.Equal(GramsBasis.Mass, reading.Basis);
    }

    [Fact]
    public void Read_ShouldMultiplyMillilitresByDensity_WhenTheFoodPours()
    {
        // Act
        var reading = NutritionGrams.Read(Measured(200m, "ml"), Milk);

        // Assert
        Assert.Equal(200m * 1.031m, reading.Grams);
        Assert.Equal(GramsBasis.Density, reading.Basis);
    }

    [Fact]
    public void Read_ShouldMultiplyLitresByDensity_WhenTheFoodPours()
    {
        // Act
        var reading = NutritionGrams.Read(Measured(0.5m, "l"), Milk);

        // Assert
        Assert.Equal(500m * 1.031m, reading.Grams);
    }

    [Fact]
    public void Read_ShouldCountATablespoonAsFifteenMillilitres_WhenTheFoodPours()
    {
        // Act
        var reading = NutritionGrams.Read(Measured(2m, "tbsp"), Oil);

        // Assert
        Assert.Equal(30m * 0.913m, reading.Grams);
        Assert.Equal(GramsBasis.Density, reading.Basis);
    }

    [Fact]
    public void Read_ShouldCountATeaspoonAsFiveMillilitres_WhenTheFoodPours()
    {
        // Act
        var reading = NutritionGrams.Read(Measured(3m, "tsp"), Oil);

        // Assert
        Assert.Equal(15m * 0.913m, reading.Grams);
    }

    [Theory]
    [InlineData("tbsp")]
    [InlineData("tsp")]
    [InlineData("ml")]
    [InlineData("l")]
    public void Read_ShouldNotCountAVolumeOrSpoon_WhenTheFoodHasNoDensity(string unit)
    {
        // Act
        var reading = NutritionGrams.Read(Measured(1m, unit), Flour);

        // Assert
        Assert.Null(reading.Grams);
        Assert.Equal(GramsRefusal.NotInGrams, reading.Refusal);
    }

    [Fact]
    public void Read_ShouldCountTwoEggs_AsTheEdibleWeightOfSizeM()
    {
        // Act
        var bare = NutritionGrams.Read(Measured(2m, unit: null), Egg);
        var pieces = NutritionGrams.Read(Measured(2m, "piece"), Egg);

        // Assert
        Assert.Equal(102m, bare.Grams);
        Assert.Equal(GramsBasis.EggSize, bare.Basis);
        Assert.Equal(102m, pieces.Grams);
    }

    [Fact]
    public void Read_ShouldWeighAYolkAndAWhite_InTheProportionOfAnEgg()
    {
        // Act
        var yolk = NutritionGrams.Read(Measured(1m, unit: null), Yolk);
        var white = NutritionGrams.Read(Measured(1m, unit: null), White);

        // Assert
        Assert.Equal(51m * 17m / 50m, yolk.Grams);
        Assert.Equal(51m * 33m / 50m, white.Grams);
        Assert.Equal(51m, yolk.Grams + white.Grams);
    }

    [Fact]
    public void Read_ShouldNotCountAnEgg_WhenTheUnitIsNotACount()
    {
        // Act
        var reading = NutritionGrams.Read(Measured(1m, "can"), Egg);

        // Assert
        Assert.Equal(GramsRefusal.NotInGrams, reading.Refusal);
    }

    [Fact]
    public void Read_ShouldNeverCountAnOnionByPiece()
    {
        // Act
        var bare = NutritionGrams.Read(Measured(1m, unit: null), Onion);
        var pieces = NutritionGrams.Read(Measured(1m, "piece"), Onion);

        // Assert
        Assert.Null(bare.Grams);
        Assert.Equal(GramsRefusal.NotInGrams, bare.Refusal);
        Assert.Null(pieces.Grams);
    }

    [Theory]
    [InlineData("clove")]
    [InlineData("bunch")]
    [InlineData("can")]
    [InlineData("pack")]
    [InlineData("pinch")]
    [InlineData("slice")]
    [InlineData("Tasse")]
    [InlineData("Becher")]
    public void Read_ShouldCountNothing_ForAHouseholdOrCountUnit(string unit)
    {
        // Act
        var reading = NutritionGrams.Read(Measured(1m, unit), Milk);

        // Assert
        Assert.Null(reading.Grams);
        Assert.Equal(GramsRefusal.NotInGrams, reading.Refusal);
    }

    [Fact]
    public void Read_ShouldSayNoAmount_ForSaltWithNoAmount()
    {
        // Act
        var reading = NutritionGrams.Read(Quantity.Unmeasured, Salt);

        // Assert
        Assert.Null(reading.Grams);
        Assert.Equal(GramsRefusal.NoAmount, reading.Refusal);
    }

    [Fact]
    public void Read_ShouldSayNoAmount_ForAnEggWithNoAmount()
    {
        // Act
        var reading = NutritionGrams.Read(Quantity.Unmeasured, Egg);

        // Assert
        Assert.Equal(GramsRefusal.NoAmount, reading.Refusal);
    }

    private static Quantity Measured(decimal amount, string? unit)
    {
        var resolved = unit is null ? null : Unit.Create(unit).Match<Unit?>(one => one, _ => null);

        return Quantity.Create(amount, resolved).Match(one => one, _ => throw new InvalidOperationException());
    }
}
