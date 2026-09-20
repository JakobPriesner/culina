using Application.Abstractions;
using Application.Assistance;

namespace Application.UnitTests.Assistance;

/// <summary>
/// That a picker built from a listing offers no two lines a person cannot tell
/// apart.
/// </summary>
public class ModelLabelsTests
{
    [Fact]
    public void Distinguish_ShouldSpellOutTheIds_WhenOneNameIsSharedBySeveralModels()
    {
        // Google's catalogue, in miniature: one family, three releases, one
        // friendly name across all of them.
        IReadOnlyList<ModelInfo> listed =
        [
            new("gemini-3-pro-image-preview", "Nano Banana Pro", CanDraw: true),
            new("gemini-3-pro-image-preview-11-2025", "Nano Banana Pro", CanDraw: true)
        ];

        // Act
        var distinguished = ModelLabels.Distinguish(listed);

        // Assert
        Assert.Equal(
            ["Nano Banana Pro (gemini-3-pro-image-preview)",
             "Nano Banana Pro (gemini-3-pro-image-preview-11-2025)"],
            distinguished.Select(model => model.Label));
    }

    [Fact]
    public void Distinguish_ShouldLeaveANameAlone_WhenNothingElseCarriesIt()
    {
        IReadOnlyList<ModelInfo> listed =
        [
            new("gemini-3-flash-preview", "Gemini 3 Flash", CanDraw: false),
            new("gemini-3-pro-image-preview", "Nano Banana Pro", CanDraw: true)
        ];

        // Act
        var distinguished = ModelLabels.Distinguish(listed);

        // Assert
        // The id is the noise the display name exists to spare everybody, so it
        // is added only where it settles something.
        Assert.Equal(
            ["Gemini 3 Flash", "Nano Banana Pro"],
            distinguished.Select(model => model.Label));
    }

    [Fact]
    public void Distinguish_ShouldNotWriteAnIdTwice_WhenItIsAlreadyTheName()
    {
        // OpenAI and Ollama have no display names, so the label is the id. A
        // repeated one would come out as "gpt-6-astra (gpt-6-astra)".
        IReadOnlyList<ModelInfo> listed =
        [
            new("gpt-6-astra", "gpt-6-astra", CanDraw: false),
            new("gpt-6-astra", "gpt-6-astra", CanDraw: false)
        ];

        // Act
        var distinguished = ModelLabels.Distinguish(listed);

        // Assert
        Assert.Equal(["gpt-6-astra", "gpt-6-astra"], distinguished.Select(model => model.Label));
    }

    [Fact]
    public void Distinguish_ShouldKeepTheOrderItWasGiven()
    {
        IReadOnlyList<ModelInfo> listed =
        [
            new("b", "Same", CanDraw: false),
            new("a", "Same", CanDraw: false)
        ];

        // Act
        var distinguished = ModelLabels.Distinguish(listed);

        // Assert
        // Sorting is the caller's; doing it again here would quietly undo it.
        Assert.Equal(["b", "a"], distinguished.Select(model => model.Id));
    }
}
