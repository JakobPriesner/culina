using Domain.Recipes;
using TestSupport;

namespace Domain.UnitTests.Recipes;

/// <summary>
/// The unit vocabulary is open, and what that costs.
/// </summary>
/// <remarks>
/// A household adds a unit by writing one. The whole safety of that rests on
/// the added unit being a counting unit: it scales, it sums with itself, and it
/// converts to nothing. Anything else would be the arithmetic guessing how much
/// a Schuss weighs.
/// </remarks>
public class UnitTests
{
    [Theory]
    [InlineData("g")]
    [InlineData("tbsp")]
    [InlineData("pinch")]
    public void Create_ShouldReturnTheBuiltIn_WhenTheCodeIsOne(string code)
    {
        // Arrange & Act
        var unit = Unit.Create(code).ShouldBeSuccess();

        // Assert
        Assert.Contains(unit, Unit.BuiltIn);
    }

    [Theory]
    [InlineData("Milliliter", "ml")]
    [InlineData("millilitres", "ml")]
    [InlineData("Liter", "l")]
    [InlineData("Gramm", "g")]
    [InlineData("Kilogramm", "kg")]
    [InlineData("EL", "tbsp")]
    [InlineData("Teelöffel", "tsp")]
    [InlineData("Stk.", "piece")]
    [InlineData("Stück", "piece")]
    [InlineData("Stueck", "piece")]
    [InlineData("Zehen", "clove")]
    [InlineData("Prise", "pinch")]
    public void Create_ShouldReadABuiltInWrittenOut_AsThatBuiltIn(string written, string code)
    {
        // Arrange & Act
        var unit = Unit.Create(written).ShouldBeSuccess();

        // Assert
        // "500 Milliliter" kept as written would be a counting unit: it would
        // never become cups for an imperial kitchen, never sum with "ml" on the
        // shopping list, and read German in an English one.
        Assert.Same(Unit.BuiltIn.Single(one => one.Code == code), unit);
    }

    [Theory]
    [InlineData("Schuss")]
    [InlineData("Handvoll")]
    [InlineData("fl oz")]
    [InlineData("Becher")]
    public void Create_ShouldAcceptAUnitAHouseholdWrote(string code)
    {
        // Arrange & Act
        var unit = Unit.Create(code).ShouldBeSuccess();

        // Assert
        // Kept as it was written: a German noun keeps its capital letter, and
        // the unit is its own label — there is nothing to translate it to.
        Assert.Equal(code, unit.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("200g")]
    [InlineData("1/2")]
    [InlineData("a unit far too long to be one")]
    public void Create_ShouldFail_WhenItIsNotAUnit(string code)
    {
        // Arrange & Act
        var result = Unit.Create(code);

        // Assert
        // Open is not the same as anything at all. "200g" is an amount that
        // lost its space, and accepting it makes a unit nothing can match.
        result.ShouldBeFailure(RecipeErrors.InvalidUnit);
    }

    [Fact]
    public void Create_ShouldTreatTheSameWordAsOneUnit_HoweverItWasCapitalised()
    {
        // Arrange & Act
        var written = Unit.Create("Schuss").ShouldBeSuccess();
        var typedInAHurry = Unit.Create("schuss").ShouldBeSuccess();

        // Assert
        // Otherwise a shopping list grows two lines whose difference nobody
        // can see.
        Assert.Equal(written, typedInAHurry);
    }

    [Fact]
    public void Create_ShouldCollapseWhitespace_SoOneSpellingRemains()
    {
        // Arrange & Act
        var unit = Unit.Create("  fl   oz ").ShouldBeSuccess();

        // Assert
        Assert.Equal("fl oz", unit.Code);
    }

    [Fact]
    public void FamilyOf_ShouldCount_WhenTheUnitIsNotBuiltIn()
    {
        // Arrange
        var unit = Unit.Create("Schuss").ShouldBeSuccess();

        // Act & Assert
        Assert.Equal(UnitFamily.Count, Units.FamilyOf(unit));
    }

    [Fact]
    public void AUnitAHouseholdWrote_ShouldStillScaleWithThePortions()
    {
        // Arrange
        var unit = Unit.Create("Schuss").ShouldBeSuccess();

        // Act & Assert
        // It is the point of adding one. Only a pinch refuses to scale.
        Assert.True(Units.Scales(unit));
    }

    [Fact]
    public void AUnitAHouseholdWrote_ShouldNeverConvertToABuiltInOne()
    {
        // Arrange
        var ownUnit = Quantity.Create(1m, Unit.Create("Schuss").ShouldBeSuccess()).ShouldBeSuccess();
        var millilitres = Quantity.Create(20m, Unit.Millilitre).ShouldBeSuccess();

        // Act & Assert
        // Nobody knows how much a Schuss is, and a list that claimed to would
        // be inventing the number.
        Assert.False(ownUnit.CanCombineWith(millilitres));
        ownUnit.Add(millilitres).ShouldBeFailure(RecipeErrors.IncompatibleUnits);
    }

    [Fact]
    public void AUnitAHouseholdWrote_ShouldAddToItself()
    {
        // Arrange
        var unit = Unit.Create("Schuss").ShouldBeSuccess();
        var one = Quantity.Create(1m, unit).ShouldBeSuccess();
        var two = Quantity.Create(2m, unit).ShouldBeSuccess();

        // Act
        var total = one.Add(two).ShouldBeSuccess();

        // Assert
        Assert.Equal(3m, total.Amount);
        Assert.Equal(unit, total.Unit);
    }

    [Fact]
    public void TwoUnitsAHouseholdWrote_ShouldNotAddToEachOther()
    {
        // Arrange
        var schuss = Quantity.Create(1m, Unit.Create("Schuss").ShouldBeSuccess()).ShouldBeSuccess();
        var handful = Quantity.Create(1m, Unit.Create("Handvoll").ShouldBeSuccess()).ShouldBeSuccess();

        // Act & Assert
        // Counting units only ever add to the identical unit: a splash and a
        // handful is not two of anything.
        Assert.False(schuss.CanCombineWith(handful));
    }
}
