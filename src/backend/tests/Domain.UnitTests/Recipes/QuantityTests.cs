using Domain.Recipes;
using TestSupport;

namespace Domain.UnitTests.Recipes;

public class QuantityTests
{
    [Fact]
    public void Create_ShouldDropTheUnit_WhenThereIsNoAmount()
    {
        // Arrange & Act
        var quantity = Quantity.Create(amount: null, Unit.Gram).ShouldBeSuccess();

        // Assert
        // "g of butter" is not a measurement, so a unit without an amount is
        // no measurement at all.
        Assert.False(quantity.IsMeasured);
        Assert.Null(quantity.Unit);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.5)]
    public void Create_ShouldFail_WhenTheAmountIsNotPositive(double amount)
    {
        // Arrange & Act
        var result = Quantity.Create((decimal)amount, Unit.Gram);

        // Assert
        result.ShouldBeFailure(RecipeErrors.InvalidQuantity);
    }

    [Fact]
    public void Add_ShouldConvertWithinAFamily_WhenSummingGramsAndKilograms()
    {
        // Arrange
        var grams = Quantity.Create(200m, Unit.Gram).ShouldBeSuccess();
        var kilograms = Quantity.Create(1.5m, Unit.Kilogram).ShouldBeSuccess();

        // Act
        var total = grams.Add(kilograms).ShouldBeSuccess();

        // Assert
        Assert.Equal(1700m, total.Amount);
        Assert.Equal(Unit.Gram, total.Unit);
    }

    [Fact]
    public void Add_ShouldRefuse_WhenTheUnitsAreInDifferentFamilies()
    {
        // Arrange
        var grams = Quantity.Create(200m, Unit.Gram).ShouldBeSuccess();
        var millilitres = Quantity.Create(200m, Unit.Millilitre).ShouldBeSuccess();

        // Act
        var result = grams.Add(millilitres);

        // Assert
        result.ShouldBeFailure(RecipeErrors.IncompatibleUnits);
    }

    [Fact]
    public void Add_ShouldRefuseToConvertSpoonsToVolume_BecauseASpoonIsNotAFixedSize()
    {
        // Arrange
        var spoons = Quantity.Create(2m, Unit.Tablespoon).ShouldBeSuccess();
        var millilitres = Quantity.Create(30m, Unit.Millilitre).ShouldBeSuccess();

        // Act
        var result = spoons.Add(millilitres);

        // Assert
        // A US tablespoon is 14.8 ml, a metric one is 15 ml, an Australian one
        // is 20 ml. Converting would invent precision the recipe never had.
        result.ShouldBeFailure(RecipeErrors.IncompatibleUnits);
    }

    [Fact]
    public void Add_ShouldCombineSpoons_WhenBothAreTheSameSpoon()
    {
        // Arrange
        var first = Quantity.Create(1m, Unit.Tablespoon).ShouldBeSuccess();
        var second = Quantity.Create(2m, Unit.Tablespoon).ShouldBeSuccess();

        // Act
        var total = first.Add(second).ShouldBeSuccess();

        // Assert
        Assert.Equal(3m, total.Amount);
        Assert.Equal(Unit.Tablespoon, total.Unit);
    }

    [Fact]
    public void Add_ShouldRefuse_WhenCountUnitsDiffer()
    {
        // Arrange
        var cloves = Quantity.Create(3m, Unit.Clove).ShouldBeSuccess();
        var bunches = Quantity.Create(2m, Unit.Bunch).ShouldBeSuccess();

        // Act
        var result = cloves.Add(bunches);

        // Assert
        // Three cloves and two bunches is not five of anything.
        result.ShouldBeFailure(RecipeErrors.IncompatibleUnits);
    }

    [Fact]
    public void Add_ShouldCombineBareCounts_WhenNeitherHasAUnit()
    {
        // Arrange
        var two = Quantity.Create(2m, unit: null).ShouldBeSuccess();
        var three = Quantity.Create(3m, unit: null).ShouldBeSuccess();

        // Act
        var total = two.Add(three).ShouldBeSuccess();

        // Assert
        Assert.Equal(5m, total.Amount);
        Assert.Null(total.Unit);
    }

    [Fact]
    public void Add_ShouldLeaveTheSumUnrounded_SoMergingDoesNotCompoundError()
    {
        // Arrange
        var first = Quantity.Create(0.1m, Unit.Litre).ShouldBeSuccess();
        var second = Quantity.Create(0.2m, Unit.Litre).ShouldBeSuccess();

        // Act
        var total = first.Add(second).ShouldBeSuccess();

        // Assert
        Assert.Equal(300m, total.Amount);
    }

    [Fact]
    public void Scales_ShouldBeFalseForAPinch_BecauseAPinchIsAGestureNotAMeasurement()
    {
        // Arrange
        var pinch = Quantity.Create(1m, Unit.Pinch).ShouldBeSuccess();
        var grams = Quantity.Create(1m, Unit.Gram).ShouldBeSuccess();

        // Act & Assert
        Assert.False(pinch.Scales);
        Assert.True(grams.Scales);
    }

    [Fact]
    public void Unmeasured_ShouldNotScale_BecauseThereIsNothingToMultiply()
    {
        // Arrange & Act
        var salt = Quantity.Unmeasured;

        // Assert
        Assert.False(salt.Scales);
        Assert.False(salt.IsMeasured);
    }
}
