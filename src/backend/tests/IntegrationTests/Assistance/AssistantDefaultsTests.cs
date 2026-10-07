using Domain.Assistance;
using Infrastructure.Assistance;

namespace IntegrationTests.Assistance;

/// <summary>What an administrator does not have to type. Here because <c>AssistantDefaults</c> is internal to Infrastructure.</summary>
public class AssistantDefaultsTests
{
    [Fact]
    public void Home_ShouldBeTheProvidersOwn_ForTheHostedOnes()
    {
        // Connecting should be choosing a provider and pasting a key, not knowing Google's address.
        Assert.Equal("https://generativelanguage.googleapis.com", AssistantDefaults.Home(AssistantKind.Gemini));
        Assert.Equal("https://api.openai.com", AssistantDefaults.Home(AssistantKind.OpenAi));
    }

    [Fact]
    public void ComposeModel_ShouldBeNamed_ForEveryProvider()
    {
        // An empty model box means "whatever is current", which needs something current to mean.
        Assert.All(
            AssistantKind.All,
            kind => Assert.NotEmpty(AssistantDefaults.ComposeModel(kind)));
    }

    [Fact]
    public void DrawModel_ShouldBeEmpty_ForAProviderThatCannotDraw()
    {
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
