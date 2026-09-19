using Application.Abstractions;
using Application.Assistance;

namespace Application.UnitTests.Assistance;

/// <summary>
/// What a model's answer is allowed to get wrong.
/// </summary>
/// <remarks>
/// The property under test is leniency, which is unusual for this codebase and
/// is the point: nothing is being written down, so a draft that loses one bad
/// line is worth far more than a refusal that loses nineteen good ones.
/// </remarks>
public class DraftMappingTests
{
    [Fact]
    public void ToResponse_ShouldKeepTheAmountAndDropTheUnit_WhenTheUnitCouldNeverBeOne()
    {
        // Arrange
        // The classic: an amount that lost its space, arriving as a unit.
        var draft = Recipe(Ingredient(200, "200g", "flour"));

        // Act
        var response = draft.ToResponse();

        // Assert
        var line = response.Groups[0].Ingredients[0];
        Assert.Equal(200, line.Quantity);
        Assert.Null(line.Unit);
        Assert.Equal("flour", line.Name);
    }

    [Fact]
    public void ToResponse_ShouldKeepAUnitTheAppHasNeverSeen_BecauseAUnitIsAnyWord()
    {
        // Act
        var response = Recipe(Ingredient(1, "Schuss", "Milch")).ToResponse();

        // Assert
        // The domain accepts any word, so steering the model towards the
        // familiar ones must not mean refusing the rest.
        Assert.Equal("Schuss", response.Groups[0].Ingredients[0].Unit);
    }

    [Fact]
    public void ToResponse_ShouldDropALineWithNoIngredient_BecauseNobodyCouldFixIt()
    {
        // Act
        var response = Recipe(
            Ingredient(200, "g", "flour"),
            Ingredient(2, "tbsp", name: null)).ToResponse();

        // Assert
        // An amount with no noun is not a shorter line, it is a mistake — and
        // one the person correcting the draft could not repair without knowing
        // what the model meant.
        Assert.Single(response.Groups[0].Ingredients);
    }

    [Fact]
    public void ToResponse_ShouldDropATimeNoRecipeCouldHave_RatherThanClampIt()
    {
        // Arrange
        var draft = new DraftedRecipe { Title = "Soup", PrepMinutes = 999_999 };

        // Act
        var response = draft.ToResponse();

        // Assert
        // Clamping would turn nonsense into a plausible number somebody might
        // not check. A blank is obviously a blank.
        Assert.Null(response.PrepMinutes);
    }

    [Fact]
    public void ToResponse_ShouldDropAGroupThatEndedUpEmpty()
    {
        // Act
        var response = Recipe(Ingredient(1, null, name: null)).ToResponse();

        // Assert
        Assert.Empty(response.Groups);
    }

    [Fact]
    public void ToResponse_ShouldDropAStepWithNoWords()
    {
        // Arrange
        var draft = new DraftedRecipe
        {
            Title = "Soup",
            Steps = [new DraftedStep { Text = "  " }, new DraftedStep { Text = "Boil it." }]
        };

        // Act
        var response = draft.ToResponse();

        // Assert
        Assert.Equal(["Boil it."], response.Steps.Select(step => step.Text));
    }

    [Fact]
    public void IsUsable_ShouldBeFalse_ForAnAnswerWithNothingInIt()
    {
        // Assert
        // Not a draft to correct — a model that did not answer. The one thing
        // worth refusing, so somebody can ask again.
        Assert.False(new DraftedRecipe().IsUsable());
    }

    [Theory]
    [InlineData("Soup", false)]
    [InlineData(null, true)]
    public void IsUsable_ShouldBeTrue_WithEitherATitleOrSomeContent(string? title, bool withSteps)
    {
        // Arrange
        var draft = new DraftedRecipe
        {
            Title = title,
            Steps = withSteps ? [new DraftedStep { Text = "Boil it." }] : []
        };

        // Assert
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
