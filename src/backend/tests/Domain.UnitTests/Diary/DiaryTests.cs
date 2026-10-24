using Domain.Diary;
using Domain.Nutrition;
using TestSupport;

namespace Domain.UnitTests.Diary;

/// <summary>What a diary entry and the diary's settings will and will not accept, and how a day adds up.</summary>
public class DiaryTests
{
    private static readonly Guid Ada = Guid.Parse("0196b6d4-0000-7000-8000-000000000001");
    private static readonly DateOnly Thursday = new(2026, 10, 22);
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Settings_ShouldRefuseToSwitchOn_WithoutAgreementToTheCurrentWording()
    {
        var off = DiarySettings.Default(Ada);

        off.Change(true, consentVersion: null, null, null, DiaryHeadline.Eaten, Now)
            .ShouldBeFailure(DiaryErrors.ConsentRequired);
        off.Change(true, consentVersion: 0, null, null, DiaryHeadline.Eaten, Now)
            .ShouldBeFailure(DiaryErrors.ConsentRequired);

        var on = off.Change(true, DiarySettings.CurrentConsentVersion, 2000, 120, DiaryHeadline.Eaten, Now)
            .ShouldBeSuccess();

        Assert.True(on.Enabled);
        Assert.Equal(Now, on.ConsentedAt);
        Assert.Equal(DiarySettings.CurrentConsentVersion, on.ConsentVersion);
    }

    [Fact]
    public void Settings_ShouldKeepTheRecordOfAgreement_WhenSwitchedOffAndChangedAgain()
    {
        var on = DiarySettings.Default(Ada)
            .Change(true, DiarySettings.CurrentConsentVersion, null, null, DiaryHeadline.Eaten, Now)
            .ShouldBeSuccess();

        var off = on.Change(false, null, null, null, DiaryHeadline.Eaten, Now.AddDays(3)).ShouldBeSuccess();
        var stillOn = on.Change(true, null, 1800, null, DiaryHeadline.Remaining, Now.AddDays(1)).ShouldBeSuccess();

        // Agreement is recorded when switching on, not on every later change.
        Assert.Equal(Now, off.ConsentedAt);
        Assert.Equal(Now, stillOn.ConsentedAt);
        Assert.Equal(1800, stillOn.KcalTarget);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(10001, null)]
    [InlineData(null, 0)]
    [InlineData(null, 1001)]
    public void Settings_ShouldRefuseATargetNoDayCouldHave(int? kcal, int? protein)
    {
        DiarySettings.Default(Ada)
            .Change(false, null, kcal, protein, DiaryHeadline.Eaten, Now)
            .ShouldBeFailure(DiaryErrors.InvalidTarget);
    }

    [Fact]
    public void Settings_ShouldAcceptALowTarget_ForTheClientToAnswerCalmly()
    {
        // A floor is a sentence and a link in the client, never a refusal.
        DiarySettings.Default(Ada).Change(false, null, 900, null, DiaryHeadline.Eaten, Now).ShouldBeSuccess();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(21)]
    public void ARecipeEntry_ShouldRefuseAnImplausibleNumberOfPortions(decimal portions)
    {
        DiaryEntry.OfRecipe(
                Ada, Thursday, DiaryMeal.Lunch, DiaryLink.None, portions, "Brot", DiaryFigures.Unknown,
                DiaryTrust.Exact, 1, Now)
            .ShouldBeFailure(DiaryErrors.InvalidAmount);
    }

    [Fact]
    public void AFoodEntry_ShouldRefuseAnAmountOutsideWhatSomeoneEats()
    {
        DiaryEntry.OfFood(
                Ada, Thursday, DiaryMeal.Snack, DiaryLink.None, new DiaryAmount(6, "kg", 6000), "Mehl",
                DiaryFigures.Unknown, DiaryTrust.Exact, 1, Now)
            .ShouldBeFailure(DiaryErrors.InvalidAmount);
    }

    [Fact]
    public void ATypedEntry_ShouldNeedANameAndPlausibleEnergy_AndKeepUnknownMacrosUnknown()
    {
        DiaryEntry.Typed(Ada, Thursday, DiaryMeal.Lunch, "  ", Figures(500), 1, Now)
            .ShouldBeFailure(DiaryErrors.Implausible);
        DiaryEntry.Typed(Ada, Thursday, DiaryMeal.Lunch, "Ein Berg", Figures(20000), 1, Now)
            .ShouldBeFailure(DiaryErrors.Implausible);

        var entry = DiaryEntry.Typed(Ada, Thursday, DiaryMeal.Lunch, " Café-Lunch ", Figures(650), 1, Now)
            .ShouldBeSuccess();

        Assert.Equal("Café-Lunch", entry.Label);
        Assert.Equal(DiarySource.Typed, entry.Source);
        Assert.Null(entry.Figures.Protein);
    }

    [Fact]
    public void AnEntry_ShouldRefuseADayTheDiaryCannotHold()
    {
        DiaryEntry.Typed(Ada, new DateOnly(1999, 12, 31), DiaryMeal.Lunch, "Kuchen", Figures(400), 1, Now)
            .ShouldBeFailure(DiaryErrors.InvalidDates);
    }

    [Fact]
    public void Figures_ShouldScaleALabelPer100Grams_ToTheGramsEaten()
    {
        var oats = new Nutrients(1555m, 372m, 7m, 1.3m, 59m, 0.7m, 13.5m, 0.01m);

        var half = DiaryFigures.Of(oats, 50m);

        Assert.Equal(186m, half.Kcal);
        Assert.Equal(6.75m, half.Protein);
        Assert.Equal(0.005m, half.Salt);
    }

    [Fact]
    public void Figures_ShouldAddWhatIsKnown_AndLeaveWhatNobodyKnowsUnknown()
    {
        var sum = DiaryFigures.Sum([Figures(300, protein: 10m), Figures(200), DiaryFigures.Unknown]);

        Assert.Equal(500m, sum.Kcal);
        Assert.Equal(10m, sum.Protein);
        Assert.Null(sum.Fat);
    }

    [Fact]
    public void Moving_ShouldKeepTheFiguresAsLogged()
    {
        var entry = DiaryEntry.Typed(Ada, Thursday, DiaryMeal.Lunch, "Kuchen", Figures(400), 1, Now)
            .ShouldBeSuccess();

        var moved = entry.MovedTo(Thursday.AddDays(1), DiaryMeal.Snack);

        Assert.Equal(entry.Id, moved.Id);
        Assert.Equal(entry.Figures, moved.Figures);
        Assert.Equal(DiaryMeal.Snack, moved.Meal);
        Assert.Equal(entry.Version + 1, moved.Version);
    }

    private static DiaryFigures Figures(decimal kcal, decimal? protein = null) =>
        new(kcal, protein, null, null, null, null, null);
}
