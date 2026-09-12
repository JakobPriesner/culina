using Domain.Recipes;
using TestSupport;

namespace Domain.UnitTests.Recipes;

public class StepTextTests
{
    private static readonly Guid Butter = Guid.CreateVersion7();
    private static readonly Guid Flour = Guid.CreateVersion7();

    [Fact]
    public void Parse_ShouldSplitTextAndReferences_WhenAStepMentionsAnIngredient()
    {
        // Arrange
        var stored = $"Melt [[ingredient:{Butter}]] in the pan.";

        // Act
        var segments = StepText.Parse(stored).ShouldBeSuccess();

        // Assert
        Assert.Collection(
            segments,
            segment => Assert.Equal("Melt ", Assert.IsType<TextSegment>(segment).Value),
            segment => Assert.Equal(Butter, Assert.IsType<IngredientSegment>(segment).RecipeIngredientId),
            segment => Assert.Equal(" in the pan.", Assert.IsType<TextSegment>(segment).Value));
    }

    [Fact]
    public void Parse_ShouldReturnOneTextSegment_WhenNothingIsReferenced()
    {
        // Arrange
        const string stored = "Preheat the oven.";

        // Act
        var segments = StepText.Parse(stored).ShouldBeSuccess();

        // Assert
        // A recipe with no links is an ordinary recipe; the feature is
        // invisible until it helps.
        var only = Assert.Single(segments);
        Assert.Equal("Preheat the oven.", Assert.IsType<TextSegment>(only).Value);
    }

    [Fact]
    public void SerialiseThenParse_ShouldReturnTheSameSegments_ForAnyMixture()
    {
        // Arrange
        StepSegment[] original =
        [
            new TextSegment("Whisk "),
            new IngredientSegment(Flour),
            new TextSegment(" into "),
            new IngredientSegment(Butter),
            new TextSegment(".")
        ];

        // Act
        var roundTripped = StepText.Parse(StepText.Serialise(original)).ShouldBeSuccess();

        // Assert
        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void SerialiseThenParse_ShouldPreserveLiteralBrackets_SoTextIsNotReadAsAReference()
    {
        // Arrange
        StepSegment[] original = [new TextSegment("Use [[whatever]] you have.")];

        // Act
        var stored = StepText.Serialise(original);
        var roundTripped = StepText.Parse(stored).ShouldBeSuccess();

        // Assert
        Assert.Equal(original, roundTripped);
    }

    [Theory]
    [InlineData("Melt [[ingredient:not-a-guid]] gently.")]
    [InlineData("Melt [[ingredient:0f1c")]
    public void Parse_ShouldFail_WhenAReferenceIsMalformed(string stored)
    {
        // Arrange & Act
        var result = StepText.Parse(stored);

        // Assert
        result.ShouldBeFailure(RecipeErrors.UnknownIngredientReference);
    }

    [Fact]
    public void ReferencedIngredients_ShouldListEachIngredientOnce_EvenWhenMentionedTwice()
    {
        // Arrange
        var segments = StepText.Parse(
            $"Add [[ingredient:{Butter}]], then the rest of the [[ingredient:{Butter}]].")
            .ShouldBeSuccess();

        // Act
        var referenced = StepText.ReferencedIngredients(segments);

        // Assert
        Assert.Equal([Butter], referenced);
    }

    [Fact]
    public void PlainText_ShouldDropReferences_SoSearchIndexesWordsNotIds()
    {
        // Arrange
        var segments = StepText.Parse($"Melt [[ingredient:{Butter}]] in the pan.").ShouldBeSuccess();

        // Act
        var plain = StepText.PlainText(segments);

        // Assert
        Assert.Equal("Melt  in the pan.", plain);
    }
}
