using Application.Recipes;
using Contracts.Recipes;
using Domain.Recipes;
using Domain.Shared;

namespace IntegrationTests.Recipes;

/// <summary>
/// The wire vocabulary, the reader and the writer must agree.
/// </summary>
/// <remarks>
/// They did not: the OpenAPI document advertised <c>gram</c> while the API only
/// ever accepted <c>g</c>, so a client generated from the contract could not
/// save an ingredient. Nothing caught it, because each list was written
/// separately and each was internally consistent.
/// </remarks>
public class RecipeVocabularyTests
{
    /// <summary>Match is the only way to observe a Result, so this is it.</summary>
    private static bool Rejected<TValue>(Result<TValue> result)
        where TValue : notnull =>
        result.Match(_ => false, _ => true);

    [Fact]
    public void EveryPublishedUnit_ShouldBeAcceptedOnAWrite()
    {
        // Arrange & Act
        var rejected = RecipeVocabulary.Units
            .Where(code => Rejected(RecipeWords.ToQuantity(1m, code)))
            .ToList();

        // Assert
        Assert.Empty(rejected);
    }

    [Fact]
    public void ThePublishedUnits_ShouldBeExactlyTheOnesBuiltIn()
    {
        // Arrange & Act
        var written = Unit.BuiltIn.Select(unit => unit.Code).ToHashSet();

        // Assert
        // The published list is what a household starts with, not what it is
        // limited to — but a built-in the document forgets is a built-in no
        // picker ever offers.
        Assert.Equal(written, RecipeVocabulary.Units.ToHashSet());
    }

    [Theory]
    [InlineData("Schuss")]
    [InlineData("Handvoll")]
    [InlineData("fl oz")]
    public void AUnitAHouseholdWrites_ShouldBeAccepted(string code)
    {
        // Arrange & Act
        var result = RecipeWords.ToQuantity(1m, code);

        // Assert
        // The vocabulary is open: writing a unit is how a unit is added.
        Assert.False(Rejected(result));
    }

    [Theory]
    [InlineData("200g")]
    [InlineData("2 1/2")]
    [InlineData("a very long unit indeed")]
    public void SomethingThatIsNotAUnit_ShouldBeRejected(string code)
    {
        // Arrange & Act
        var result = RecipeWords.ToQuantity(1m, code);

        // Assert
        // Open is not the same as anything. "200g" is an amount that lost its
        // space, and accepting it would make a unit nobody could match again.
        Assert.True(Rejected(result));
    }

    [Fact]
    public void EveryPublishedYieldKind_ShouldBeAcceptedOnAWrite()
    {
        // Arrange & Act
        var rejected = RecipeVocabulary.YieldKinds
            .Where(code => Rejected(RecipeWords.ToYieldKind(code)))
            .ToList();

        // Assert
        Assert.Empty(rejected);
    }

    [Fact]
    public void EveryPublishedLanguage_ShouldBeAcceptedOnAWrite()
    {
        // Arrange & Act
        var rejected = RecipeVocabulary.Languages
            .Where(code => Rejected(RecipeWords.ToLanguage(code)))
            .ToList();

        // Assert
        Assert.Empty(rejected);
    }
}
