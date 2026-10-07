using Application.Recipes.GetRelated;
using Domain.Shared;

namespace Application.UnitTests.Recipes;

/// <summary>How the reason beside a related recipe is worded.</summary>
public class RelatedReasonTests
{
    [Fact]
    public void Words_ShouldNameTheConceptItself_NotEverythingItIsAKindOf()
    {
        var words = GetRelatedRecipesQueryHandler.Words(["chicken", "poultry", "meat"], Language.De);

        // Two chicken recipes share chicken; "Hähnchen, Geflügel, Fleisch" would say one thing three times.
        Assert.Equal(["Hähnchen"], words);
    }

    [Fact]
    public void Words_ShouldKeepAFamily_WhenTheTwoShareItThroughDifferentMembers()
    {
        // Salmon and cod: nothing more specific in common than being fish.
        var words = GetRelatedRecipesQueryHandler.Words(["fish", "pasta"], Language.En);

        Assert.Equal(["fish", "pasta"], words);
    }

    [Fact]
    public void Words_ShouldSpeakTheLanguageOfTheRecipeBeingRead()
    {
        var german = GetRelatedRecipesQueryHandler.Words(["dessert"], Language.De);
        var english = GetRelatedRecipesQueryHandler.Words(["dessert"], Language.En);

        Assert.Equal(["Nachtisch"], german);
        Assert.Equal(["dessert"], english);
    }

    [Fact]
    public void Words_ShouldNameAtMostThree_TheMostTellingFirst()
    {
        var words = GetRelatedRecipesQueryHandler.Words(
            ["mince", "tomato", "onion", "garlic", "carrot"],
            Language.De);

        Assert.Equal(["Hackfleisch", "Tomate", "Zwiebel"], words);
    }
}
