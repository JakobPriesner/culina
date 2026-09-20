using System.Globalization;
using Application.Abstractions;
using Application.Assistance;

namespace Application.UnitTests.Assistance;

/// <summary>
/// That narrowing a catalogue never leaves an administrator with nothing.
/// </summary>
public class ModelCatalogueTests
{
    private static ModelInfo Model(string id) => new(id, id, CanDraw: false);

    [Fact]
    public void Narrow_ShouldDropWhatCannotWriteARecipe()
    {
        // Arrange
        IReadOnlyList<ModelInfo> offered =
            [Model("gpt-5.5"), Model("text-embedding-3-large"), Model("tts-1")];

        // Act
        var kept = ModelCatalogue.Narrow(offered, model => !model.Id.Contains("tts") &&
            !model.Id.Contains("embedding"));

        // Assert
        Assert.Equal(["gpt-5.5"], kept.Select(model => model.Id));
    }

    [Fact]
    public void Narrow_ShouldKeepEverything_WhenTheRuleWouldKeepNothing()
    {
        // Arrange
        // A real key, whose project could reach the speech models and nothing
        // else. Filtered to nothing, the screen said the provider had listed
        // no models at all — which sent the administrator to look at the wrong
        // thing. These six on screen say what is wrong in one glance.
        IReadOnlyList<ModelInfo> offered =
            [Model("tts-1"), Model("tts-1-hd"), Model("gpt-4o-mini-tts")];

        // Act
        var kept = ModelCatalogue.Narrow(offered, model => !model.Id.Contains("tts"));

        // Assert
        Assert.Equal(offered, kept);
    }

    [Fact]
    public void Narrow_ShouldLeaveAnEmptyCatalogueEmpty()
    {
        // Nothing offered is nothing to show, and no rule changes that.
        Assert.Empty(ModelCatalogue.Narrow([], _ => true));
    }

    [Fact]
    public void Newest_ShouldPutTheMostRecentlyPublishedFirst()
    {
        // Arrange
        // Alphabetical buries the thing somebody came for: gpt-image-2.5 sorts
        // above gpt-6, and a dated snapshot sorts beside its family whether it
        // is a year old or a day.
        IReadOnlyList<ModelInfo> offered =
        [
            Dated("gpt-4o-mini", "2024-07-18"),
            Dated("gpt-image-2.5-flare", "2026-04-02"),
            Dated("gpt-6-astra", "2026-08-11")
        ];

        // Act
        var ordered = ModelCatalogue.Newest(offered);

        // Assert
        Assert.Equal(
            ["gpt-6-astra", "gpt-image-2.5-flare", "gpt-4o-mini"],
            ordered.Select(model => model.Id));
    }

    [Fact]
    public void Newest_ShouldKeepNameOrder_ForAProviderThatDatesNothing()
    {
        // Arrange
        // Google's listing carries no date. An order that looked meaningful
        // here would be one this app had made up.
        IReadOnlyList<ModelInfo> offered =
            [Model("gemini-3-pro"), Model("gemini-2.5-flash"), Model("gemma-4-31b-it")];

        // Act
        var ordered = ModelCatalogue.Newest(offered);

        // Assert
        Assert.Equal(
            ["gemini-2.5-flash", "gemini-3-pro", "gemma-4-31b-it"],
            ordered.Select(model => model.Id));
    }

    [Fact]
    public void Newest_ShouldPutADatedModelAboveAnUndatedOne()
    {
        // Arrange
        // Not a case any single provider produces — a listing either carries
        // dates or does not — but the rule should be stated rather than left
        // to whichever comparer happens to run.
        IReadOnlyList<ModelInfo> offered = [Model("aaa-no-date"), Dated("zzz-dated", "2020-01-01")];

        // Act
        var ordered = ModelCatalogue.Newest(offered);

        // Assert
        Assert.Equal(["zzz-dated", "aaa-no-date"], ordered.Select(model => model.Id));
    }

    private static ModelInfo Dated(string id, string added) =>
        new(id, id, CanDraw: false, DateTimeOffset.Parse(added, CultureInfo.InvariantCulture));
}
