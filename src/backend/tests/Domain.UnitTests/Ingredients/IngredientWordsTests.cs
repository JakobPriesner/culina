using Domain.Ingredients;

namespace Domain.UnitTests.Ingredients;

public class IngredientWordsTests
{
    [Theory]
    [InlineData("grüner Spargel", "spargel", "green")]
    [InlineData("Spargel, grün", "spargel", "green")]
    [InlineData("Spargel grün", "spargel", "green")]
    [InlineData("grünen Spargel", "spargel", "green")]
    [InlineData("Grünspargel", "spargel", "green")]
    [InlineData("weißer Spargel", "spargel", "white")]
    [InlineData("Kokosmilch light", "kokosmilch", "light")]
    [InlineData("Kokosmilch (light)", "kokosmilch", "light")]
    [InlineData("TK-Erbsen", "erbsen", "frozen")]
    [InlineData("Kichererbsen aus der Dose", "kichererbsen", "canned")]
    [InlineData("fettarme Milch", "milch", "reduced_fat")]
    public void Read_ShouldTellTheThingFromWhatChangesWhichOneYouBuy(string name, string head, string qualifier)
    {
        // Act
        var reading = IngredientWords.Read(name);

        // Assert
        Assert.Equal([head], reading.Head);
        Assert.Equal([qualifier], reading.Qualifiers);
    }

    [Theory]
    [InlineData("große Zwiebeln", "large")]
    [InlineData("Bio-Eier", "organic")]
    [InlineData("frische Petersilie", "fresh")]
    public void Read_ShouldSetAsideWordsThatChangeNeither(string name, string descriptor)
    {
        // Act
        var reading = IngredientWords.Read(name);

        // Assert
        Assert.Equal([descriptor], reading.Descriptors);
        Assert.Empty(reading.Qualifiers);
    }

    [Theory]
    [InlineData("gehackte Zwiebeln", "zwiebeln")]
    [InlineData("weiche Butter", "butter")]
    [InlineData("Butter für die Form", "butter")]
    [InlineData("Petersilie zum Bestreuen", "petersilie")]
    [InlineData("Zucker nach Belieben", "zucker")]
    public void Read_ShouldFindThePreparationThatBelongsInTheNote(string name, string head)
    {
        // Act
        var reading = IngredientWords.Read(name);

        // Assert
        Assert.Equal([head], reading.Head);
        Assert.NotEmpty(reading.Preparation);
    }

    [Theory]
    [InlineData("gehackte Tomaten")]
    [InlineData("passierte Tomaten")]
    [InlineData("gemahlene Mandeln")]
    [InlineData("gemischtes Gehacktes")]
    public void Read_ShouldKeepAProductWhole_WhenTheWholeNameIsAnotherFood(string name)
    {
        // Act: "gehackte Tomaten" is a can, not chopped tomatoes.
        var reading = IngredientWords.Read(name);

        // Assert
        Assert.Empty(reading.Preparation);
        Assert.True(reading.Head.Count >= 2 || reading.Qualifiers.Count > 0, string.Join(' ', reading.Head));
    }

    [Theory]
    [InlineData("Gehacktes")]
    [InlineData("Bio")]
    public void Read_ShouldTakeANameOfOnlyDescribingWords_AsTheThing(string name)
    {
        // Act
        var reading = IngredientWords.Read(name);

        // Assert
        Assert.NotEmpty(reading.Head);
    }

    [Theory]
    [InlineData("Schwarzwälder Schinken")]
    [InlineData("Rotisserie")]
    public void Read_ShouldNotSplitACompound_WhoseRestIsNoFood(string name)
    {
        // Act
        var reading = IngredientWords.Read(name);

        // Assert
        Assert.Empty(reading.Qualifiers);
    }

