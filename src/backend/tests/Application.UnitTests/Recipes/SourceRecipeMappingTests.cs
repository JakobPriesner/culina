using Application.Recipes.Sources;
using Domain.Import;
using Domain.Recipes;

namespace Application.UnitTests.Recipes;

/// <summary>
/// Recipes written for another app: almost every test is about <em>not</em> refusing over a detail.
/// </summary>
public class SourceRecipeMappingTests
{
    [Fact]
    public void ToDetails_ShouldShortenATooLongTitle_RatherThanRefuseTheRecipe()
    {
        var theirs = Recipe(title: new string('a', RecipeTitle.MaxLength + 50));

        var details = SourceRecipeMapping.ToDetails(theirs, Domain.Shared.Language.En);

        Assert.Equal(
            RecipeTitle.MaxLength,
            details.Match(value => value.Title.Value.Length, error => throw Failed(error.Code)));
    }

    [Fact]
    public void ToDetails_ShouldRefuse_OnlyWhenThereIsNoTitleAtAll()
    {
        var details = SourceRecipeMapping.ToDetails(Recipe(title: "   "), Domain.Shared.Language.En);

        // The one hard requirement: a recipe with no name cannot be shortened into one.
        Assert.Equal(
            RecipeErrors.InvalidTitle.Code,
            details.Match(value => value.Title.Value, error => error.Code));
    }

