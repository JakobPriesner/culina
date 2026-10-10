using Domain.Nutrition;
using Domain.Recipes;

namespace Domain.UnitTests.Nutrition;

public class NutritionGramsTests
{
    private static readonly FoodName Flour = new("C214100", ["Mehl"], ["flour"], Density: null, EggPart.None, "Mehl", "flour");
    private static readonly FoodName Onion = new("G480100", ["Zwiebel"], ["onion"], Density: null, EggPart.None, "Zwiebel", "onion");
    private static readonly FoodName Salt = new("R111000", ["Salz"], ["salt"], Density: null, EggPart.None, "Salz", "salt");
    private static readonly FoodName Milk = new("M111300", ["Milch"], ["milk"], Density: 1.031m, EggPart.None, "Milch", "milk");
    private static readonly FoodName Oil = new("Q120000", ["Olivenöl"], ["olive oil"], Density: 0.913m, EggPart.None, "Olivenöl", "olive oil");
    private static readonly FoodName Egg = new("E111100", ["Ei"], ["egg"], Density: null, EggPart.Whole, "Ei", "egg");
    private static readonly FoodName Yolk = new("E112100", ["Eigelb"], ["egg yolk"], Density: null, EggPart.Yolk, "Eigelb", "egg yolk");
    private static readonly FoodName White = new("E113100", ["Eiweiß"], ["egg white"], Density: null, EggPart.White, "Eiweiß", "egg white");

    private static readonly FoodName Broth = new(
        "R821000",
        ["Gemüsebrühpulver"],
        ["vegetable stock powder"],
        Density: null,
        EggPart.None,
        "Gemüsebrühpulver",
        "vegetable stock powder",
        new LiquidFood("X416243", 0.934m, "Gemüsebrühe (flüssig)", "vegetable stock (liquid)"));

    [Theory]
    [InlineData(500, "ml")]
    [InlineData(0.5, "l")]
    [InlineData(1, "cup")]
    [InlineData(2, "fl oz")]
    [InlineData(500, "g")]
    [InlineData(50.1, "g")]
    [InlineData(1, "kg")]
    public void Resolve_ShouldTakeTheLiquid_ForAVolumeAndForMoreThanFiftyGrams(double amount, string unit)
    {
        // Act
        var resolved = NutritionGrams.Resolve(Measured((decimal)amount, unit), Broth);

        // Assert
        Assert.Equal("X416243", resolved.Code);
        Assert.Equal("Gemüsebrühe (flüssig)", resolved.LabelDe);
        Assert.Equal(0.934m, resolved.Density);
    }

    [Theory]
    [InlineData(4, "g")]
    [InlineData(50, "g")]
    [InlineData(1, "tsp")]
    [InlineData(1, "tbsp")]
    [InlineData(1, "piece")]
    [InlineData(1, "Würfel")]
    public void Resolve_ShouldTakeThePowder_ForFewGramsSpoonsAndCounts(double amount, string unit)
    {
        // Act
        var resolved = NutritionGrams.Resolve(Measured((decimal)amount, unit), Broth);

        // Assert
        Assert.Equal(Broth, resolved);
    }

    [Fact]
    public void Resolve_ShouldTakeThePowder_WhenThereIsNoUnit()
    {
        // Act & Assert
        Assert.Equal(Broth, NutritionGrams.Resolve(Quantity.Unmeasured, Broth));
    }

    [Fact]
    public void Resolve_ShouldReturnTheEntry_WhenItHasNoLiquid()
    {
        // Act & Assert
        Assert.Equal(Milk, NutritionGrams.Resolve(Measured(500m, "g"), Milk));
    }

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
    [InlineData("tbsp", GramsRefusal.SpoonOfSolid)]
    [InlineData("tsp", GramsRefusal.SpoonOfSolid)]
    [InlineData("ml", GramsRefusal.VolumeOfSolid)]
    [InlineData("l", GramsRefusal.VolumeOfSolid)]
    [InlineData("cup", GramsRefusal.VolumeOfSolid)]
    [InlineData("fl oz", GramsRefusal.VolumeOfSolid)]
    public void Read_ShouldNotCountAVolumeOrSpoon_WhenTheFoodHasNoDensity(string unit, GramsRefusal reason)
    {
        // Act
        var reading = NutritionGrams.Read(Measured(1m, unit), Flour);

        // Assert
        Assert.Null(reading.Grams);
        Assert.Equal(reason, reading.Refusal);
    }

    [Theory]
    [InlineData("cup", 236.588)]
    [InlineData("cups", 236.588)]
    [InlineData("Cup", 236.588)]
    [InlineData("CUPS", 236.588)]
    [InlineData("fl oz", 29.5735)]
    [InlineData("fl. oz", 29.5735)]
    [InlineData("FL OZ", 29.5735)]
    public void Read_ShouldCountAUsMeasureLikeMillilitres_WhenTheFoodPours(string unit, double millilitres)
    {
        // Act
        var reading = NutritionGrams.Read(Measured(2m, unit), Milk);

        // Assert
        Assert.Equal(2m * (decimal)millilitres * 1.031m, reading.Grams);
        Assert.Equal(GramsBasis.Density, reading.Basis);
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
        Assert.Equal(GramsRefusal.HouseholdUnit, reading.Refusal);
    }

    [Fact]
    public void Read_ShouldNeverCountAnOnionByPiece()
    {
        // Act
        var bare = NutritionGrams.Read(Measured(1m, unit: null), Onion);
        var pieces = NutritionGrams.Read(Measured(1m, "piece"), Onion);

        // Assert
        Assert.Null(bare.Grams);
        Assert.Equal(GramsRefusal.Count, bare.Refusal);
        Assert.Null(pieces.Grams);
        Assert.Equal(GramsRefusal.Count, pieces.Refusal);
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
        Assert.Equal(GramsRefusal.HouseholdUnit, reading.Refusal);
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
