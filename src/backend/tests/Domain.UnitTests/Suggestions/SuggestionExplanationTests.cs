using Domain.Suggestions;

namespace Domain.UnitTests.Suggestions;

/// <summary>
/// A reason is a fact, not a sentence: the term that actually won, or none (an invented reason
/// discredits true ones).
/// </summary>
public class SuggestionExplanationTests
{
    private static readonly RankingWeights Weights = RankingWeights.Default;

    [Fact]
    public void For_ShouldNameTheDominantTerm_WhenOneCarriedTheScore()
    {
        List<ScoreTerm> terms =
        [
            new(SuggestionReason.Rediscovery, 0.9m, null),
            new(SuggestionReason.Tag, 0.1m, "suppe"),
            new(SuggestionReason.Fresh, 0.05m, null)
        ];

        var reason = SuggestionExplanation.For(terms, Weights);

        Assert.Equal(SuggestionReason.Rediscovery, reason.Reason);
    }

    [Fact]
    public void For_ShouldSayNothing_WhenTheScoreWasACommitteeRatherThanADecision()
    {
        // Four terms of roughly equal weight: no honest one-line answer to "why this one".
        List<ScoreTerm> terms =
        [
            new(SuggestionReason.Affinity, 0.25m, null),
            new(SuggestionReason.Tag, 0.25m, "suppe"),
            new(SuggestionReason.Slot, 0.25m, null),
            new(SuggestionReason.Fresh, 0.25m, null)
        ];

        var reason = SuggestionExplanation.For(terms, Weights);

        Assert.Equal(SuggestionReason.None, reason.Reason);
    }

    [Fact]
    public void For_ShouldIgnoreNegativeTerms_BecauseTheyExplainWhySomethingIsNotSuggested()
    {
        // Even a huge repetition penalty is not a reason: "you had this on Tuesday" on a suggestion
        // is nonsense.
        List<ScoreTerm> terms =
        [
            new(SuggestionReason.None, -5.0m, null),
            new(SuggestionReason.Tag, 0.4m, "suppe")
        ];

        var reason = SuggestionExplanation.For(terms, Weights);

        Assert.Equal(SuggestionReason.Tag, reason.Reason);
        Assert.Equal("suppe", reason.Subject);
    }

    [Fact]
    public void For_ShouldSayNothing_WhenEveryTermIsNegative()
    {
        // Still a legitimate suggestion, with nothing good to say about it.
        List<ScoreTerm> terms = [new(SuggestionReason.Affinity, -0.2m, null)];

        var reason = SuggestionExplanation.For(terms, Weights);

        Assert.Equal(SuggestionReason.None, reason.Reason);
    }

    [Fact]
    public void For_ShouldSayNothing_WhenThereAreNoTermsAtAll()
    {
        var reason = SuggestionExplanation.For([], Weights);

        Assert.Equal(SuggestionReason.None, reason.Reason);
    }

    [Theory]
    [InlineData(SuggestionReason.Tag)]
    [InlineData(SuggestionReason.Ingredient)]
    [InlineData(SuggestionReason.Household)]
    public void For_ShouldSayNothing_WhenTheWinningTermCannotNameWhatItIsAbout(SuggestionReason reason)
    {
        // "You often cook ___" with a hole is worse than no line.
        List<ScoreTerm> terms = [new(reason, 1.0m, null)];

        var chosen = SuggestionExplanation.For(terms, Weights);

        Assert.Equal(SuggestionReason.None, chosen.Reason);
    }

    [Fact]
    public void For_ShouldNotNeedASubject_WhenTheReasonIsAboutTheOccasionRatherThanAThing()
    {
        // "Not since April" is answered by the card's own date, so the term needs no subject.
        List<ScoreTerm> terms = [new(SuggestionReason.Rediscovery, 1.0m, null)];

        var reason = SuggestionExplanation.For(terms, Weights);

        Assert.Equal(SuggestionReason.Rediscovery, reason.Reason);
    }

    [Fact]
    public void For_ShouldFollowTheThreshold_WhenItIsRaised()
    {
        // The dominance share is a weight like any other: a stricter installation says less.
        List<ScoreTerm> terms =
        [
            new(SuggestionReason.Affinity, 0.5m, null),
            new(SuggestionReason.Tag, 0.5m, "suppe")
        ];

        var lenient = SuggestionExplanation.For(terms, Weights with { ReasonDominance = 0.45m });
        var strict = SuggestionExplanation.For(terms, Weights with { ReasonDominance = 0.75m });

        Assert.Equal(SuggestionReason.Affinity, lenient.Reason);
        Assert.Equal(SuggestionReason.None, strict.Reason);
    }
}
