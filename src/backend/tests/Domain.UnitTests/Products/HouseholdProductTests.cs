using Domain.Products;
using TestSupport;

namespace Domain.UnitTests.Products;

public class HouseholdProductTests
{
    private const string Barilla = "8076800195057";

    [Fact]
    public void Create_ShouldKeepWhatTheHouseholdSaid_TrimmedAndNormalised()
    {
        // Act
        var product = HouseholdProduct.Create(
            "042100005264", "  Barilla Spaghetti n.5 ", " Spaghetti ", 500m, "G", 9, " Al dente ").ShouldBeSuccess();

        // Assert
        Assert.Equal("0042100005264", product.Gtin.Value);
        Assert.Equal("Barilla Spaghetti n.5", product.Name);
        Assert.Equal("Spaghetti", product.Ingredient);
        Assert.Equal(500m, product.Quantity);
        Assert.Equal("g", product.Unit);
        Assert.Equal(9, product.CookMinutes);
        Assert.Equal("Al dente", product.Note);
    }

    [Fact]
    public void Create_ShouldNeedOnlyAName_AndReadBlankAsNone()
    {
        // Act
        var product = HouseholdProduct.Create(Barilla, "Pasta", " ", null, "", null, "").ShouldBeSuccess();

        // Assert
        Assert.Null(product.Ingredient);
        Assert.Null(product.Unit);
        Assert.Null(product.Note);
    }

    [Fact]
    public void Create_ShouldRefuseABarcodeThatIsNotOne()
    {
        // Act
        var product = HouseholdProduct.Create("123", "Pasta", null, null, null, null, null);

        // Assert
        product.ShouldBeFailure(ProductErrors.InvalidGtin);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ShouldRefuseNoName(string? name)
    {
        // Act
        var product = HouseholdProduct.Create(Barilla, name, null, null, null, null, null);

        // Assert
        product.ShouldBeFailure(ProductErrors.InvalidName);
    }

    [Fact]
    public void Create_ShouldRefuseTextLongerThanTheColumnKeeps()
    {
        // Act
        var name = HouseholdProduct.Create(Barilla, new string('a', 201), null, null, null, null, null);
        var ingredient = HouseholdProduct.Create(Barilla, "Pasta", new string('a', 201), null, null, null, null);
        var note = HouseholdProduct.Create(Barilla, "Pasta", null, null, null, null, new string('a', 501));
        var longest = HouseholdProduct.Create(Barilla, new string('a', 200), new string('a', 200), null, null, null, new string('a', 500));

        // Assert
        name.ShouldBeFailure(ProductErrors.InvalidName);
        ingredient.ShouldBeFailure(ProductErrors.InvalidIngredient);
        note.ShouldBeFailure(ProductErrors.InvalidNote);
        longest.ShouldBeSuccess();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10_000_000)]
    public void Create_ShouldRefuseAQuantityOutsideWhatAPackHolds(int quantity)
    {
        // Act
        var product = HouseholdProduct.Create(Barilla, "Pasta", null, quantity, "g", null, null);

        // Assert
        product.ShouldBeFailure(ProductErrors.InvalidQuantity);
    }

    [Theory]
    [InlineData("500g")]
    [InlineData("a very long unit indeed")]
    public void Create_ShouldRefuseAUnitThatIsNotAWord(string unit)
    {
        // Act
        var product = HouseholdProduct.Create(Barilla, "Pasta", null, 500m, unit, null, null);

        // Assert
        product.ShouldBeFailure(ProductErrors.InvalidUnit);
    }

    [Fact]
    public void Create_ShouldRefuseAUnitWithoutAQuantity()
    {
        // Act
        var product = HouseholdProduct.Create(Barilla, "Pasta", null, null, "g", null, null);

        // Assert
        product.ShouldBeFailure(ProductErrors.InvalidUnit);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(601)]
    [InlineData(-5)]
    public void Create_ShouldRefuseACookingTimeOutsideOneToSixHundredMinutes(int minutes)
    {
        // Act
        var product = HouseholdProduct.Create(Barilla, "Pasta", null, null, null, minutes, null);

        // Assert
        product.ShouldBeFailure(ProductErrors.InvalidCookMinutes);
    }
}