    /// <summary>
    /// Not read out of the words, but no longer English for everybody: the importer's language is
    /// used.
    /// </summary>
    [Fact]
    public void ToDetails_ShouldLandTheRecipeInTheImportersLanguage()
    {
        var details = SourceRecipeMapping.ToDetails(
            Recipe(title: "Linsensuppe"),
            Domain.Shared.Language.De);

        Assert.Equal(
            Domain.Shared.Language.De,
            details.Match(value => value.Language, error => throw Failed(error.Code)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    [InlineData(100_000)]
    public void ToDetails_ShouldFallBackToTheDefaultYield_RatherThanGuess(decimal servings)
    {
        var details = SourceRecipeMapping.ToDetails(Recipe() with { Servings = servings }, Domain.Shared.Language.En);

        // The number scales every amount; a wrong one is worse than an absent one.
        Assert.Equal(
            Yield.Default.Amount,
            details.Match(value => value.Yield.Amount, error => throw Failed(error.Code)));
    }

    [Fact]
    public void ToDetails_ShouldDropAnImplausibleTime_RatherThanStoreIt()
    {
        var details = SourceRecipeMapping.ToDetails(
            Recipe() with { PrepMinutes = 0, CookMinutes = Recipe().CookMinutes },
            Domain.Shared.Language.En);

        Assert.Null(details.Match(value => value.PrepMinutes, error => throw Failed(error.Code)));
    }

    [Fact]
    public void ToGroups_ShouldKeepAUnitItCannotSpell_AsWordsInTheNote()
    {
        // "1/2 Dose" cannot be stored as a unit, but the amount is right and a cook needs to read
        // it.
        var theirs = Recipe(ingredients: [new SourceIngredient(2m, "1/2 Dose", "Tomaten", "geschält")]);

        var groups = SourceRecipeMapping.ToGroups(theirs);
        var line = Single(groups);

        Assert.Null(line.Quantity.Unit);
        Assert.Equal(2m, line.Quantity.Amount);
        Assert.Equal("1/2 Dose, geschält", line.Note);
    }

    [Fact]
    public void ToGroups_ShouldKeepAUnitThisAppHasNeverSeen_WhenItIsSpellable()
    {
        // The unit vocabulary is open: an import writes several new units at once.
        var theirs = Recipe(ingredients: [new SourceIngredient(1m, "Schuss", "Milch", null)]);

        var line = Single(SourceRecipeMapping.ToGroups(theirs));

        Assert.Equal("Schuss", line.Quantity.Unit?.Code);
        Assert.Null(line.Note);
    }

    [Fact]
    public void ToGroups_ShouldReadABuiltInTheOtherAppWroteOut_AsTheBuiltIn()
    {
        // Tandoor stores units as names; "500 Milliliter" as a household unit would never become
        // cups.
        var theirs = Recipe(ingredients: [new SourceIngredient(500m, "Milliliter", "Milch", null)]);

        var line = Single(SourceRecipeMapping.ToGroups(theirs));

        Assert.Same(Unit.Millilitre, line.Quantity.Unit);
    }

    [Fact]
    public void ToGroups_ShouldDropANamelessIngredient_RatherThanRefuseTheRecipe()
    {
        var theirs = Recipe(ingredients:
        [
            new SourceIngredient(1m, "g", "   ", null),
            new SourceIngredient(200m, "g", "Mehl", null)
        ]);

        var line = Single(SourceRecipeMapping.ToGroups(theirs));

        // A blank row left behind over there is not worth refusing the recipe over.
        Assert.Equal("Mehl", line.Name);
    }

    [Fact]
    public void ToGroups_ShouldGiveARecipeWithNoIngredients_AListToTypeInto()
    {
        var groups = SourceRecipeMapping.ToGroups(Recipe(ingredients: []));

        Assert.Single(groups.Match(value => value.Groups, error => throw Failed(error.Code)));
    }

    [Fact]
    public void ToSteps_ShouldTurnAStepIntoPlainWords_AndLinkNothing()
    {
        var theirs = Recipe(ingredients: [new SourceIngredient(200m, "g", "Mehl", null)]) with
        {
            Steps = [new SourceStep("Das Mehl sieben.", 120)]
        };

        var steps = Steps(theirs);

        // Not linked although "Mehl" appears in both: nothing over there said they match, and
        // guessing is silently wrong.
        Assert.Empty(steps[0].Uses);
        Assert.Equal(120, steps[0].DurationSeconds);
    }

    [Fact]
    public void ToSteps_ShouldLinkTheIngredientTheOtherAppItselfLinked()
    {
        // Not a guess: "{{ ingredients[0] }}" over there is the same fact this app stores as a
        // reference.
        var theirs = Recipe(ingredients: [new SourceIngredient(200m, "g", "Mehl", null)]) with
        {
            Steps = [new SourceStep([new SourceTextSegment("Das "), new SourceIngredientReference(0)], null)]
        };

        var ingredients = SourceRecipeMapping.ToGroups(theirs)
            .Match(value => value, error => throw Failed(error.Code));
        var steps = SourceRecipeMapping.ToSteps(theirs, ingredients.Landed)
            .Match(value => value, error => throw Failed(error.Code));

        var flour = ingredients.Groups.SelectMany(group => group.Ingredients).Single();

        Assert.Equal(
            [new TextSegment("Das "), new IngredientSegment(flour.Id)],
            steps[0].Segments);

        Assert.Equal([flour.Id], steps[0].Uses);
    }

    [Fact]
    public void ToSteps_ShouldCountPastARowThatWasDropped()
    {
        // The first row has no name so it is dropped here, but the reference still counts it.
        var theirs = Recipe(ingredients:
        [
            new SourceIngredient(1m, "g", "   ", null),
            new SourceIngredient(200m, "g", "Mehl", null)
        ]) with
        {
            Steps = [new SourceStep([new SourceIngredientReference(1)], null)]
        };

        var ingredients = SourceRecipeMapping.ToGroups(theirs)
            .Match(value => value, error => throw Failed(error.Code));
        var steps = SourceRecipeMapping.ToSteps(theirs, ingredients.Landed)
            .Match(value => value, error => throw Failed(error.Code));

        var flour = ingredients.Groups.SelectMany(group => group.Ingredients).Single();

        Assert.Equal([new IngredientSegment(flour.Id)], steps[0].Segments);
    }

    [Fact]
    public void ToSteps_ShouldDropAReferenceToARowThisAppDidNotKeep()
    {
        var theirs = Recipe(ingredients: [new SourceIngredient(1m, "g", "   ", null)]) with
        {
            Steps = [new SourceStep([new SourceTextSegment("Das "), new SourceIngredientReference(0)], null)]
        };

        var steps = Steps(theirs);

        // A sentence missing a noun reads better than one naming an ingredient not in the list.
        Assert.Equal([new TextSegment("Das")], steps[0].Segments);
    }

    [Fact]
    public void ToSteps_ShouldSkipAnEmptyStep_RatherThanRefuseTheRecipe()
    {
        var theirs = Recipe() with { Steps = [new SourceStep("  ", null), new SourceStep("Backen.", null)] };

        var steps = Steps(theirs);

        Assert.Single(steps);
    }

    [Fact]
    public void ToSteps_ShouldSplitALoneStep_WrittenAsALinePerAction()
    {
        // As the Tandoor import of a Chefkoch recipe arrives: the whole method in one step.
        var theirs = Recipe() with
        {
            Steps = [new SourceStep("Zwiebeln würfeln.\r\nIn Öl 4 Min. braten.\n\nBei mittlerer Hitze 1 Std. garen!", null)]
        };

        var steps = Steps(theirs);

        Assert.Equal(
            ["Zwiebeln würfeln.", "In Öl 4 Min. braten.", "Bei mittlerer Hitze 1 Std. garen!"],
            steps.Select(step => Assert.IsType<TextSegment>(Assert.Single(step.Segments)).Value));
        Assert.Equal([0, 1, 2], steps.Select(step => step.SortOrder));
    }

    [Fact]
    public void ToSteps_ShouldKeepAReference_WithTheLineItIsIn()
    {
        var theirs = Recipe(ingredients: [new SourceIngredient(1m, null, "Zwiebel", null)]) with
        {
            Steps =
            [
                new SourceStep(
                    [new SourceTextSegment("Die "), new SourceIngredientReference(0), new SourceTextSegment(" würfeln.\nIn Öl braten.")],
                    null)
            ]
        };

        var ingredients = SourceRecipeMapping.ToGroups(theirs)
            .Match(value => value, error => throw Failed(error.Code));
        var steps = SourceRecipeMapping.ToSteps(theirs, ingredients.Landed)
            .Match(value => value, error => throw Failed(error.Code));
        var onion = ingredients.Groups.SelectMany(group => group.Ingredients).Single();

        Assert.Equal(2, steps.Count);
        Assert.Equal([onion.Id], steps[0].Uses);
        Assert.Empty(steps[1].Uses);
    }

    [Fact]
    public void ToSteps_ShouldKeepLineBreaks_InARecipeThatHasStepsOfItsOwn()
    {
        var theirs = Recipe() with
        {
            Steps = [new SourceStep("Teig kneten.\nRuhen lassen.", null), new SourceStep("Backen.", null)]
        };

        Assert.Equal(2, Steps(theirs).Count);
    }

    [Theory]
    [InlineData("Für den Teig:\n- 200 g Mehl\n- 100 ml Wasser")]
    [InlineData("Alles verrühren.\nDazu passt Reis")]
    [InlineData("Nur ein Satz.")]
    public void ToSteps_ShouldKeepALoneStep_WhoseLinesAreNotEachASentence(string text)
    {
        var theirs = Recipe() with { Steps = [new SourceStep(text, null)] };

        Assert.Single(Steps(theirs));
    }

    [Fact]
    public void ToSteps_ShouldKeepALoneStep_ThatCarriesATimer()
    {
        // The timer belongs to the whole step; split, it would have to go with one line or the other.
        var theirs = Recipe() with { Steps = [new SourceStep("Anbraten.\nSchmoren.", 600)] };

        Assert.Single(Steps(theirs));
    }

    [Fact]
    public void ToDetails_ShouldDeduplicateTags_BecauseTwoAppsSpellThemDifferently()
    {
        var details = SourceRecipeMapping.ToDetails(
            Recipe() with { Tags = ["Vegan", "vegan", "  ", "Schnell"] },
            Domain.Shared.Language.En);

        Assert.Equal(
            ["Vegan", "Schnell"],
            details.Match(value => value.Tags, error => throw Failed(error.Code)));
    }

    [Fact]
    public void ToDetails_ShouldFitTheDescriptionAndTagsToWhatARecipeHolds_RatherThanRefuseTheRecipe()
    {
        var details = SourceRecipeMapping.ToDetails(
            Recipe() with
            {
                Description = new string('a', 3000),
                Tags = [new string('b', 100), .. Enumerable.Range(0, 40).Select(number => $"tag{number}")]
            },
            Domain.Shared.Language.En);

        var fitted = details.Match(value => value, error => throw Failed(error.Code));

        Assert.True(fitted.Description!.Length <= Domain.Recipes.Recipe.MaxDescriptionLength);
        Assert.Equal(Domain.Recipes.Recipe.MaxTags, fitted.Tags.Count);
        Assert.All(fitted.Tags, tag => Assert.True(tag.Length <= Domain.Recipes.Recipe.MaxTagLength));
    }

    [Fact]
    public void ToSteps_ShouldKeepALoneStep_ThatHasATitle()
    {
        // The heading belongs to the whole step, like a timer.
        var theirs = Recipe() with { Steps = [new SourceStep("Anbraten.\nSchmoren.", null) { Title = "Fleisch" }] };

        var step = Assert.Single(Steps(theirs));

        Assert.Equal("Fleisch", step.Title);
    }

    [Fact]
    public void ToSteps_ShouldKeepTheIngredientsAStepUses_WithoutNamingThem()
    {
        var theirs = Recipe(ingredients:
        [
            new SourceIngredient(200m, "g", "Mehl", null),
            new SourceIngredient(1m, null, "Ei", null)
        ]) with
        {
            Steps = [new SourceStep("Alles verrühren.", null) { Uses = [1, 7, -1] }]
        };

        var ingredients = SourceRecipeMapping.ToGroups(theirs)
            .Match(value => value, error => throw Failed(error.Code));
        var steps = SourceRecipeMapping.ToSteps(theirs, ingredients.Landed)
            .Match(value => value, error => throw Failed(error.Code));

        var egg = ingredients.Groups.SelectMany(group => group.Ingredients).Last();

        // A position past either end points at nothing, and is dropped rather than guessed.
        Assert.Equal([egg.Id], steps[0].Uses);
    }

    [Fact]
    public void ToDetails_ShouldUseTheLanguageTheOtherAppRecorded_OverTheImportersOwn()
    {
        var details = SourceRecipeMapping.ToDetails(
            Recipe() with { Language = Domain.Shared.Language.De },
            Domain.Shared.Language.En);

        Assert.Equal(
            Domain.Shared.Language.De,
            details.Match(value => value.Language, error => throw Failed(error.Code)));
    }

    [Fact]
    public void ToDetails_ShouldKeepPiecesAndTheRecipesOwnWord_WhenTheOtherAppRecordsThem()
    {
        var details = SourceRecipeMapping.ToDetails(
            Recipe() with { Servings = 12m, YieldKind = YieldKind.Pieces, YieldLabel = "Muffins" },
            Domain.Shared.Language.En);

        var yields = details.Match(value => value.Yield, error => throw Failed(error.Code));

        Assert.Equal((12m, YieldKind.Pieces, "Muffins"), (yields.Amount, yields.Kind, yields.Label));
    }

    private static RecipeIngredient Single(
        Domain.Shared.Result<ImportedIngredients> ingredients) =>
        ingredients.Match(
            value => value.Groups.SelectMany(group => group.Ingredients).Single(),
            error => throw Failed(error.Code));

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
