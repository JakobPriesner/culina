using Domain.Ingredients;

namespace Domain.UnitTests.Ingredients;

public class IngredientLookalikesTests
{
    private static readonly IReadOnlySet<string> NoneKeptApart = new HashSet<string>();

    [Theory]
    [InlineData("Olivenoel", "Olivenöl", LookalikeReason.Spelling)]
    [InlineData("Hühnchenbrustfilet", "Hähnchenbrust", LookalikeReason.SameFood)]
    [InlineData("Origano", "Oregano", LookalikeReason.Typo)]
    [InlineData("Ras el Hanut", "Ras el Hanout", LookalikeReason.Typo)]
    [InlineData("Paprika rot", "rote Paprika", LookalikeReason.Spelling)]
    [InlineData("Spargel, grün", "grüner Spargel", LookalikeReason.Spelling)]
    [InlineData("Grünspargel", "grüner Spargel", LookalikeReason.Spelling)]
    [InlineData("grünen Spargel", "grüner Spargel", LookalikeReason.Spelling)]
    public void Find_ShouldProposeMergingIntoTheMoreUsedName(string rare, string common, LookalikeReason reason)
    {
        // Arrange
        IngredientUse[] uses = [new(rare, 1), new(common, 3)];

        // Act
        var suggestion = Assert.Single(IngredientLookalikes.Find(uses, NoneKeptApart));

        // Assert
        Assert.Equal(rare, suggestion.From.Name);
        Assert.Equal(common, suggestion.To.Name);
        Assert.Equal(reason, suggestion.Reason);
    }

    [Theory]
    [InlineData("Reis", "Mais")]
    [InlineData("Mehl 405", "Mehl 550")]
    [InlineData("Butter", "Buttermilch")]
    [InlineData("Paprika rot", "Paprika gelb")]
    [InlineData("Zitrone", "Zitronensaft")]
    [InlineData("grüner Spargel", "Spargel")]
    [InlineData("weißer Spargel", "Grüner Spargel")]
    [InlineData("Spargel grün", "Spargel")]
    [InlineData("Vollmilch", "Milch")]
    [InlineData("Kirschtomaten", "Tomaten")]
    [InlineData("Kokosmilch light", "Kokosmilch")]
    [InlineData("große Zwiebel", "Zwiebel")]
    [InlineData("gehackte Zwiebeln", "Zwiebeln")]
    public void Find_ShouldLeaveDifferentIngredientsAlone(string one, string other)
    {
        // Act
        var suggestions = IngredientLookalikes.Find([new(one, 1), new(other, 1)], NoneKeptApart);

        // Assert
        Assert.Empty(suggestions);
    }

    [Fact]
    public void Find_ShouldPreferTheShorterName_WhenBothAreUsedAsOften()
    {
        // Act
        var suggestion = Assert.Single(IngredientLookalikes.Find(
            [new("Hähnchenbrustfilet", 2), new("Hähnchenbrust", 2)],
            NoneKeptApart));

        // Assert
        Assert.Equal("Hähnchenbrustfilet", suggestion.From.Name);
        Assert.Equal("Hähnchenbrust", suggestion.To.Name);
    }

    [Theory]
    [InlineData("Tomate", "Tomaten")]
    [InlineData("Zwiebel", "Zwiebeln")]
    [InlineData("rote Zwiebel", "rote Zwiebeln")]
    [InlineData("Champignon", "Champignons")]
    [InlineData("Kartoffel", "Kartoffeln")]
    public void Find_ShouldLeaveSingularAndPluralAlone(string one, string other)
    {
        // Act: merging would rewrite "1 Tomate" as "1 Tomaten", or "4 Tomaten" as "4 Tomate".
        var suggestions = IngredientLookalikes.Find([new(one, 1), new(other, 3)], NoneKeptApart);

        // Assert
        Assert.Empty(suggestions);
    }

    [Fact]
    public void Find_ShouldProposeTheMostUsedName_WhenLookalikesChain()
    {
        // Arrange: "Origano" looks like both; "Origanno" only like "Origano".
        IngredientUse[] uses = [new("Oregano", 5), new("Origano", 2), new("Origanno", 1)];

        // Act
        var suggestions = IngredientLookalikes.Find(uses, NoneKeptApart);

        // Assert
        Assert.All(suggestions, suggestion => Assert.Equal("Oregano", suggestion.To.Name));
        Assert.Equal(["Origano", "Origanno"], suggestions.Select(suggestion => suggestion.From.Name));
    }

    [Fact]
    public void Find_ShouldNotProposeANameTheHouseholdKeptApart()
    {
        // Act
        var suggestions = IngredientLookalikes.Find(
            [new("Hühnchenbrustfilet", 1), new("Hähnchenbrust", 3)],
            new HashSet<string> { "huehnchenbrustfilet" });

        // Assert
        Assert.Empty(suggestions);
    }
}
