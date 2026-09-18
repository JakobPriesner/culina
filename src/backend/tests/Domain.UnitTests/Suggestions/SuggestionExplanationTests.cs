using Domain.Suggestions;

namespace Domain.UnitTests.Suggestions;

/// <summary>
/// The promise that a reason is a fact rather than a sentence.
/// </summary>
/// <remarks>
/// These are not tests of a formatter. They are the tests of a product promise:
/// an explanation is the term that actually won, or there is none — because an
/// invented reason, caught once, discredits every reason that was true.
/// </remarks>
public class SuggestionExplanationTests
{
    private static readonly RankingWeights Weights = RankingWeights.Default;

    [Fact]
    public void For_ShouldNameTheDominantTerm_WhenOneCarriedTheScore()
    {
        // Arrange
        List<ScoreTerm> terms =
        [
            new(SuggestionReason.Rediscovery, 0.9m, null),
            new(SuggestionReason.Tag, 0.1m, "suppe"),
            new(SuggestionReason.Fresh, 0.05m, null)
        ];

        // Act
        var reason = SuggestionExplanation.For(terms, Weights);

        // Assert
        Assert.Equal(SuggestionReason.Rediscovery, reason.Reason);
    }

    [Fact]
    public void For_ShouldSayNothing_WhenTheScoreWasACommitteeRatherThanADecision()
    {
        // Arrange
        // Four terms of roughly equal weight: the recipe is a good suggestion
        // and there is no honest one-line answer to "why this one".
        List<ScoreTerm> terms =
        [
            new(SuggestionReason.Affinity, 0.25m, null),
            new(SuggestionReason.Tag, 0.25m, "suppe"),
            new(SuggestionReason.Slot, 0.25m, null),
            new(SuggestionReason.Fresh, 0.25m, null)
        ];

        // Act
        var reason = SuggestionExplanation.For(terms, Weights);

        // Assert
        // Saying nothing is the correct answer, not a gap to be filled.
        Assert.Equal(SuggestionReason.None, reason.Reason);
    }

    [Fact]
    public void For_ShouldIgnoreNegativeTerms_BecauseTheyExplainWhySomethingIsNotSuggested()
    {
        // Arrange
        // The repetition penalty is huge here, and it is still not a reason:
        // "you had this on Tuesday" printed on a card that is being suggested
        // is nonsense.
        List<ScoreTerm> terms =
        [
            new(SuggestionReason.None, -5.0m, null),
            new(SuggestionReason.Tag, 0.4m, "suppe")
        ];

        // Act
        var reason = SuggestionExplanation.For(terms, Weights);

        // Assert
        Assert.Equal(SuggestionReason.Tag, reason.Reason);
        Assert.Equal("suppe", reason.Subject);
    }

    [Fact]
    public void For_ShouldSayNothing_WhenEveryTermIsNegative()
    {
        // Arrange
        // Still a legitimate suggestion — a saturated household has nothing but
        // recipes it ate recently — and still nothing good to say about it.
        List<ScoreTerm> terms = [new(SuggestionReason.Affinity, -0.2m, null)];

        // Act
        var reason = SuggestionExplanation.For(terms, Weights);

        // Assert
        Assert.Equal(SuggestionReason.None, reason.Reason);
    }

    [Fact]
    public void For_ShouldSayNothing_WhenThereAreNoTermsAtAll()
    {
        // Act
        var reason = SuggestionExplanation.For([], Weights);

        // Assert
        Assert.Equal(SuggestionReason.None, reason.Reason);
    }

    [Theory]
    [InlineData(SuggestionReason.Tag)]
    [InlineData(SuggestionReason.Ingredient)]
    [InlineData(SuggestionReason.Household)]
    public void For_ShouldSayNothing_WhenTheWinningTermCannotNameWhatItIsAbout(SuggestionReason reason)
    {
        // Arrange
        // "You often cook ___" with a hole in it is worse than no line at all.
        List<ScoreTerm> terms = [new(reason, 1.0m, null)];

        // Act
        var chosen = SuggestionExplanation.For(terms, Weights);

        // Assert
        Assert.Equal(SuggestionReason.None, chosen.Reason);
    }

    [Fact]
    public void For_ShouldNotNeedASubject_WhenTheReasonIsAboutTheOccasionRatherThanAThing()
    {
        // Arrange
        // "Not since April" is answered by the card's own last-cooked date, so
        // the term carries no subject and still explains itself.
        List<ScoreTerm> terms = [new(SuggestionReason.Rediscovery, 1.0m, null)];

        // Act
        var reason = SuggestionExplanation.For(terms, Weights);

        // Assert
        Assert.Equal(SuggestionReason.Rediscovery, reason.Reason);
    }

    [Fact]
    public void For_ShouldFollowTheThreshold_WhenItIsRaised()
    {
        // Arrange
        // The dominance share is a weight like any other, so a stricter
        // installation says less rather than saying it differently.
        List<ScoreTerm> terms =
        [
            new(SuggestionReason.Affinity, 0.5m, null),
            new(SuggestionReason.Tag, 0.5m, "suppe")
        ];

        // Act
        var lenient = SuggestionExplanation.For(terms, Weights with { ReasonDominance = 0.45m });
        var strict = SuggestionExplanation.For(terms, Weights with { ReasonDominance = 0.75m });

        // Assert
        Assert.Equal(SuggestionReason.Affinity, lenient.Reason);
        Assert.Equal(SuggestionReason.None, strict.Reason);
    }
}
