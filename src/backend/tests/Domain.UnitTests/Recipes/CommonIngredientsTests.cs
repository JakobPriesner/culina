using Domain.Recipes;
using Domain.Shared;
using Domain.Shopping;

namespace Domain.UnitTests.Recipes;

/// <summary>
/// The seeded list, which is a floor and not a vocabulary.
/// </summary>
/// <remarks>
/// It exists so an empty kitchen is offered something. What it must not do is
/// offer the wrong thing, offer nine of them, or offer a word in the language
/// nobody is writing in.
/// </remarks>
public class CommonIngredientsTests
{
    [Fact]
    public void Matching_ShouldNameThemInTheLanguageThatWasAsked()
    {
        // Arrange & Act
        var german = CommonIngredients.Matching("zwiebel", Language.De, 5);
        var english = CommonIngredients.Matching("onion", Language.En, 5);

        // Assert
        Assert.Contains(german, entry => entry.De == "Zwiebel");
        Assert.Contains(english, entry => entry.En == "Onion");
    }

    [Fact]
    public void Matching_ShouldNotOfferAGermanNameToSomebodyWritingInEnglish()
    {
        // Arrange & Act
        var found = CommonIngredients.Matching("zwiebel", Language.En, 5);

        // Assert
        // The list is bilingual; a suggestion is not. Offering "Zwiebel" into
        // an English recipe puts a word in it that nothing else will match.
        Assert.Empty(found);
    }

    [Fact]
    public void Matching_ShouldPreferANameThatStartsWithWhatWasTyped()
    {
        // Arrange & Act
        var found = CommonIngredients.Matching("oil", Language.En, 5);

        // Assert
        // Somebody typing "oil" means the oil, not the something-oil that
        // happens to contain those letters later on.
        Assert.StartsWith("Olive oil", found[0].En, StringComparison.Ordinal);
    }

    [Fact]
    public void Matching_ShouldIgnoreHowItWasAccentedOrCapitalised()
    {
        // Arrange & Act
        var typedInAHurry = CommonIngredients.Matching("kuerbis", Language.De, 5);
        var typedProperly = CommonIngredients.Matching("Kürbis", Language.De, 5);

        // Assert
        // The same person writes "Kürbis" at a keyboard and "kuerbis" on a
        // phone, and both have to find the pumpkin.
        Assert.Equal(typedProperly, typedInAHurry);
    }

    [Fact]
    public void Matching_ShouldStopAtTheLimit()
    {
        // Arrange & Act
        var found = CommonIngredients.Matching(query: null, Language.En, 4);

        // Assert
        // A list you scroll is a list you stop reading.
        Assert.Equal(4, found.Count);
    }

    [Fact]
    public void EverySeededName_ShouldBeUniqueWithinItsLanguage()
    {
        // Arrange & Act
        var german = CommonIngredients.All.Select(entry => ItemName.Fold(entry.De)).ToList();
        var english = CommonIngredients.All.Select(entry => ItemName.Fold(entry.En)).ToList();

        // Assert
        // Two entries with one name are one suggestion shown twice, and the
        // second is the one that can never be chosen.
        Assert.Equal(german.Count, german.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(english.Count, english.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void EverySeededName_ShouldBeAnIngredientNameTheDomainAccepts()
    {
        // Arrange & Act
        var rejected = CommonIngredients.All
            .SelectMany(entry => new[] { entry.De, entry.En })
            .Where(name => RecipeIngredient
                .Create(id: null, 0, Quantity.Unmeasured, name, note: null)
                .Match(_ => false, _ => true))
            .ToList();

        // Assert
        // Suggesting something that cannot be saved is worse than suggesting
        // nothing at all.
        Assert.Empty(rejected);
    }

    [Fact]
    public void EverySeededIngredient_ShouldBeShelvedSomewhereReal()
    {
        // Arrange & Act
        var unplaced = CommonIngredients.All
            .Where(entry => entry.Section == ShoppingSection.Other)
            .ToList();

        // Assert
        // The seed exists partly to know where a thing lives. An entry filed
        // under "other" knows nothing the guesser did not already guess.
        Assert.Empty(unplaced);
    }
}
