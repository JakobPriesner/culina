using Domain.Ingredients;
using Domain.Recipes;
using Domain.Shared;
using TestSupport;

namespace Domain.UnitTests.Ingredients;

public class IngredientMergeTests
{
    [Fact]
    public void Create_ShouldRefuse_WhenTheNameIsMergedIntoItself()
    {
        // Act
        var result = IngredientMerge.Create(" Salz ", "Salz");

        // Assert
        result.ShouldBeFailure(IngredientErrors.SameName);
    }

    [Theory]
    [InlineData("", "Salz")]
    [InlineData("Salz", "   ")]
    [InlineData("Salz", null)]
    public void Create_ShouldRefuse_WhenANameIsMissing(string? from, string? to)
    {
        // Act
        var result = IngredientMerge.Create(from, to);

        // Assert
        result.ShouldBeFailure(IngredientErrors.InvalidName);
    }

    [Fact]
    public void Create_ShouldAcceptASpellingFix_WhenBothFoldTheSame()
    {
        // Act
        var merge = IngredientMerge.Create("Olivenoel", "Olivenöl").ShouldBeSuccess();

        // Assert
        Assert.Equal(merge.From.ComparisonKey, merge.To.ComparisonKey);
    }

    [Theory]
    [InlineData("Hühnchenbrustfilet", "Hähnchenbrust")]
    [InlineData("hühnchenbrustfilet", "Hähnchenbrust")]
    [InlineData("Huehnchenbrustfilet", "Hähnchenbrust")]
    [InlineData("Reis", "Reis")]
    public void Resolve_ShouldGiveTheKeptName_ForEverySpellingOfAMergedOne(string written, string stored)
    {
        // Arrange
        var merges = new IngredientMerges([IngredientMerge.Create("Hühnchenbrustfilet", "Hähnchenbrust").ShouldBeSuccess()]);

        // Act
        var resolved = merges.Resolve(written);

        // Assert
        Assert.Equal(stored, resolved);
    }

    [Fact]
    public void UseKeptNames_ShouldRenameLinesButKeepTheirIdsAndTheVersion()
    {
        // Arrange
        var now = DateTimeOffset.UnixEpoch;
        var recipe = Recipe.Create(Guid.NewGuid(), RecipeTitle.Create("Curry").ShouldBeSuccess(), Guid.NewGuid(), Language.De, now);
        var line = RecipeIngredient.Create(null, 0, Quantity.Unmeasured, "Hühnchenbrustfilet", "gewürfelt").ShouldBeSuccess();
        recipe.SetContents([IngredientGroup.Create(null, null, 0, [line]).ShouldBeSuccess()], [], now).ShouldBeSuccess();
        var merges = new IngredientMerges([IngredientMerge.Create("Hühnchenbrustfilet", "Hähnchenbrust").ShouldBeSuccess()]);

        // Act
        recipe.UseKeptNames(merges);

        // Assert
        var renamed = Assert.Single(recipe.Ingredients);
        Assert.Equal("Hähnchenbrust", renamed.Name);
        Assert.Equal("gewürfelt", renamed.Note);
        Assert.Equal(line.Id, renamed.Id);
        Assert.Equal(1, recipe.Version);
    }
}
