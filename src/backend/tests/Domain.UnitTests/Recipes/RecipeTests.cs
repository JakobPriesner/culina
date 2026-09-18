using Domain.Recipes;
using Domain.Shared;
using TestSupport;

namespace Domain.UnitTests.Recipes;

public class RecipeTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid Household = Guid.CreateVersion7();
    private static readonly Guid Author = Guid.CreateVersion7();

    [Fact]
    public void Create_ShouldNeedNothingButATitle_BecauseThatIsThePointOfTheCreateForm()
    {
        // Arrange
        var title = RecipeTitle.Create("Bolognese").ShouldBeSuccess();

        // Act
        var recipe = Recipe.Create(Household, title, Author, Now);

        // Assert
        Assert.Equal("Bolognese", recipe.Title.Value);
        Assert.Equal(1, recipe.Version);
        // One implicit group, so grouping stays invisible until it is used.
        Assert.Single(recipe.Groups);
        Assert.Null(Assert.Single(recipe.Groups).Name);
        Assert.Empty(recipe.Steps);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Title_ShouldBeRequired_BecauseItIsTheOneThingARecipeMustHave(string? value)
    {
        // Arrange & Act
        var result = RecipeTitle.Create(value);

        // Assert
        result.ShouldBeFailure(RecipeErrors.InvalidTitle);
    }

    [Fact]
    public void TotalMinutes_ShouldBeNull_WhenNeitherTimeIsGiven()
    {
        // Arrange
        var recipe = ARecipe();

        // Act
        recipe.Describe(Details(prep: null, cook: null), Now).ShouldBeSuccess();

        // Assert
        // Derived, never stored: a stored total is a second source of truth
        // that eventually disagrees with its parts.
        Assert.Null(recipe.TotalMinutes);
    }

    [Fact]
    public void TotalMinutes_ShouldSumWhatIsGiven_WhenOnlyOneTimeIsKnown()
    {
        // Arrange
        var recipe = ARecipe();

        // Act
        recipe.Describe(Details(prep: 15, cook: null), Now).ShouldBeSuccess();

        // Assert
        Assert.Equal(15, recipe.TotalMinutes);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10_081)]
    public void Describe_ShouldRejectAnImplausibleTime(int minutes)
    {
        // Arrange
        var recipe = ARecipe();

        // Act
        var result = recipe.Describe(Details(prep: minutes, cook: null), Now);

        // Assert
        result.ShouldBeFailure(RecipeErrors.InvalidDuration);
    }

    [Fact]
    public void SetContents_ShouldAcceptAStepThatMentionsAnIngredientOfThisRecipe()
    {
        // Arrange
        var recipe = ARecipe();
        var butter = AnIngredient("butter");
        var group = AGroup(butter);
        var step = AStep(0, [new TextSegment("Melt "), new IngredientSegment(butter.Id)]);

        // Act
        var result = recipe.SetContents([group], [step], Now);

        // Assert
        result.ShouldBeSuccess();
        Assert.Equal([butter.Id], recipe.Steps[0].Uses);
    }

    [Fact]
    public void Step_ShouldNeedAnIngredientItsWordsName_EvenWhenTheCallerDidNotListIt()
    {
        // Arrange
        var butter = AnIngredient("butter");

        // Act
        var step = AStep(0, [new TextSegment("Melt "), new IngredientSegment(butter.Id)]);

        // Assert
        // The words and the list can never disagree, because the words are
        // folded into the list where the step is made.
        Assert.Equal([butter.Id], step.Uses);
    }

    [Fact]
    public void Step_ShouldNeedAnIngredientItsWordsDoNotName()
    {
        // Arrange
        var flour = AnIngredient("flour");

        // Act
        // "Combine everything and knead" needs flour and never says so.
        var step = AStep(0, [new TextSegment("Combine everything and knead.")], flour.Id);

        // Assert
        Assert.Equal([flour.Id], step.Uses);
    }

    [Fact]
    public void Step_ShouldCollapseAnIngredientListedTwice()
    {
        // Arrange
        var butter = AnIngredient("butter");

        // Act
        var step = AStep(
            0,
            [new TextSegment("Melt "), new IngredientSegment(butter.Id)],
            butter.Id,
            butter.Id);

        // Assert
        // Saying it twice means what saying it once means.
        Assert.Equal([butter.Id], step.Uses);
    }

    [Fact]
    public void Step_ShouldRejectMoreIngredientsThanARecipeCouldHave()
    {
        // Arrange
        var tooMany = Enumerable.Range(0, Recipe.MaxIngredients + 1)
            .Select(_ => Guid.CreateVersion7())
            .ToArray();

        // Act
        var result = Step.Create(null, 0, [new TextSegment("Combine.")], tooMany, null);

        // Assert
        result.ShouldBeFailure(RecipeErrors.TooManyIngredients);
    }

    [Fact]
    public void SetContents_ShouldRejectAStepThatNeedsAnIngredientThisRecipeNeverHad()
    {
        // Arrange
        var recipe = ARecipe();
        var step = AStep(0, [new TextSegment("Combine.")], Guid.CreateVersion7());

        // Act
        var result = recipe.SetContents([AGroup()], [step], Now);

        // Assert
        // Listing an ingredient is checked exactly as naming one is: both end up
        // in the same set, and the set is what the reference index is built from.
        result.ShouldBeFailure(RecipeErrors.UnknownIngredientReference);
    }

    [Fact]
    public void SetContents_ShouldNameTheStep_WhenAnIngredientItOnlyListedWasRemoved()
    {
        // Arrange
        var recipe = ARecipe();
        var flour = AnIngredient("flour");
        var step = AStep(1, [new TextSegment("Combine everything and knead.")], flour.Id);
        recipe.SetContents([AGroup(flour)], [AStep(0, [new TextSegment("Preheat.")]), step], Now)
            .ShouldBeSuccess();

        // Act
        var result = recipe.SetContents([AGroup()], [AStep(0, [new TextSegment("Preheat.")]), step], Now);

        // Assert
        // There is no mention to point at, so the message has to name the step.
        result.ShouldBeFailure(RecipeErrors.IngredientInUse(2));
    }

    [Fact]
    public void SetContents_ShouldRejectAStepThatMentionsAnIngredientThisRecipeNeverHad()
    {
        // Arrange
        var recipe = ARecipe();
        var step = AStep(0, [new IngredientSegment(Guid.CreateVersion7())]);

        // Act
        var result = recipe.SetContents([AGroup()], [step], Now);

        // Assert
        result.ShouldBeFailure(RecipeErrors.UnknownIngredientReference);
    }

    [Fact]
    public void SetContents_ShouldNameTheStep_WhenAnIngredientStillInUseWasRemoved()
    {
        // Arrange
        var recipe = ARecipe();
        var butter = AnIngredient("butter");
        var step = AStep(1, [new TextSegment("Melt "), new IngredientSegment(butter.Id)]);
        recipe.SetContents([AGroup(butter)], [AStep(0, [new TextSegment("Preheat.")]), step], Now)
            .ShouldBeSuccess();

        // Act
        var result = recipe.SetContents([AGroup()], [AStep(0, [new TextSegment("Preheat.")]), step], Now);

        // Assert
        // The more helpful error: this is an editing mistake with an obvious
        // fix, not a malformed request.
        result.ShouldBeFailure(RecipeErrors.IngredientInUse(2));
    }

    [Fact]
    public void SetContents_ShouldRestoreTheImplicitGroup_WhenEveryGroupIsRemoved()
    {
        // Arrange
        var recipe = ARecipe();

        // Act
        var result = recipe.SetContents([], [], Now);

        // Assert
        result.ShouldBeSuccess();
        Assert.Null(Assert.Single(recipe.Groups).Name);
    }

    [Fact]
    public void SetContents_ShouldRefuse_WhenThereAreMoreIngredientsThanAnyoneCouldCookFrom()
    {
        // Arrange
        var recipe = ARecipe();
        var many = Enumerable.Range(0, Recipe.MaxIngredients + 1)
            .Select(index => AnIngredient($"thing {index}"))
            .ToList();

        // Act
        var result = recipe.SetContents([AGroup([.. many])], [], Now);

        // Assert
        result.ShouldBeFailure(RecipeErrors.TooManyIngredients);
    }

    [Fact]
    public void SetContents_ShouldRefuse_WhenTwoLinesClaimTheSameIngredientId()
    {
        // Arrange
        var recipe = ARecipe();
        var shared = CulinaId.New();

        // Act
        var result = recipe.SetContents(
            [AGroup(AnIngredient("butter", shared), AnIngredient("flour", shared))],
            [],
            Now);

        // Assert
        result.ShouldBeFailure(RecipeErrors.DuplicateIngredient);
    }

    [Fact]
    public void SetContents_ShouldCountLinesRatherThanIds_WhenCappingIngredients()
    {
        // Arrange
        var recipe = ARecipe();
        var many = Enumerable.Range(0, Recipe.MaxIngredients)
            .Select(index => AnIngredient($"thing {index}"))
            .ToList();

        // Act
        var result = recipe.SetContents([AGroup([.. many])], [], Now);

        // Assert
        result.ShouldBeSuccess();
        Assert.Equal(Recipe.MaxIngredients, recipe.Ingredients.Count());
    }

    [Fact]
    public void SetContents_ShouldRefuse_WhenThereAreMoreStepsThanAnyoneCouldFollow()
    {
        // Arrange
        var recipe = ARecipe();
        var many = Enumerable.Range(0, Recipe.MaxSteps + 1)
            .Select(index => AStep(index, [new TextSegment($"Step {index}.")]))
            .ToList();

        // Act
        var result = recipe.SetContents([AGroup()], many, Now);

        // Assert
        result.ShouldBeFailure(RecipeErrors.TooManySteps);
    }

    [Fact]
    public void Step_ShouldRejectAnImplausibleTimer()
    {
        // Arrange & Act
        var result = Step.Create(null, 0, [new TextSegment("Rest.")], [], durationSeconds: 90_000);

        // Assert
        result.ShouldBeFailure(RecipeErrors.InvalidDuration);
    }

    [Fact]
    public void Step_ShouldTakeATitle_SoALayeredRecipeCanNameItsParts()
    {
        // Arrange & Act
        var step = Step.Create(
            null,
            0,
            [new TextSegment("Rub the butter into the flour.")],
            [],
            null,
            "  Prepare the base  ").ShouldBeSuccess();

        // Assert
        Assert.Equal("Prepare the base", step.Title);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Step_ShouldReadABlankTitleAsNoTitle_SoClearingTheFieldGivesTheNumberBack(
        string? title)
    {
        // Arrange & Act
        var step = Step.Create(null, 0, [new TextSegment("Bake.")], [], null, title)
            .ShouldBeSuccess();

        // Assert
        Assert.Null(step.Title);
    }

    [Fact]
    public void Step_ShouldRejectATitleLongEnoughToBeTheInstruction()
    {
        // Arrange & Act
        var result = Step.Create(
            null,
            0,
            [new TextSegment("Bake.")],
            [],
            null,
            new string('x', Step.MaxTitleLength + 1));

        // Assert
        result.ShouldBeFailure(RecipeErrors.InvalidStepTitle);
    }

    [Fact]
    public void Yield_ShouldTakeARecipesOwnWord_SoACakeIsNotFourPortions()
    {
        // Arrange & Act
        var measure = Yield.Create(1m, YieldKind.Servings, "  Cake  ").ShouldBeSuccess();

        // Assert
        Assert.Equal("Cake", measure.Label);
        // The word replaces the wording and nothing else: the kind still says
        // how the servings control counts.
        Assert.Equal(YieldKind.Servings, measure.Kind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Yield_ShouldReadABlankWordAsNoWord_SoAnEmptiedFieldIsNotAYieldOfNothing(
        string? label)
    {
        // Arrange & Act
        var measure = Yield.Create(4m, YieldKind.Servings, label).ShouldBeSuccess();

        // Assert
        Assert.Null(measure.Label);
    }

    [Fact]
    public void Yield_ShouldRejectAWordLongEnoughToBeASentence()
    {
        // Arrange & Act
        var result = Yield.Create(
            4m,
            YieldKind.Servings,
            new string('x', Yield.MaxLabelLength + 1));

        // Assert
        result.ShouldBeFailure(RecipeErrors.InvalidYieldLabel);
    }

    [Fact]
    public void Ingredient_ShouldKeepThePreparationOutOfTheName_SoShoppingCanMerge()
    {
        // Arrange & Act
        var ingredient = RecipeIngredient.Create(
            null,
            0,
            Quantity.Create(200m, Unit.Gram).ShouldBeSuccess(),
            "  butter  ",
            "  finely chopped  ").ShouldBeSuccess();

        // Assert
        Assert.Equal("butter", ingredient.Name);
        Assert.Equal("finely chopped", ingredient.Note);
    }

    private static Recipe ARecipe() =>
        Recipe.Create(Household, RecipeTitle.Create("Bolognese").ShouldBeSuccess(), Author, Now);

    private static RecipeDetails Details(int? prep, int? cook) => new(
        RecipeTitle.Create("Bolognese").ShouldBeSuccess(),
        Description: null,
        Language.En,
        Yield.Default,
        prep,
        cook,
        Tags: []);

    private static RecipeIngredient AnIngredient(string name, Guid? id = null) =>
        RecipeIngredient.Create(id, 0, Quantity.Unmeasured, name, null).ShouldBeSuccess();

    private static IngredientGroup AGroup(params RecipeIngredient[] ingredients) =>
        IngredientGroup.Create(null, null, 0, ingredients).ShouldBeSuccess();

    private static Step AStep(int sortOrder, StepSegment[] segments, params Guid[] uses) =>
        Step.Create(null, sortOrder, segments, uses, null).ShouldBeSuccess();
}
