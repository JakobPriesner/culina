using Domain.Products;
using TestSupport;

namespace Domain.UnitTests.Products;

public class GtinTests
{
    [Theory]
    [InlineData("8076800195057", "8076800195057")]
    [InlineData(" 8076800195057 ", "8076800195057")]
    [InlineData("96385074", "96385074")]
    [InlineData("10012345678902", "10012345678902")]
    public void Create_ShouldKeepAValidEan8Ean13OrGtin14AsItIs(string code, string expected)
    {
        // Act
        var gtin = Gtin.Create(code).ShouldBeSuccess();

        // Assert
        Assert.Equal(expected, gtin.Value);
    }

    [Fact]
    public void Create_ShouldPadAUpcAToThirteenDigits_SoItMeetsTheEan13OfTheSameProduct()
    {
        // Act
        var upcA = Gtin.Create("042100005264").ShouldBeSuccess();
        var ean = Gtin.Create("0042100005264").ShouldBeSuccess();

        // Assert
        Assert.Equal("0042100005264", upcA.Value);
        Assert.Equal(ean, upcA);
    }

    [Theory]
    [InlineData("04252614", "0042100005264")] // the sixth digit decides where the six land: 0 to 2
    [InlineData("00123430", "0001200000340")] // 3
    [InlineData("00123444", "0001230000044")] // 4
    public void Create_ShouldExpandAUpcEToItsUpcA_SoTheSymbologyReadDoesNotChooseTheRow(string upcE, string expected)
    {
        // Act
        var gtin = Gtin.Create(upcE).ShouldBeSuccess();

        // Assert
        Assert.Equal(expected, gtin.Value);
    }

    [Fact]
    public void Create_ShouldReadAnEightDigitCodeThatFitsBothSymbologiesAsAnEan8()
    {
        // Arrange: with a sixth digit of 5 to 9 a UPC-E's check digit is the EAN-8 sum too; the digits alone cannot say which it was.
        const string both = "01234565";

        // Act
        var gtin = Gtin.Create(both).ShouldBeSuccess();

        // Assert
        Assert.Equal(both, gtin.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("807680019505")] // 12 digits, wrong check digit
    [InlineData("8076800195058")] // wrong check digit
    [InlineData("8076 8001 9505 7")]
    [InlineData("80768001950AB")]
    [InlineData("123456789")] // a length no grocery code has
    [InlineData("123456789012345")]
    [InlineData("96385075")] // EAN-8 with a wrong check digit that is no UPC-E either
    public void Create_ShouldRefuseWhatIsNotABarcode(string code)
    {
        // Act
        var gtin = Gtin.Create(code);

        // Assert
        gtin.ShouldBeFailure(ProductErrors.InvalidGtin);
    }

    [Fact]
    public void Create_ShouldRefuseNull()
    {
        // Act
        var gtin = Gtin.Create(null);

        // Assert
        gtin.ShouldBeFailure(ProductErrors.InvalidGtin);
    }
}
