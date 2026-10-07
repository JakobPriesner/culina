using Domain.Assistance;

namespace Domain.UnitTests.Assistance;

public class AssistantKindTests
{
    [Theory]
    [InlineData("gemini")]
    [InlineData("GEMINI")]
    [InlineData("  gemini  ")]
    public void Parse_ShouldReadGemini_HoweverItIsWritten(string code)
    {
        // Act
        var kind = AssistantKind.Parse(code);

        // Assert
        Assert.Equal(AssistantKind.Gemini, kind);
    }

    [Fact]
    public void Parse_ShouldReadOpenAi_AsOneWord()
    {
        Assert.Equal(AssistantKind.OpenAi, AssistantKind.Parse("openai"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("anthropic")]
    [InlineData("open-ai")]
    public void Parse_ShouldRefuse_WhatIsNotAProviderThisKnows(string? code)
    {
        // Act
        var kind = AssistantKind.Parse(code);

        // Assert
        // Null, not a default: guessing would send the key to a provider nobody chose.
        Assert.Null(kind);
    }

    [Fact]
    public void Parse_ShouldReadOllama_TheOneThatRunsOnYourOwnMachine()
    {
        Assert.Equal(AssistantKind.Ollama, AssistantKind.Parse("ollama"));
    }

    [Fact]
    public void Code_ShouldBeWhatIsStored_SoARowStaysReadable()
    {
        Assert.Equal("gemini", AssistantKind.Gemini.Code);
        Assert.Equal("openai", AssistantKind.OpenAi.Code);
        Assert.Equal("ollama", AssistantKind.Ollama.Code);
    }

    [Fact]
    public void Ollama_ShouldNeedAnAddressAndNoKey_BecauseItIsYourOwnMachine()
    {
        // Assert
        Assert.False(AssistantKind.Ollama.NeedsApiKey);
        Assert.True(AssistantKind.Ollama.NeedsAddress);
    }

    [Fact]
    public void Ollama_ShouldNotDraw_BecauseItServesLanguageAndVisionModels()
    {
        // Assert
        // The settings screen must know before offering the switch, not after the call fails.
        Assert.False(AssistantKind.Ollama.CanDraw);
    }

    [Fact]
    public void TheHostedProviders_ShouldNeedAKeyAndNoAddress_AndBeAbleToDraw()
    {
        foreach (var kind in new[] { AssistantKind.Gemini, AssistantKind.OpenAi })
        {
            Assert.True(kind.NeedsApiKey);
            Assert.False(kind.NeedsAddress);
            Assert.True(kind.CanDraw);
        }
    }

    [Fact]
    public void All_ShouldRoundTripThroughParse_SoNothingIsOfferedThatCannotBeRead()
    {
        // Assert
        Assert.All(AssistantKind.All, kind => Assert.Equal(kind, AssistantKind.Parse(kind.Code)));
    }
}
