using Domain.Assistance;

namespace Domain.UnitTests.Assistance;

/// <summary>What a provider name will and will not be read as.</summary>
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
        // Case and whitespace are what a settings row collects over the years
        // of being edited by hand; neither is a different provider.
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
        // Null rather than a default. A settings row naming a provider this
        // cannot talk to is a configuration mistake, and quietly picking one
        // would send somebody's key to a company they did not choose.
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
        // There is nobody to authenticate to, and no address that could be
        // right by default — which is the exact inverse of the hosted two.
        Assert.False(AssistantKind.Ollama.NeedsApiKey);
        Assert.True(AssistantKind.Ollama.NeedsAddress);
    }

    [Fact]
    public void Ollama_ShouldNotDraw_BecauseItServesLanguageAndVisionModels()
    {
        // Assert
        // It will read a photograph of a cookbook page quite happily. It does
        // not make pictures, and the settings screen has to know that before it
        // offers the switch rather than after the call fails.
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
        // The list the settings screen offers and the list this can read back
        // are the same list, which is the one way they can disagree.
        Assert.All(AssistantKind.All, kind => Assert.Equal(kind, AssistantKind.Parse(kind.Code)));
    }
}
