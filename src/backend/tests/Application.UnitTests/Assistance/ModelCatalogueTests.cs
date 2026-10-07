using System.Globalization;
using Application.Abstractions;
using Application.Assistance;

namespace Application.UnitTests.Assistance;

/// <summary>Narrowing a catalogue never leaves an administrator with nothing.</summary>
public class ModelCatalogueTests
{
    private static ModelInfo Model(string id) => new(id, id, CanDraw: false);

    [Fact]
    public void Narrow_ShouldDropWhatCannotWriteARecipe()
    {
        IReadOnlyList<ModelInfo> offered =
            [Model("gpt-5.5"), Model("text-embedding-3-large"), Model("tts-1")];

        var kept = ModelCatalogue.Narrow(offered, model => !model.Id.Contains("tts") &&
            !model.Id.Contains("embedding"));

        Assert.Equal(["gpt-5.5"], kept.Select(model => model.Id));
    }

    [Fact]
    public void Narrow_ShouldKeepEverything_WhenTheRuleWouldKeepNothing()
    {
        // A key whose project reached only speech models: filtering to nothing sent the admin
        // looking at the wrong thing.
        IReadOnlyList<ModelInfo> offered =
            [Model("tts-1"), Model("tts-1-hd"), Model("gpt-4o-mini-tts")];

        var kept = ModelCatalogue.Narrow(offered, model => !model.Id.Contains("tts"));

        Assert.Equal(offered, kept);
    }

    [Fact]
    public void Narrow_ShouldLeaveAnEmptyCatalogueEmpty()
    {
        Assert.Empty(ModelCatalogue.Narrow([], _ => true));
    }

    [Fact]
    public void Newest_ShouldPutTheMostRecentlyPublishedFirst()
    {
        // Alphabetical buries what somebody came for: gpt-image-2.5 sorts above gpt-6.
        IReadOnlyList<ModelInfo> offered =
        [
            Dated("gpt-4o-mini", "2024-07-18"),
            Dated("gpt-image-2.5-flare", "2026-04-02"),
            Dated("gpt-6-astra", "2026-08-11")
        ];

        var ordered = ModelCatalogue.Newest(offered);

        Assert.Equal(
            ["gpt-6-astra", "gpt-image-2.5-flare", "gpt-4o-mini"],
            ordered.Select(model => model.Id));
    }

    [Fact]
    public void Newest_ShouldKeepNameOrder_ForAProviderThatDatesNothing()
    {
        // Google's listing carries no date; an order that looked meaningful would be made up.
        IReadOnlyList<ModelInfo> offered =
            [Model("gemini-3-pro"), Model("gemini-2.5-flash"), Model("gemma-4-31b-it")];

        var ordered = ModelCatalogue.Newest(offered);

        Assert.Equal(
            ["gemini-2.5-flash", "gemini-3-pro", "gemma-4-31b-it"],
            ordered.Select(model => model.Id));
    }

    [Fact]
    public void Newest_ShouldPutADatedModelAboveAnUndatedOne()
    {
        // Not a case any provider produces, but the rule should be stated rather than left to the
        // comparer.
        IReadOnlyList<ModelInfo> offered = [Model("aaa-no-date"), Dated("zzz-dated", "2020-01-01")];

        var ordered = ModelCatalogue.Newest(offered);

        Assert.Equal(["zzz-dated", "aaa-no-date"], ordered.Select(model => model.Id));
    }

    private static ModelInfo Dated(string id, string added) =>
        new(id, id, CanDraw: false, DateTimeOffset.Parse(added, CultureInfo.InvariantCulture));
}
