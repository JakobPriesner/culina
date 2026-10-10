using Domain.Import;
using Domain.Recipes;

namespace Domain.UnitTests.Import;

public class YieldTextTests
{
    [Theory]
    [InlineData("4", 4, YieldKind.Servings, null)]
    [InlineData("4 Portionen", 4, YieldKind.Servings, null)]
    [InlineData("Für 4 Personen", 4, YieldKind.Servings, null)]
    [InlineData("4 SERVINGS", 4, YieldKind.Servings, null)]
    [InlineData("Serves 4", 4, YieldKind.Servings, null)]
    [InlineData("Serves 4 people", 4, YieldKind.Servings, null)]
    [InlineData("1.5 servings", 1.5, YieldKind.Servings, null)]
    [InlineData("1,5 Portionen", 1.5, YieldKind.Servings, null)]
    [InlineData("12 Stück", 12, YieldKind.Pieces, null)]
    [InlineData("12 stk.", 12, YieldKind.Pieces, null)]
    [InlineData("20 pieces", 20, YieldKind.Pieces, null)]
    [InlineData("24 Plätzchen", 24, YieldKind.Pieces, "Plätzchen")]
    [InlineData("Makes 24 cookies", 24, YieldKind.Pieces, "cookies")]
    [InlineData("makes about 12 Muffins", 12, YieldKind.Pieces, "Muffins")]
    [InlineData("Ergibt 8 Brötchen", 8, YieldKind.Pieces, "Brötchen")]
    [InlineData("1 loaf", 1, YieldKind.Pieces, "loaf")]
    [InlineData("2 Loaves", 2, YieldKind.Pieces, "Loaves")]
    [InlineData("1 Kuchen (26 cm)", 1, YieldKind.Pieces, "Kuchen (26 cm)")]
    [InlineData("1 Blech", 1, YieldKind.Pieces, "Blech")]
    [InlineData("2 Gläser", 2, YieldKind.Pieces, "Gläser")]
    [InlineData("1 Brot", 1, YieldKind.Pieces, "Brot")]
    [InlineData("6-8 servings", 8, YieldKind.Servings, null)]
    [InlineData("4 – 6 Portionen", 6, YieldKind.Servings, null)]
    [InlineData("4 to 6", 6, YieldKind.Servings, null)]
    [InlineData("20 bis 24 Stück", 24, YieldKind.Pieces, null)]
    // Something with a number but no word this knows: the number, as imports always read it.
    [InlineData("4 tomatoes", 4, YieldKind.Servings, null)]
    [InlineData("4 (large)", 4, YieldKind.Servings, null)]
    public void Read_ShouldReadWhatTheRecipeMakes(string written, decimal amount, YieldKind kind, string? label)
    {
        var made = YieldText.Read(written);

        Assert.NotNull(made);
        Assert.Equal(amount, made.Amount);
        Assert.Equal(kind, made.Kind);
        Assert.Equal(label, made.Label);
    }

    [Theory]
    [InlineData("a dozen cookies")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("lots")]
    [InlineData("0 servings")]
    [InlineData("5000 Stück")]
    public void Read_ShouldLeaveWhatItCannotCountAlone(string? written) =>
        Assert.Null(YieldText.Read(written));

    [Fact]
    public void Read_ShouldKeepJustTheWord_WhenTheWholeTextIsTooLongForALabel()
    {
        var made = YieldText.Read("1 Kuchen (springform, 26 cm, hoch, mit Rand und Deckel)");

        Assert.Equal("Kuchen", made!.Label);
    }

    [Theory]
    [InlineData("12", "12 muffins")]
    [InlineData("12 muffins", "12")]
    public void Read_ShouldPreferTheEntryThatSaysWhatItMakes(string first, string second)
    {
        var made = YieldText.Read([first, second]);

        Assert.Equal(YieldKind.Pieces, made!.Kind);
        Assert.Equal("muffins", made.Label);
    }

    [Fact]
    public void Read_ShouldTakeTheFirstEntry_WhenNoneNamesAnything()
    {
        Assert.Equal(6, YieldText.Read(["6", "8"])!.Amount);
    }
}
