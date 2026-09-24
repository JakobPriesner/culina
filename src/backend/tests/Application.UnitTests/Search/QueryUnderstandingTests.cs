using Application.Search;

namespace Application.UnitTests.Search;

/// <summary>
/// What a query is read to mean — and, as much, what it is not.
/// </summary>
public class QueryUnderstandingTests
{
    [Theory]
    [InlineData("vegetarisch unter 30 Minuten mit Kartoffeln",
        "diet=vegetarian; time=30; ingredient=potato; free=")]
    [InlineData("vegetarisches Abendessen unter 30 Minuten mit Kartoffeln",
        "diet=vegetarian; meal=dinner; time=30; ingredient=potato; free=")]
    // Under-parse, on purpose: "mit" joins two foods in a dish's name.
    [InlineData("Nudeln mit Tomatensoße", "free=Nudeln mit Tomatensoße")]
    [InlineData("Gericht ohne Fleisch", "diet=vegetarian; free=")]
    [InlineData("schnelles Abendessen", "quick=quick; meal=dinner; free=")]
    [InlineData("was kann ich mit Kartoffeln machen?", "ingredient=potato; free=")]
    [InlineData("etwas mit Hähnchen", "ingredient=chicken; free=")]
    [InlineData("what can I make with chicken", "ingredient=chicken; free=")]
    [InlineData("Bolognese", "free=Bolognese")]
    [InlineData("Spaghetti Bolognese", "free=Spaghetti Bolognese")]
    [InlineData("Nachtisch", "meal=dessert; free=")]
    [InlineData("italienisch", "cuisine=italian; free=")]
    [InlineData("Thai Curry", "cuisine=thai; free=Curry")]
    [InlineData("Tomaten -Reis", "exclusion=rice; free=Tomaten")]
    [InlineData("Linsensuppe ohne Zwiebeln und Knoblauch", "exclusion=onion; exclusion=garlic; free=Linsensuppe")]
    [InlineData("ohne Zuckerguss", "exclusion=Zuckerguss; free=")]
    [InlineData("vegetarisch mit Lachs", "diet=vegetarian; ingredient=salmon; free=")]
    [InlineData("Kuchen in einer Stunde", "time=60; free=Kuchen")]
    [InlineData("Suppe in einer halben Stunde", "time=30; free=Suppe")]
    [InlineData("30min Pasta", "time=30; free=Pasta")]
    [InlineData("under 20 minutes", "time=20; free=")]
    [InlineData("glutenfrei", "diet=gluten_free; free=")]
    [InlineData("vegan low carb", "diet=vegan; diet=low_carb; free=")]
    [InlineData("ich suche ein Rezept für Lasagne", "free=Lasagne")]
    [InlineData("Wiener Schnitzel", "free=Wiener Schnitzel")]
    public void Parse_ShouldExtractWhatWasMeant_AndNoMore(string query, string expected)
    {
        // Act
        var intent = QueryUnderstanding.Parse(query);

        // Assert
        Assert.Equal(expected, Summary(intent));
    }

    [Fact]
    public void Parse_ShouldNotTurnQuickIntoATimeFilter()
    {
        // Act
        // "schnell" states an intent, not a number. As thirty minutes it would
        // silently drop the recipe nobody wrote a time on.
        var intent = QueryUnderstanding.Parse("schnell");

        // Assert
        Assert.True(intent.Quick);
        Assert.Null(intent.MaxMinutes);
    }

    [Theory]
    [InlineData("vegetarisch unter 30 Minuten mit Kartoffeln")]
    [InlineData("was kann ich mit Kartoffeln machen?")]
    [InlineData("Linsensuppe ohne Zwiebeln und Knoblauch")]
    [InlineData("Tomaten -Reis")]
    [InlineData("schnelles Abendessen")]
    public void Parse_ShouldReturnTheCharactersEveryChipWasReadFrom(string query)
    {
        // Act
        var intent = QueryUnderstanding.Parse(query);

        // Assert
        Assert.NotEmpty(intent.Applied);
        Assert.All(intent.Applied, one => Assert.Equal(one.Text, query[one.Start..one.End]));
    }

    [Fact]
    public void RemovingAChip_ShouldBeDeletingItsSpan_AndAskingAgain()
    {
        // Arrange
        const string query = "vegetarisch unter 30 Minuten mit Kartoffeln";
        var time = QueryUnderstanding.Parse(query).Of(InferenceKind.Time).Single();

        // Act
        var without = QueryUnderstanding.Parse(query.Remove(time.Start, time.End - time.Start));

        // Assert
        Assert.Equal("diet=vegetarian; ingredient=potato; free=", Summary(without));
    }

    [Fact]
    public void RemovingTheOnlyIngredient_ShouldTakeTheSentenceAroundItWithIt()
    {
        // Arrange
        const string query = "was kann ich mit Kartoffeln machen?";
        var chip = QueryUnderstanding.Parse(query).Applied.Single();

        // Act
        var left = query.Remove(chip.Start, chip.End - chip.Start);

        // Assert
        Assert.Equal("?", left);
        Assert.Equal(string.Empty, QueryUnderstanding.Parse(left).FreeText.Trim('?'));
    }

    [Fact]
    public void Parse_ShouldNameIngredients_AsAnIngredientLineWould()
    {
        // Act
        // The ranking counts ingredients by name, and a line says "Kartoffel".
        var intent = QueryUnderstanding.Parse("etwas mit Kartoffeln");

        // Assert
        Assert.Equal(["Kartoffel"], intent.Ingredients);
    }

    [Fact]
    public void Parse_ShouldLeaveAnEmptyQueryEmpty()
    {
        // Act & Assert
        Assert.Equal("free=", Summary(QueryUnderstanding.Parse(null)));
        Assert.Equal("free=", Summary(QueryUnderstanding.Parse("   ")));
    }

    private static string Summary(QueryIntent intent) =>
        string.Join("; ", intent.Applied
            .Select(one => $"{one.Kind.ToString().ToLowerInvariant()}={one.Value}")
            .Append($"free={intent.FreeText}"));
}
