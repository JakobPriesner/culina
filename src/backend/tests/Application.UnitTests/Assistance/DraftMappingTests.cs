using Application.Abstractions;
using Application.Assistance;

namespace Application.UnitTests.Assistance;

/// <summary>
/// What a model's answer is allowed to get wrong: leniency, since a draft losing one bad line beats
/// a refusal losing nineteen good ones.
/// </summary>
public class DraftMappingTests
{
    [Fact]
    public void ToResponse_ShouldKeepTheAmountAndDropTheUnit_WhenTheUnitCouldNeverBeOne()
    {
        // The classic: an amount that lost its space, arriving as a unit.
        var draft = Recipe(Ingredient(200, "200g", "flour"));

        var response = draft.ToResponse();

        var line = response.Groups[0].Ingredients[0];
        Assert.Equal(200, line.Quantity);
        Assert.Null(line.Unit);
        Assert.Equal("flour", line.Name);
    }

    [Fact]
    public void ToResponse_ShouldKeepAUnitTheAppHasNeverSeen_BecauseAUnitIsAnyWord()
    {
        var response = Recipe(Ingredient(1, "Schuss", "Milch")).ToResponse();

        // The domain accepts any word, so steering the model to familiar ones must not refuse the
        // rest.
        Assert.Equal("Schuss", response.Groups[0].Ingredients[0].Unit);
    }

    [Fact]
    public void ToResponse_ShouldDropALineWithNoIngredient_BecauseNobodyCouldFixIt()
    {
        var response = Recipe(
            Ingredient(200, "g", "flour"),
            Ingredient(2, "tbsp", name: null)).ToResponse();

        // An amount with no noun is a mistake the person could not repair without knowing what the
        // model meant.
        Assert.Single(response.Groups[0].Ingredients);
    }

    [Fact]
    public void ToResponse_ShouldDropATimeNoRecipeCouldHave_RatherThanClampIt()
    {
        var draft = new DraftedRecipe { Title = "Soup", PrepMinutes = 999_999 };

        var response = draft.ToResponse();

        // Clamping would turn nonsense into a plausible number; a blank is obviously a blank.
        Assert.Null(response.PrepMinutes);
    }

    [Fact]
    public void ToResponse_ShouldDropAGroupThatEndedUpEmpty()
    {
        var response = Recipe(Ingredient(1, null, name: null)).ToResponse();

        Assert.Empty(response.Groups);
    }

    [Fact]
    public void ToResponse_ShouldDropAStepWithNoWords()
    {
        var draft = new DraftedRecipe
        {
            Title = "Soup",
            Steps = [new DraftedStep { Text = "  " }, new DraftedStep { Text = "Boil it." }]
        };

        var response = draft.ToResponse();

        Assert.Equal(["Boil it."], response.Steps.Select(step => step.Text));
    }

    [Fact]
    public void IsUsable_ShouldBeFalse_ForAnAnswerWithNothingInIt()
    {
        // Not a draft to correct but a model that did not answer: the one thing worth refusing.
        Assert.False(new DraftedRecipe().IsUsable());
    }

    [Theory]
    [InlineData("Soup", false)]
    [InlineData(null, true)]
    public void IsUsable_ShouldBeTrue_WithEitherATitleOrSomeContent(string? title, bool withSteps)
    {
        var draft = new DraftedRecipe
        {
            Title = title,
            Steps = withSteps ? [new DraftedStep { Text = "Boil it." }] : []
        };

        Assert.True(draft.IsUsable());
    }

    private static DraftedRecipe Recipe(params DraftedIngredient[] lines) => new()
    {
        Title = "Soup",
        Groups = [new DraftedGroup { Ingredients = lines }]
    };

    private static DraftedIngredient Ingredient(decimal? quantity, string? unit, string? name) =>
        new() { Quantity = quantity, Unit = unit, Name = name };
}
