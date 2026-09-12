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
    public void EveryUnitTheApiCanReturn_ShouldBePublished()
    {
        // Arrange & Act
        var written = Enum.GetValues<Unit>().Select(unit => RecipeWords.Of(unit)).ToList();

        // Assert
        // Otherwise a recipe could be saved and read back with a unit the
        // contract says cannot exist.
        Assert.All(written, code => Assert.Contains(code, RecipeVocabulary.Units));
    }

    [Fact]
    public void ThePublishedUnits_ShouldBeExactlyTheOnesTheDomainHas()
    {
        // Arrange & Act
        var written = Enum.GetValues<Unit>().Select(unit => RecipeWords.Of(unit)!).ToHashSet();

        // Assert
        Assert.Equal(written, RecipeVocabulary.Units.ToHashSet());
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
