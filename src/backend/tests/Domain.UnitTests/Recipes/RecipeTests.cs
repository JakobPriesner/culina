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
        var title = RecipeTitle.Create("Bolognese").ShouldBeSuccess();

        var recipe = Recipe.Create(Household, title, Author, Language.De, Now);

        Assert.Equal("Bolognese", recipe.Title.Value);
        Assert.Equal(1, recipe.Version);
        // One implicit group, so grouping stays invisible until used.
        Assert.Single(recipe.Groups);
        Assert.Null(Assert.Single(recipe.Groups).Name);
        Assert.Empty(recipe.Steps);
        // A recipe that starts in the wrong language is searched with the wrong stemmer, unnoticed.
        Assert.Equal(Language.De, recipe.Language);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Title_ShouldBeRequired_BecauseItIsTheOneThingARecipeMustHave(string? value)
    {
        var result = RecipeTitle.Create(value);

        result.ShouldBeFailure(RecipeErrors.InvalidTitle);
    }

    [Fact]
    public void TotalMinutes_ShouldBeNull_WhenNeitherTimeIsGiven()
    {
        var recipe = ARecipe();

        recipe.Describe(Details(prep: null, cook: null), Now).ShouldBeSuccess();

        // Derived, never stored: a stored total would eventually disagree with its parts.
        Assert.Null(recipe.TotalMinutes);
    }

    [Fact]
    public void TotalMinutes_ShouldSumWhatIsGiven_WhenOnlyOneTimeIsKnown()
    {
        var recipe = ARecipe();

        recipe.Describe(Details(prep: 15, cook: null), Now).ShouldBeSuccess();

        Assert.Equal(15, recipe.TotalMinutes);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10_081)]
    public void Describe_ShouldRejectAnImplausibleTime(int minutes)
    {
        var recipe = ARecipe();

        var result = recipe.Describe(Details(prep: minutes, cook: null), Now);

        result.ShouldBeFailure(RecipeErrors.InvalidDuration);
    }

    [Fact]
    public void Describe_ShouldRejectADescriptionOverTheLimit()
    {
        var recipe = ARecipe();

        var result = recipe.Describe(
            Details(prep: null, cook: null, description: new string('a', Recipe.MaxDescriptionLength + 1)),
            Now);

        result.ShouldBeFailure(RecipeErrors.InvalidDescription);
    }

    [Fact]
    public void Describe_ShouldAcceptADescriptionAtTheLimit()
    {
        var recipe = ARecipe();

        recipe.Describe(
            Details(prep: null, cook: null, description: new string('a', Recipe.MaxDescriptionLength)),
            Now).ShouldBeSuccess();
    }

    [Fact]
    public void Describe_ShouldRejectMoreTagsThanTheLimit()
    {
        var recipe = ARecipe();
        var tags = Enumerable.Range(0, Recipe.MaxTags + 1).Select(number => $"tag{number}").ToArray();

        var result = recipe.Describe(Details(prep: null, cook: null, tags: tags), Now);

        result.ShouldBeFailure(RecipeErrors.TooManyTags);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Describe_ShouldRejectABlankTag(string tag)
    {
        var recipe = ARecipe();

        var result = recipe.Describe(Details(prep: null, cook: null, tags: ["pasta", tag]), Now);

        result.ShouldBeFailure(RecipeErrors.InvalidTag);
    }

    [Fact]
    public void Describe_ShouldRejectATagOverTheLimit()
    {
        var recipe = ARecipe();

        var result = recipe.Describe(
            Details(prep: null, cook: null, tags: [new string('a', Recipe.MaxTagLength + 1)]),
            Now);

        result.ShouldBeFailure(RecipeErrors.InvalidTag);
    }

    [Fact]
    public void Describe_ShouldChangeNothing_WhenTheTagsAreRejected()
    {
        var recipe = ARecipe();

        recipe.Describe(Details(prep: null, cook: null, tags: [""]), Now);

        Assert.Empty(recipe.Tags);
    }

    [Fact]
    public void SetContents_ShouldAcceptAStepThatMentionsAnIngredientOfThisRecipe()
    {
        var recipe = ARecipe();
        var butter = AnIngredient("butter");
        var group = AGroup(butter);
        var step = AStep(0, [new TextSegment("Melt "), new IngredientSegment(butter.Id)]);

        var result = recipe.SetContents([group], [step], Now);

        result.ShouldBeSuccess();
        Assert.Equal([butter.Id], recipe.Steps[0].Uses);
    }

    [Fact]
    public void Step_ShouldNeedAnIngredientItsWordsName_EvenWhenTheCallerDidNotListIt()
    {
        var butter = AnIngredient("butter");

        var step = AStep(0, [new TextSegment("Melt "), new IngredientSegment(butter.Id)]);

        // The words and the list cannot disagree: words are folded into the list when the step is
        // made.
        Assert.Equal([butter.Id], step.Uses);
    }

    [Fact]
    public void Step_ShouldNeedAnIngredientItsWordsDoNotName()
    {
        var flour = AnIngredient("flour");

        // "Combine everything and knead" needs flour without saying so.
        var step = AStep(0, [new TextSegment("Combine everything and knead.")], flour.Id);

        Assert.Equal([flour.Id], step.Uses);
    }

    [Fact]
    public void Step_ShouldCollapseAnIngredientListedTwice()
    {
        var butter = AnIngredient("butter");

        var step = AStep(
            0,
            [new TextSegment("Melt "), new IngredientSegment(butter.Id)],
            butter.Id,
            butter.Id);

        Assert.Equal([butter.Id], step.Uses);
    }

    [Fact]
    public void Step_ShouldRejectMoreIngredientsThanARecipeCouldHave()
    {
        var tooMany = Enumerable.Range(0, Recipe.MaxIngredients + 1)
            .Select(_ => Guid.CreateVersion7())
            .ToArray();

        var result = Step.Create(null, 0, [new TextSegment("Combine.")], tooMany, null);

        result.ShouldBeFailure(RecipeErrors.TooManyIngredients);
    }

    [Fact]
    public void SetContents_ShouldRejectAStepThatNeedsAnIngredientThisRecipeNeverHad()
    {
        var recipe = ARecipe();
        var step = AStep(0, [new TextSegment("Combine.")], Guid.CreateVersion7());

        var result = recipe.SetContents([AGroup()], [step], Now);

        result.ShouldBeFailure(RecipeErrors.UnknownIngredientReference);
    }

    [Fact]
    public void SetContents_ShouldNameTheStep_WhenAnIngredientItOnlyListedWasRemoved()
    {
        var recipe = ARecipe();
        var flour = AnIngredient("flour");
        var step = AStep(1, [new TextSegment("Combine everything and knead.")], flour.Id);
        recipe.SetContents([AGroup(flour)], [AStep(0, [new TextSegment("Preheat.")]), step], Now)
            .ShouldBeSuccess();

        var result = recipe.SetContents([AGroup()], [AStep(0, [new TextSegment("Preheat.")]), step], Now);

        // No mention to point at, so the message must name the step.
        result.ShouldBeFailure(RecipeErrors.IngredientInUse(2));
    }

    [Fact]
    public void SetContents_ShouldRejectAStepThatMentionsAnIngredientThisRecipeNeverHad()
    {
        var recipe = ARecipe();
        var step = AStep(0, [new IngredientSegment(Guid.CreateVersion7())]);

        var result = recipe.SetContents([AGroup()], [step], Now);

        result.ShouldBeFailure(RecipeErrors.UnknownIngredientReference);
    }

    [Fact]
    public void SetContents_ShouldNameTheStep_WhenAnIngredientStillInUseWasRemoved()
    {
        var recipe = ARecipe();
        var butter = AnIngredient("butter");
        var step = AStep(1, [new TextSegment("Melt "), new IngredientSegment(butter.Id)]);
        recipe.SetContents([AGroup(butter)], [AStep(0, [new TextSegment("Preheat.")]), step], Now)
            .ShouldBeSuccess();

        var result = recipe.SetContents([AGroup()], [AStep(0, [new TextSegment("Preheat.")]), step], Now);

        // An editing mistake with an obvious fix, not a malformed request.
        result.ShouldBeFailure(RecipeErrors.IngredientInUse(2));
    }

    [Fact]
    public void SetContents_ShouldRestoreTheImplicitGroup_WhenEveryGroupIsRemoved()
    {
        var recipe = ARecipe();

        var result = recipe.SetContents([], [], Now);

        result.ShouldBeSuccess();
        Assert.Null(Assert.Single(recipe.Groups).Name);
    }

    [Fact]
    public void SetContents_ShouldRefuse_WhenThereAreMoreIngredientsThanAnyoneCouldCookFrom()
    {
        var recipe = ARecipe();
        var many = Enumerable.Range(0, Recipe.MaxIngredients + 1)
            .Select(index => AnIngredient($"thing {index}"))
            .ToList();

        var result = recipe.SetContents([AGroup([.. many])], [], Now);

        result.ShouldBeFailure(RecipeErrors.TooManyIngredients);
    }

    [Fact]
    public void SetContents_ShouldRefuse_WhenTwoLinesClaimTheSameIngredientId()
    {
        var recipe = ARecipe();
        var shared = CulinaId.New();

        var result = recipe.SetContents(
            [AGroup(AnIngredient("butter", shared), AnIngredient("flour", shared))],
            [],
            Now);

        result.ShouldBeFailure(RecipeErrors.DuplicateIngredient);
    }

    [Fact]
    public void SetContents_ShouldRefuse_WhenTwoGroupsClaimTheSameId()
    {
        var recipe = ARecipe();
        var shared = CulinaId.New();

        var result = recipe.SetContents(
            [
                IngredientGroup.Create(shared, "Dough", 0, [AnIngredient("flour")]).ShouldBeSuccess(),
                IngredientGroup.Create(shared, "Filling", 1, [AnIngredient("apples")]).ShouldBeSuccess()
            ],
            [],
            Now);

        result.ShouldBeFailure(RecipeErrors.DuplicateGroup);
    }

    [Fact]
    public void SetContents_ShouldRefuse_WhenTwoStepsClaimTheSameId()
    {
        var recipe = ARecipe();
        var shared = CulinaId.New();

        var result = recipe.SetContents(
            [AGroup()],
            [
                Step.Create(shared, 0, [new TextSegment("Preheat.")], [], null).ShouldBeSuccess(),
                Step.Create(shared, 1, [new TextSegment("Bake.")], [], null).ShouldBeSuccess()
            ],
            Now);

        result.ShouldBeFailure(RecipeErrors.DuplicateStep);
    }

    [Fact]
    public void SetContents_ShouldCountLinesRatherThanIds_WhenCappingIngredients()
    {
        var recipe = ARecipe();
        var many = Enumerable.Range(0, Recipe.MaxIngredients)
            .Select(index => AnIngredient($"thing {index}"))
            .ToList();

        var result = recipe.SetContents([AGroup([.. many])], [], Now);

        result.ShouldBeSuccess();
        Assert.Equal(Recipe.MaxIngredients, recipe.Ingredients.Count());
    }

    [Fact]
    public void SetContents_ShouldRefuse_WhenThereAreMoreStepsThanAnyoneCouldFollow()
    {
        var recipe = ARecipe();
        var many = Enumerable.Range(0, Recipe.MaxSteps + 1)
            .Select(index => AStep(index, [new TextSegment($"Step {index}.")]))
            .ToList();

        var result = recipe.SetContents([AGroup()], many, Now);

        result.ShouldBeFailure(RecipeErrors.TooManySteps);
    }

    [Fact]
    public void Step_ShouldRejectAnImplausibleTimer()
    {
        var result = Step.Create(null, 0, [new TextSegment("Rest.")], [], durationSeconds: 90_000);

        result.ShouldBeFailure(RecipeErrors.InvalidDuration);
    }

    [Fact]
    public void Step_ShouldTakeATitle_SoALayeredRecipeCanNameItsParts()
    {
        var step = Step.Create(
            null,
            0,
            [new TextSegment("Rub the butter into the flour.")],
            [],
            null,
            "  Prepare the base  ").ShouldBeSuccess();

        Assert.Equal("Prepare the base", step.Title);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Step_ShouldReadABlankTitleAsNoTitle_SoClearingTheFieldGivesTheNumberBack(
        string? title)
    {
        var step = Step.Create(null, 0, [new TextSegment("Bake.")], [], null, title)
            .ShouldBeSuccess();

        Assert.Null(step.Title);
    }

    [Fact]
    public void Step_ShouldRejectATitleLongEnoughToBeTheInstruction()
    {
        var result = Step.Create(
            null,
            0,
            [new TextSegment("Bake.")],
            [],
            null,
            new string('x', Step.MaxTitleLength + 1));

        result.ShouldBeFailure(RecipeErrors.InvalidStepTitle);
    }

    [Fact]
    public void Yield_ShouldTakeARecipesOwnWord_SoACakeIsNotFourPortions()
    {
        var measure = Yield.Create(1m, YieldKind.Servings, "  Cake  ").ShouldBeSuccess();

        Assert.Equal("Cake", measure.Label);
        // Only the wording changes; the kind still drives how the servings control counts.
        Assert.Equal(YieldKind.Servings, measure.Kind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Yield_ShouldReadABlankWordAsNoWord_SoAnEmptiedFieldIsNotAYieldOfNothing(
        string? label)
    {
        var measure = Yield.Create(4m, YieldKind.Servings, label).ShouldBeSuccess();

        Assert.Null(measure.Label);
    }

    [Fact]
    public void Yield_ShouldRejectAWordLongEnoughToBeASentence()
    {
        var result = Yield.Create(
            4m,
            YieldKind.Servings,
            new string('x', Yield.MaxLabelLength + 1));

        result.ShouldBeFailure(RecipeErrors.InvalidYieldLabel);
    }

    [Fact]
    public void Ingredient_ShouldKeepThePreparationOutOfTheName_SoShoppingCanMerge()
    {
        var ingredient = RecipeIngredient.Create(
            null,
            0,
            Quantity.Create(200m, Unit.Gram).ShouldBeSuccess(),
            "  butter  ",
            "  finely chopped  ").ShouldBeSuccess();

        Assert.Equal("butter", ingredient.Name);
        Assert.Equal("finely chopped", ingredient.Note);
    }

    [Fact]
    public void CopyInto_ShouldGiveTheCopyLinesOfItsOwn_WithEveryStepPointingAtThem()
    {
        var recipe = ARecipe();
        recipe.Describe(Details(prep: 15, cook: 60) with { Tags = ["pasta"] }, Now).ShouldBeSuccess();
        var butter = AnIngredient("Butter");
        var salt = AnIngredient("Salt");
        recipe.SetContents(
            [AGroup(butter, salt)],
            [AStep(0, [new TextSegment("Melt "), new IngredientSegment(butter.Id)], salt.Id)],
            Now).ShouldBeSuccess();
        var flat = Guid.CreateVersion7();

        var copy = recipe.CopyInto(flat, Author, Now).ShouldBeSuccess();

        Assert.NotEqual(recipe.Id, copy.Id);
        Assert.Equal(flat, copy.HouseholdId);
        Assert.Equal(["Butter", "Salt"], copy.Ingredients.Select(line => line.Name));
        Assert.Equal(["pasta"], copy.Tags);
        Assert.Equal(15, copy.PrepMinutes);

        // The copy's steps must point at the copy's own lines, not the original's.
        var copied = copy.Ingredients.ToDictionary(line => line.Name, line => line.Id);
        Assert.DoesNotContain(copy.Ingredients, line => line.Id == butter.Id || line.Id == salt.Id);
        var step = Assert.Single(copy.Steps);
        Assert.Equal(copied["Butter"], Assert.IsType<IngredientSegment>(step.Segments[1]).RecipeIngredientId);
        Assert.Contains(copied["Salt"], step.Uses);
    }

    [Fact]
    public void CopyInto_ShouldLeaveTheOriginalAsItWas()
    {
        var recipe = ARecipe();
        var butter = AnIngredient("Butter");
        recipe.SetContents([AGroup(butter)], [], Now).ShouldBeSuccess();

        recipe.CopyInto(Guid.CreateVersion7(), Author, Now).ShouldBeSuccess();

        Assert.Equal(Household, recipe.HouseholdId);
        Assert.Equal(butter.Id, Assert.Single(recipe.Ingredients).Id);
    }

    private static Recipe ARecipe() =>
        Recipe.Create(
            Household,
            RecipeTitle.Create("Bolognese").ShouldBeSuccess(),
            Author,
            Language.En,
            Now);

    private static RecipeDetails Details(
        int? prep,
        int? cook,
        string? description = null,
        IReadOnlyList<string>? tags = null) => new(
        RecipeTitle.Create("Bolognese").ShouldBeSuccess(),
        description,
        Language.En,
        Yield.Default,
        prep,
        cook,
        tags ?? []);

    private static RecipeIngredient AnIngredient(string name, Guid? id = null) =>
        RecipeIngredient.Create(id, 0, Quantity.Unmeasured, name, null).ShouldBeSuccess();

    private static IngredientGroup AGroup(params RecipeIngredient[] ingredients) =>
        IngredientGroup.Create(null, null, 0, ingredients).ShouldBeSuccess();

    private static Step AStep(int sortOrder, StepSegment[] segments, params Guid[] uses) =>
        Step.Create(null, sortOrder, segments, uses, null).ShouldBeSuccess();
}
