using Domain.Nutrition;

namespace Domain.UnitTests.Nutrition;

public class HouseholdFoodsTests
{
    private static readonly Guid Id = Guid.CreateVersion7();

    [Fact]
    public void Create_ShouldWorkOutTheKilojoules_AndLeaveWhatTheLabelDoesNotSayUnknown()
    {
        // Act
        var food = HouseholdFoods.Create(Id, " Kokosmilch light ", Label(kcal: 75m, fat: 7m))
            .Match(one => one, error => throw new InvalidOperationException(error.Code));

        // Assert
        Assert.Equal(HouseholdFoods.CodeOf(Id), food.Code);
        Assert.True(HouseholdFoods.IsOwn(food.Code));
        Assert.Equal("Kokosmilch light", food.NameDe);
        Assert.Equal(314m, food.Per100Grams.EnergyKj);
        Assert.Equal(7m, food.Per100Grams.Fat);
        Assert.Null(food.Per100Grams.Protein);
    }

    [Theory]
    [InlineData("", 75, null, null)]
    [InlineData("Skyr", 0, null, null)]
    [InlineData("Skyr", 901, null, null)]
    [InlineData("Skyr", 60, 101, null)]
    [InlineData("Skyr", 60, 2, 3)]
    public void Create_ShouldRefuse_ALabelNoFoodCouldHave(string name, int kcal, int? fat, int? saturatedFat)
    {
        // Act
        var code = HouseholdFoods.Create(Id, name, Label(kcal, fat, saturatedFat)).Match(_ => "valid", error => error.Code);

        // Assert
        Assert.Equal(NutritionErrors.InvalidFood.Code, code);
    }

    [Fact]
    public void IsOwn_ShouldNotTakeABlsCodeForAHouseholdsFood()
    {
        Assert.False(HouseholdFoods.IsOwn("Q611000"));
    }

    private static Nutrients Label(decimal kcal, decimal? fat = null, decimal? saturatedFat = null) =>
        new(null, kcal, fat, saturatedFat, null, null, null, null);
}
