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
}
