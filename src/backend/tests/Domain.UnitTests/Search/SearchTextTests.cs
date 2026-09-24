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
}