    [Theory]
    [InlineData("grüner Spargel", "Spargel, grün")]
    [InlineData("Grünspargel", "grüner Spargel")]
    [InlineData("Tomate", "Tomaten")]
    [InlineData("große Zwiebeln", "Zwiebel")]
    [InlineData("gehackte Zwiebeln", "Zwiebeln")]
    [InlineData("Kokosmilch, light", "light Kokosmilch")]
    public void IsSameIngredient_ShouldHold_ForOneThingToBuy(string one, string other)
    {
        Assert.True(IngredientWords.Read(one).IsSameIngredient(IngredientWords.Read(other)));
    }

    [Theory]
    [InlineData("grüner Spargel", "Spargel")]
    [InlineData("grüner Spargel", "weißer Spargel")]
    [InlineData("Kokosmilch light", "Kokosmilch")]
    [InlineData("gehackte Tomaten", "Tomaten")]
    [InlineData("Butter", "Butt")]
    [InlineData("Ei", "Eis")]
    public void IsSameIngredient_ShouldNotHold_ForTwoThingsToBuy(string one, string other)
    {
        Assert.False(IngredientWords.Read(one).IsSameIngredient(IngredientWords.Read(other)));
    }

    [Theory]
    [InlineData("grün", true)]
    [InlineData("light", true)]
    [InlineData("aus der Dose", true)]
    [InlineData("weiß, geschält", false)]
    [InlineData("fein gehackt", false)]
    [InlineData("", false)]
    public void AreAllQualifiers_ShouldTellAVariantFromAPreparation(string tail, bool expected)
    {
        Assert.Equal(expected, IngredientWords.AreAllQualifiers(tail));
    }

    [Theory]
    [InlineData("Kokosmilch light", "Kokosmilch, light")]
    [InlineData("Kokosmilch (light)", "Kokosmilch, light")]
    [InlineData("grüner Spargel", "Spargel, grün")]
    [InlineData("Rote Zwiebeln", "Zwiebeln, rot")]
    [InlineData("große Zwiebeln", "Zwiebeln")]
    [InlineData("Kichererbsen aus der Dose", "Kichererbsen, aus der Dose")]
    [InlineData("TK-Erbsen", "Erbsen, TK")]
    [InlineData("Grünspargel", "Grünspargel")]
    [InlineData("Thai-Basilikum", "Thai-Basilikum")]
    [InlineData("gehackte Tomaten", "gehackte Tomaten")]
    public void Wording_ShouldSayWhatWasUnderstood_InTheWordsTyped(string name, string understood)
    {
        // Act
        var wording = IngredientWords.Wording(name);

        // Assert
        Assert.Equal(understood, wording.Understood);
    }

    [Theory]
    [InlineData("gehackte Zwiebeln", "Zwiebeln", "gehackt")]
    [InlineData("fein gehackte rote Zwiebeln", "rote Zwiebeln", "fein gehackt")]
    [InlineData("Zimmerwarme Butter", "Butter", "zimmerwarm")]
    [InlineData("geschälte Kartoffeln, gewürfelt", "Kartoffeln", "geschält, gewürfelt")]
    [InlineData("Butter für die Form", "Butter", "für die Form")]
    [InlineData("Zucker nach Belieben", "Zucker", "nach Belieben")]
    [InlineData("chopped onions", "onions", "chopped")]
    public void Wording_ShouldOfferThePreparationForTheNote(string name, string without, string note)
    {
        // Act
        var wording = IngredientWords.Wording(name);

        // Assert
        Assert.Equal(without, wording.Name);
        Assert.Equal(note, wording.Note);
    }

    [Theory]
    [InlineData("Zwiebeln")]
    [InlineData("gehackte Tomaten")]
    [InlineData("gemischtes Gehacktes")]
    [InlineData("große Zwiebeln")]
    public void Wording_ShouldOfferNothingForTheNote_WhenNothingIsDoneToIt(string name)
    {
        // Act
        var wording = IngredientWords.Wording(name);

        // Assert
        Assert.Null(wording.Name);
        Assert.Null(wording.Note);
    }
}
