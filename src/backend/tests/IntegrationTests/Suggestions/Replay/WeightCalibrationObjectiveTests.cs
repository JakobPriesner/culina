namespace IntegrationTests.Suggestions.Replay;

/// <summary>What a calibration step is asked to raise, on hand-written scores; no database.</summary>
public class WeightCalibrationObjectiveTests
{
    [Fact]
    public void ObjectiveOf_ShouldBeRecall_WhenRepetitionIsWithinTheCeiling()
    {
        var score = Score(recall: 0.40, repetition: WeightCalibration.RepetitionCeiling);

        Assert.Equal(0.40, WeightCalibration.ObjectiveOf(score), precision: 6);
    }

    [Fact]
    public void ObjectiveOf_ShouldTakeAPointOfRecallPerPointOverTheCeiling()
    {
        var score = Score(recall: 0.40, repetition: WeightCalibration.RepetitionCeiling + 0.20);

        Assert.Equal(0.20, WeightCalibration.ObjectiveOf(score), precision: 6);
    }

    [Fact]
    public void ObjectiveOf_ShouldPreferAVarierListThatGivesUpALittleRecall()
    {
        var staples = Score(recall: 0.40, repetition: 0.80);
        var varied = Score(recall: 0.34, repetition: 0.55);

        Assert.True(WeightCalibration.ObjectiveOf(varied) > WeightCalibration.ObjectiveOf(staples));
    }

    [Fact]
    public void ObjectiveOf_ShouldNotPayForVarietyBeyondTheCeiling()
    {
        var steady = Score(recall: 0.40, repetition: WeightCalibration.RepetitionCeiling);
        var shuffled = Score(recall: 0.30, repetition: 0.10);

        Assert.True(WeightCalibration.ObjectiveOf(steady) > WeightCalibration.ObjectiveOf(shuffled));
    }

    private static ReplayScore Score(double recall, double repetition) =>
        new(Points: 100, recall, RecallAt10: 0, ReciprocalRank: 0, ChanceAt5: 0, new ShortlistQuality(0, repetition, 0, 0));
}
