using Domain.Recipes;
using TestSupport;

namespace Domain.UnitTests.Recipes;

/// <summary>
/// The unit vocabulary is open: an added unit must be a counting unit (scales, sums with itself,
/// converts to nothing).
/// </summary>
public class UnitTests
{
    [Theory]
    [InlineData("g")]
    [InlineData("tbsp")]
    [InlineData("pinch")]
    public void Create_ShouldReturnTheBuiltIn_WhenTheCodeIsOne(string code)
    {
        var unit = Unit.Create(code).ShouldBeSuccess();

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
        var unit = Unit.Create(written).ShouldBeSuccess();

        // "500 Milliliter" as a counting unit would never become cups or sum with "ml".
        Assert.Same(Unit.BuiltIn.Single(one => one.Code == code), unit);
    }

    [Theory]
    [InlineData("Schuss")]
    [InlineData("Handvoll")]
    [InlineData("fl oz")]
    [InlineData("Becher")]
    public void Create_ShouldAcceptAUnitAHouseholdWrote(string code)
    {
        var unit = Unit.Create(code).ShouldBeSuccess();

        // Kept as written: a German noun keeps its capital and has nothing to translate to.
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
        var result = Unit.Create(code);

        // Open is not "anything": "200g" is an amount that lost its space.
        result.ShouldBeFailure(RecipeErrors.InvalidUnit);
    }

    [Fact]
    public void Create_ShouldTreatTheSameWordAsOneUnit_HoweverItWasCapitalised()
    {
        var written = Unit.Create("Schuss").ShouldBeSuccess();
        var typedInAHurry = Unit.Create("schuss").ShouldBeSuccess();

        // Otherwise a shopping list grows two lines whose difference nobody can see.
        Assert.Equal(written, typedInAHurry);
    }

    [Fact]
    public void Create_ShouldCollapseWhitespace_SoOneSpellingRemains()
    {
        var unit = Unit.Create("  fl   oz ").ShouldBeSuccess();

        Assert.Equal("fl oz", unit.Code);
    }

    [Fact]
    public void FamilyOf_ShouldCount_WhenTheUnitIsNotBuiltIn()
    {
        var unit = Unit.Create("Schuss").ShouldBeSuccess();

        Assert.Equal(UnitFamily.Count, Units.FamilyOf(unit));
    }

    [Fact]
    public void AUnitAHouseholdWrote_ShouldStillScaleWithThePortions()
    {
        var unit = Unit.Create("Schuss").ShouldBeSuccess();

        Assert.True(Units.Scales(unit));
    }

    [Fact]
    public void AUnitAHouseholdWrote_ShouldNeverConvertToABuiltInOne()
    {
        var ownUnit = Quantity.Create(1m, Unit.Create("Schuss").ShouldBeSuccess()).ShouldBeSuccess();
        var millilitres = Quantity.Create(20m, Unit.Millilitre).ShouldBeSuccess();

        // Nobody knows how much a Schuss is; claiming to would invent a number.
        Assert.False(ownUnit.CanCombineWith(millilitres));
        ownUnit.Add(millilitres).ShouldBeFailure(RecipeErrors.IncompatibleUnits);
    }

    [Fact]
    public void AUnitAHouseholdWrote_ShouldAddToItself()
    {
        var unit = Unit.Create("Schuss").ShouldBeSuccess();
        var one = Quantity.Create(1m, unit).ShouldBeSuccess();
        var two = Quantity.Create(2m, unit).ShouldBeSuccess();

        var total = one.Add(two).ShouldBeSuccess();

        Assert.Equal(3m, total.Amount);
        Assert.Equal(unit, total.Unit);
    }

    [Fact]
    public void TwoUnitsAHouseholdWrote_ShouldNotAddToEachOther()
    {
        var schuss = Quantity.Create(1m, Unit.Create("Schuss").ShouldBeSuccess()).ShouldBeSuccess();
        var handful = Quantity.Create(1m, Unit.Create("Handvoll").ShouldBeSuccess()).ShouldBeSuccess();

        // Counting units only add to the identical unit.
        Assert.False(schuss.CanCombineWith(handful));
    }
}
