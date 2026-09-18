using Application.Recipes.Sources;
using Domain.Import;
using Domain.Recipes;

namespace Application.UnitTests.Recipes;

/// <summary>
/// What happens to a recipe that was written for a different app.
/// </summary>
/// <remarks>
/// Almost every test here is about <em>not</em> refusing. Somebody moving eight
/// hundred recipes cannot fix the one with a 140-character ingredient name, and
/// an import that stops on recipe 341 is worse than one that shortens a word.
/// </remarks>
public class SourceRecipeMappingTests
{
    [Fact]
    public void ToDetails_ShouldShortenATooLongTitle_RatherThanRefuseTheRecipe()
    {
        // Arrange
        var theirs = Recipe(title: new string('a', RecipeTitle.MaxLength + 50));

        // Act
        var details = SourceRecipeMapping.ToDetails(theirs);

        // Assert
        Assert.Equal(
            RecipeTitle.MaxLength,
            details.Match(value => value.Title.Value.Length, error => throw Failed(error.Code)));
    }

    [Fact]
    public void ToDetails_ShouldRefuse_OnlyWhenThereIsNoTitleAtAll()
    {
        // Act
        var details = SourceRecipeMapping.ToDetails(Recipe(title: "   "));

        // Assert
        // The one hard requirement: a recipe with no name is not a recipe that
        // can be shortened into one.
        Assert.Equal(
            RecipeErrors.InvalidTitle.Code,
            details.Match(value => value.Title.Value, error => error.Code));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    [InlineData(100_000)]
    public void ToDetails_ShouldFallBackToTheDefaultYield_RatherThanGuess(decimal servings)
    {
        // Act
        var details = SourceRecipeMapping.ToDetails(Recipe() with { Servings = servings });

        // Assert
        // This number scales every amount in the recipe. A wrong one is worse
        // than an absent one.
        Assert.Equal(
            Yield.Default.Amount,
            details.Match(value => value.Yield.Amount, error => throw Failed(error.Code)));
    }

    [Fact]
    public void ToDetails_ShouldDropAnImplausibleTime_RatherThanStoreIt()
    {
        // Act
        var details = SourceRecipeMapping.ToDetails(
            Recipe() with { PrepMinutes = 0, CookMinutes = Recipe().CookMinutes });

        // Assert
        Assert.Null(details.Match(value => value.PrepMinutes, error => throw Failed(error.Code)));
    }

    [Fact]
    public void ToGroups_ShouldKeepAUnitItCannotSpell_AsWordsInTheNote()
    {
        // Arrange
        // "1/2 Dose" is not a unit this app can store, but the amount is still
        // right and a cook still needs to read it.
        var theirs = Recipe(ingredients: [new SourceIngredient(2m, "1/2 Dose", "Tomaten", "geschält")]);

        // Act
        var groups = SourceRecipeMapping.ToGroups(theirs);
        var line = Single(groups);

        // Assert
        Assert.Null(line.Quantity.Unit);
        Assert.Equal(2m, line.Quantity.Amount);
        Assert.Equal("1/2 Dose, geschält", line.Note);
    }

    [Fact]
    public void ToGroups_ShouldKeepAUnitThisAppHasNeverSeen_WhenItIsSpellable()
    {
        // Arrange
        // The unit vocabulary is open: a cook who measures in Schuss adds one
        // by writing it, and an import is somebody writing several at once.
        var theirs = Recipe(ingredients: [new SourceIngredient(1m, "Schuss", "Milch", null)]);

        // Act
        var line = Single(SourceRecipeMapping.ToGroups(theirs));

        // Assert
        Assert.Equal("Schuss", line.Quantity.Unit?.Code);
        Assert.Null(line.Note);
    }

    [Fact]
    public void ToGroups_ShouldDropANamelessIngredient_RatherThanRefuseTheRecipe()
    {
        // Arrange
        var theirs = Recipe(ingredients:
        [
            new SourceIngredient(1m, "g", "   ", null),
            new SourceIngredient(200m, "g", "Mehl", null)
        ]);

        // Act
        var line = Single(SourceRecipeMapping.ToGroups(theirs));

        // Assert
        // A blank row somebody left behind over there is not worth refusing
        // their recipe over.
        Assert.Equal("Mehl", line.Name);
    }

    [Fact]
    public void ToGroups_ShouldGiveARecipeWithNoIngredients_AListToTypeInto()
    {
        // Act
        var groups = SourceRecipeMapping.ToGroups(Recipe(ingredients: []));

        // Assert
        Assert.Single(groups.Match(value => value.Groups, error => throw Failed(error.Code)));
    }

    [Fact]
    public void ToSteps_ShouldTurnAStepIntoPlainWords_AndLinkNothing()
    {
        // Arrange
        var theirs = Recipe(ingredients: [new SourceIngredient(200m, "g", "Mehl", null)]) with
        {
            Steps = [new SourceStep("Das Mehl sieben.", 120)]
        };

        // Act
        var steps = Steps(theirs);

        // Assert
        // "Mehl" appears in the sentence and in the ingredient list, and it is
        // still not linked: nothing over there said they were the same thing,
        // and guessing would be wrong invisibly, eight hundred times.
        Assert.Empty(steps[0].Uses);
        Assert.Equal(120, steps[0].DurationSeconds);
    }

    [Fact]
    public void ToSteps_ShouldLinkTheIngredientTheOtherAppItselfLinked()
    {
        // Arrange
        // Not a guess. Somebody wrote "{{ ingredients[0] }}" over there, which
        // is the same fact this app stores as a reference, and carrying it
        // across is what keeps the amount moving with the servings.
        var theirs = Recipe(ingredients: [new SourceIngredient(200m, "g", "Mehl", null)]) with
        {
            Steps = [new SourceStep([new SourceTextSegment("Das "), new SourceIngredientReference(0)], null)]
        };

        // Act
        var ingredients = SourceRecipeMapping.ToGroups(theirs)
            .Match(value => value, error => throw Failed(error.Code));
        var steps = SourceRecipeMapping.ToSteps(theirs, ingredients.Landed)
            .Match(value => value, error => throw Failed(error.Code));

        // Assert
        var flour = ingredients.Groups.SelectMany(group => group.Ingredients).Single();

        Assert.Equal(
            [new TextSegment("Das "), new IngredientSegment(flour.Id)],
            steps[0].Segments);

        // And the step needs it, which is what a shopping list reads.
        Assert.Equal([flour.Id], steps[0].Uses);
    }

    [Fact]
    public void ToSteps_ShouldCountPastARowThatWasDropped()
    {
        // Arrange
        // The first row has no name, so it is not an ingredient here — but it
        // was one over there, and the reference counts it.
        var theirs = Recipe(ingredients:
        [
            new SourceIngredient(1m, "g", "   ", null),
            new SourceIngredient(200m, "g", "Mehl", null)
        ]) with
        {
            Steps = [new SourceStep([new SourceIngredientReference(1)], null)]
        };

        // Act
        var ingredients = SourceRecipeMapping.ToGroups(theirs)
            .Match(value => value, error => throw Failed(error.Code));
        var steps = SourceRecipeMapping.ToSteps(theirs, ingredients.Landed)
            .Match(value => value, error => throw Failed(error.Code));

        // Assert
        var flour = ingredients.Groups.SelectMany(group => group.Ingredients).Single();

        Assert.Equal([new IngredientSegment(flour.Id)], steps[0].Segments);
    }

    [Fact]
    public void ToSteps_ShouldDropAReferenceToARowThisAppDidNotKeep()
    {
        // Arrange
        var theirs = Recipe(ingredients: [new SourceIngredient(1m, "g", "   ", null)]) with
        {
            Steps = [new SourceStep([new SourceTextSegment("Das "), new SourceIngredientReference(0)], null)]
        };

        // Act
        var steps = Steps(theirs);

        // Assert
        // A sentence missing a noun reads better than one naming an ingredient
        // the list does not contain.
        Assert.Equal([new TextSegment("Das")], steps[0].Segments);
    }

    [Fact]
    public void ToSteps_ShouldSkipAnEmptyStep_RatherThanRefuseTheRecipe()
    {
        // Arrange
        var theirs = Recipe() with { Steps = [new SourceStep("  ", null), new SourceStep("Backen.", null)] };

        // Act
        var steps = Steps(theirs);

        // Assert
        Assert.Single(steps);
    }

    [Fact]
    public void ToDetails_ShouldDeduplicateTags_BecauseTwoAppsSpellThemDifferently()
    {
        // Act
        var details = SourceRecipeMapping.ToDetails(
            Recipe() with { Tags = ["Vegan", "vegan", "  ", "Schnell"] });

        // Assert
        Assert.Equal(
            ["Vegan", "Schnell"],
            details.Match(value => value.Tags, error => throw Failed(error.Code)));
    }

    private static RecipeIngredient Single(
        Domain.Shared.Result<ImportedIngredients> ingredients) =>
        ingredients.Match(
            value => value.Groups.SelectMany(group => group.Ingredients).Single(),
            error => throw Failed(error.Code));

    /// <summary>The steps, made the way the import makes them.</summary>
    private static IReadOnlyList<Step> Steps(SourceRecipe theirs) =>
        SourceRecipeMapping.ToGroups(theirs)
            .Match(
                ingredients => SourceRecipeMapping.ToSteps(theirs, ingredients.Landed),
                error => throw Failed(error.Code))
            .Match(value => value, error => throw Failed(error.Code));

    private static InvalidOperationException Failed(string code) => new($"Unexpected failure: {code}");

    private static SourceRecipe Recipe(
        string title = "Zwiebelkuchen",
        IReadOnlyList<SourceIngredient>? ingredients = null) =>
        new()
        {
            ExternalId = "42",
            Title = title,
            Servings = 4m,
            PrepMinutes = 20,
            CookMinutes = 45,
            Groups = [new SourceIngredientGroup(null, ingredients ?? [])]
        };
}
