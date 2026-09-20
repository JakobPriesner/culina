using Domain.Assistance;

namespace Domain.UnitTests.Assistance;

/// <summary>
/// Which models a writing job may be offered, and which a drawing job may.
/// </summary>
/// <remarks>
/// The two lists are each other's complement, so every case here is really two:
/// a name read wrongly does not merely go missing from one picker, it turns up
/// in the other. A drawing model offered for "improve a recipe" is a choice
/// that can only fail.
/// </remarks>
public class DrawingModelTests
{
    [Theory]
    [InlineData("gemini-3-pro-image-preview")]
    [InlineData("gemini-3.1-flash-image-preview")]
    [InlineData("imagen-4.0-generate-001")]
    [InlineData("gpt-image-1")]
    [InlineData("dall-e-3")]
    [InlineData("stable-diffusion-3.5")]
    public void Draws_ShouldRecogniseAModelThatMakesPictures(string id)
    {
        Assert.True(DrawingModel.Draws(id));
    }

    [Theory]
    [InlineData("gemini-3-flash")]
    [InlineData("gpt-6-astra")]
    [InlineData("llama4:70b")]
    [InlineData("qwen3-vl:8b")]
    public void Draws_ShouldLeaveWritingAndReadingModelsAlone(string id)
    {
        // Reading a photograph is not drawing one: a vision model takes a
        // picture in and gives text back, which is what "read a photograph"
        // needs and what the drawing job cannot use.
        Assert.False(DrawingModel.Draws(id));
    }

    [Fact]
    public void Draws_ShouldReadTheDisplayName_WhereTheIdSaysNothing()
    {
        // Google's image models are a fruit in the display name and something
        // else entirely in the id, and the display name is what an
        // administrator recognises.
        Assert.True(DrawingModel.Draws("gemini-3-pro-preview", "Nano Banana 2"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Draws_ShouldSayNo_ToANameThatIsNotThere(string? id)
    {
        Assert.False(DrawingModel.Draws(id));
    }
}
