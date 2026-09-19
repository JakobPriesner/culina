using Domain.Assistance;
using Infrastructure.Assistance;

namespace IntegrationTests.Assistance;

/// <summary>
/// What an administrator does not have to type.
/// </summary>
/// <remarks>
/// In the integration project because <c>AssistantDefaults</c> is internal to
/// Infrastructure, which is the assembly this one can see. Nothing here needs a
/// database; it is here for visibility rather than for a fixture.
/// </remarks>
public class AssistantDefaultsTests
{
    [Fact]
    public void Home_ShouldBeTheProvidersOwn_ForTheHostedOnes()
    {
        // Assert
        // Connecting should be choosing a provider and pasting a key. Google's
        // address is not a deployment decision and asking for it made setting
        // this up look like more work than it is.
        Assert.Equal("https://generativelanguage.googleapis.com", AssistantDefaults.Home(AssistantKind.Gemini));
        Assert.Equal("https://api.openai.com", AssistantDefaults.Home(AssistantKind.OpenAi));
    }

    [Fact]
    public void ComposeModel_ShouldBeNamed_ForEveryProvider()
    {
        // Assert
        // An empty model box means "whatever is current", which only works if
        // there is something current to mean.
        Assert.All(
            AssistantKind.All,
            kind => Assert.NotEmpty(AssistantDefaults.ComposeModel(kind)));
    }

    [Fact]
    public void DrawModel_ShouldBeEmpty_ForAProviderThatCannotDraw()
    {
        // Assert
        // The honest answer rather than a name that would fail on use.
        Assert.Empty(AssistantDefaults.DrawModel(AssistantKind.Ollama));
        Assert.NotEmpty(AssistantDefaults.DrawModel(AssistantKind.Gemini));
        Assert.NotEmpty(AssistantDefaults.DrawModel(AssistantKind.OpenAi));
    }

    [Fact]
    public void DrawModel_ShouldBeNamed_ForEveryProviderThatCan()
    {
        Assert.All(
            AssistantKind.All.Where(kind => kind.CanDraw),
            kind => Assert.NotEmpty(AssistantDefaults.DrawModel(kind)));
    }

    [Theory]
    [InlineData("", "fallback")]
    [InlineData("chosen", "chosen")]
    public void Or_ShouldPreferWhatWasChosen_AndFallBackWhenNothingWas(
        string configured,
        string expected)
    {
        Assert.Equal(expected, configured.Or("fallback"));
    }
}
