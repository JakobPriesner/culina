using Domain.Nutrition;

namespace Domain.UnitTests.Nutrition;

public class TypicalWeightsTests
{
    [Fact]
    public void All_ShouldHaveOneRowPerFoodAndUnit_WithAPositiveWeightAndASource()
    {
        // Act
        var keys = TypicalWeights.All.Select(row => (row.Code, row.UnitKey)).ToList();

        // Assert
        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.All(TypicalWeights.All, row =>
        {
            Assert.InRange(row.Grams, 0.01m, NutritionGrams.MostGramsPerUnit);
            Assert.False(string.IsNullOrWhiteSpace(row.Source));
        });
    }

    [Fact]
    public void Find_ShouldReturnTheRow_AndNullForAnUnknownFoodOrUnit()
    {
        // Act & Assert
        Assert.Equal(110m, TypicalWeights.Find("G480100", "piece")!.Grams);
        Assert.Null(TypicalWeights.Find("G480100", "bunch"));
        Assert.Null(TypicalWeights.Find("NOPE", "piece"));
    }

    [Fact]
    public void Find_ShouldHaveNoPieceOfGarlic_BecauseABareGarlicCanBeABulb()
    {
        // Act & Assert
        Assert.Equal(3m, TypicalWeights.Find("G490100", "clove")!.Grams);
        Assert.Null(TypicalWeights.Find("G490100", "piece"));
    }

    [Fact]
    public void Find_ShouldCountACanOfFreshTomatoesAsCanned()
    {
        // Act
        var row = TypicalWeights.Find("G561100", "can")!;

        // Assert
        Assert.Equal("G568900", row.CountAs);
        Assert.Equal(400m, row.Grams);
    }
}
