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

    [Fact]
    public void Find_ShouldProposeTheMostUsedName_WhenLookalikesChain()
    {
        // Arrange: "Tomaten" looks like both; "Tomatn" only like "Tomaten".
        IngredientUse[] uses = [new("Tomate", 5), new("Tomaten", 2), new("Tomatn", 1)];

        // Act
        var suggestions = IngredientLookalikes.Find(uses, NoneKeptApart);

        // Assert
        Assert.All(suggestions, suggestion => Assert.Equal("Tomate", suggestion.To.Name));
        Assert.Equal(["Tomaten", "Tomatn"], suggestions.Select(suggestion => suggestion.From.Name));
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
