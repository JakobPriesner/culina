using Application.Recipes;
using Contracts.Recipes;
using Domain.Recipes;
using Domain.Shared;

namespace IntegrationTests.Recipes;

/// <summary>The wire vocabulary, the reader and the writer must agree (the OpenAPI document once advertised <c>gram</c> while the API accepted only <c>g</c>).</summary>
public class RecipeVocabularyTests
{
    private static bool Rejected<TValue>(Result<TValue> result)
        where TValue : notnull =>
        result.Match(_ => false, _ => true);

    [Fact]
    public void EveryPublishedUnit_ShouldBeAcceptedOnAWrite()
    {
        var rejected = RecipeVocabulary.Units
            .Where(code => Rejected(RecipeWords.ToQuantity(1m, code)))
            .ToList();

        Assert.Empty(rejected);
    }

    [Fact]
    public void ThePublishedUnits_ShouldBeExactlyTheOnesBuiltIn()
    {
        var written = Unit.BuiltIn.Select(unit => unit.Code).ToHashSet();

        // A built-in the document forgets is one no picker ever offers.
        Assert.Equal(written, RecipeVocabulary.Units.ToHashSet());
    }

    [Theory]
    [InlineData("Schuss")]
    [InlineData("Handvoll")]
    [InlineData("fl oz")]
    public void AUnitAHouseholdWrites_ShouldBeAccepted(string code)
    {
        var result = RecipeWords.ToQuantity(1m, code);

        Assert.False(Rejected(result));
    }

    [Theory]
    [InlineData("200g")]
    [InlineData("2 1/2")]
    [InlineData("a very long unit indeed")]
    public void SomethingThatIsNotAUnit_ShouldBeRejected(string code)
    {
        var result = RecipeWords.ToQuantity(1m, code);

        // Open is not anything goes: "200g" lost its space and would be an unmatchable unit.
        Assert.True(Rejected(result));
    }

    [Fact]
    public void EveryPublishedYieldKind_ShouldBeAcceptedOnAWrite()
    {
        var rejected = RecipeVocabulary.YieldKinds
            .Where(code => Rejected(RecipeWords.ToYieldKind(code)))
            .ToList();

        Assert.Empty(rejected);
    }

    [Fact]
    public void EveryPublishedLanguage_ShouldBeAcceptedOnAWrite()
    {
        var rejected = RecipeVocabulary.Languages
            .Where(code => Rejected(RecipeWords.ToLanguage(code)))
            .ToList();

        Assert.Empty(rejected);
    }
}
