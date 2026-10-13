using Application.Recipes;
using Application.Shopping;
using Domain.Recipes;
using Domain.Shopping;
using TestSupport;

namespace Application.UnitTests.Recipes;

/// <summary>Words from a request that name nothing Culina knows each fail with a code of their own: the code is what the reader is shown.</summary>
public class RecipeWordsTests
{
    [Fact]
    public void ToLanguage_ShouldFailWithItsOwnCode_ForALanguageCulinaDoesNotKnow() =>
        RecipeWords.ToLanguage("fr").ShouldBeFailure(RecipeErrors.InvalidLanguage);

    [Fact]
    public void ToSection_ShouldFailWithItsOwnCode_ForASectionTheListDoesNotHave() =>
        ShoppingWords.ToSection("garden").ShouldBeFailure(ShoppingErrors.UnknownSection);
}
