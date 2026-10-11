using Domain.Nutrition;

namespace Domain.UnitTests.Nutrition;

public class HouseholdWeightTests
{
    [Theory]
    [InlineData("Stück", "piece")]
    [InlineData("EL", "tbsp")]
    [InlineData("Päckchen", "pack")]
    [InlineData("Handvoll", "handvoll")]
    public void Create_ShouldKeyTheWeightByTheCanonicalUnit(string unit, string key)
    {
        // Act
        var weight = HouseholdWeight.Create(unit, 150m);

        // Assert
        Assert.True(Succeeds(weight));
        Assert.Equal(new HouseholdWeight(key, 150m), weight.Match(one => one, _ => null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(10000.5)]
    [InlineData(20000)]
    public void Create_ShouldRefuseGramsOutsideTheRange(double grams)
    {
        // Act
        var weight = HouseholdWeight.Create("piece", (decimal)grams);

        // Assert
        Assert.Equal(NutritionErrors.InvalidGrams, weight.Match(_ => null!, error => error));
    }

    [Theory]
    [InlineData("g")]
    [InlineData("Kilogramm")]
    [InlineData("ml")]
    [InlineData("l")]
    [InlineData("cup")]
    [InlineData("fl oz")]
    public void Create_ShouldRefuseAUnitThatHasASizeOfItsOwn(string unit)
    {
        // Act
        var weight = HouseholdWeight.Create(unit, 100m);

        // Assert
        Assert.Equal(NutritionErrors.UnitHasASize, weight.Match(_ => null!, error => error));
    }

    [Fact]
    public void Create_ShouldRefuseAUnitThatIsNotAWord()
    {
        // Act & Assert
        Assert.False(Succeeds(HouseholdWeight.Create("200g", 100m)));
        Assert.False(Succeeds(HouseholdWeight.Create(" ", 100m)));
        Assert.False(Succeeds(HouseholdWeight.Create(null, 100m)));
    }

    [Fact]
    public void Create_ShouldAcceptTheMostAUnitMayWeigh()
    {
        // Act & Assert
        Assert.True(Succeeds(HouseholdWeight.Create("piece", NutritionGrams.MostGramsPerUnit)));
    }

    private static bool Succeeds(Domain.Shared.Result<HouseholdWeight> result) => result.Match(_ => true, _ => false);
}
