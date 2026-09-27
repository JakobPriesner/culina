using Domain.Search;

namespace Domain.UnitTests.Search;

/// <summary>
/// Both German transliterations, and nothing a LIKE pattern could mistake for
/// a wildcard.
/// </summary>
public class SearchTextTests
{
    [Theory]
    [InlineData("Hähnchen", "haehnchen", "hahnchen")]
    [InlineData("Müsli", "muesli", "musli")]
    [InlineData("Soße", "sosse", "sosse")]
    [InlineData("crème brûlée", "creme brulee", "creme brulee")]
    [InlineData("  Spaghetti   Bolognese ", "spaghetti bolognese", "spaghetti bolognese")]
    [InlineData("Bolognese-Sauce", "bolognese sauce", "bolognese sauce")]
    [InlineData("100% Roggen_brot", "100 roggen brot", "100 roggen brot")]
    [InlineData("ÄÖÜ", "aeoeue", "aou")]
    [InlineData("%", "", "")]
    public void Fold_ShouldEmitBothTransliterations(string input, string umlaut, string stripped)
    {
        // Act & Assert
        Assert.Equal(umlaut, SearchText.FoldAe(input));
        Assert.Equal(stripped, SearchText.FoldA(input));
    }

    [Theory]
    [InlineData("Sommergericht", new[] { "sommer" })]
    [InlineData("Winteressen", new[] { "winter" })]
    [InlineData("Partyrezepte", new[] { "party" })]
    // The linking s, or a word's own: both readings, and a tag decides.
    [InlineData("Sonntagsessen", new[] { "sonntags", "sonntag" })]
    [InlineData("Maisgericht", new[] { "mais" })]
    [InlineData("schnelle Ofengerichte", new[] { "ofen" })]
    public void Modifiers_ShouldReadWhatACompoundIsAbout_WhenItsHeadSaysNothing(string query, string[] expected)
    {
        // Act & Assert
        Assert.Equal(expected, SearchText.Modifiers(query));
    }

    [Theory]
    // A Fischsuppe is a soup, not something with fish: the head decides.
    [InlineData("Fischsuppe")]
    // Nothing left in front of the head, or too little to be a tag.
    [InlineData("Gericht")]
    [InlineData("Eisrezept")]
    [InlineData("Sommer")]
    public void Modifiers_ShouldReadNothing_WhenTheHeadMeansSomething(string query)
    {
        // Act & Assert
        Assert.Empty(SearchText.Modifiers(query));
    }
}
