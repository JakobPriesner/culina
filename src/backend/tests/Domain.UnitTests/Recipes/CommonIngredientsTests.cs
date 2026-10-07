using Domain.Recipes;
using Domain.Shared;
using Domain.Shopping;

namespace Domain.UnitTests.Recipes;

/// <summary>The seeded list is a floor, not a vocabulary: it must not offer wrong or too many things, or the wrong language.</summary>
public class CommonIngredientsTests
{
    [Fact]
    public void Matching_ShouldNameThemInTheLanguageThatWasAsked()
    {
        var german = CommonIngredients.Matching("zwiebel", Language.De, 5);
        var english = CommonIngredients.Matching("onion", Language.En, 5);

        Assert.Contains(german, entry => entry.De == "Zwiebel");
        Assert.Contains(english, entry => entry.En == "Onion");
    }

    [Fact]
    public void Matching_ShouldNotOfferAGermanNameToSomebodyWritingInEnglish()
    {
        var found = CommonIngredients.Matching("zwiebel", Language.En, 5);

        // The list is bilingual; suggesting "Zwiebel" into an English recipe adds a word nothing else matches.
        Assert.Empty(found);
    }

    [Fact]
    public void Matching_ShouldPreferANameThatStartsWithWhatWasTyped()
    {
        var found = CommonIngredients.Matching("oil", Language.En, 5);

        Assert.StartsWith("Olive oil", found[0].En, StringComparison.Ordinal);
    }

    [Fact]
    public void Matching_ShouldIgnoreHowItWasAccentedOrCapitalised()
    {
        var typedInAHurry = CommonIngredients.Matching("kuerbis", Language.De, 5);
        var typedProperly = CommonIngredients.Matching("Kürbis", Language.De, 5);

        // "Kürbis" at a keyboard and "kuerbis" on a phone must both find the pumpkin.
        Assert.Equal(typedProperly, typedInAHurry);
    }

    [Fact]
    public void Matching_ShouldStopAtTheLimit()
    {
        var found = CommonIngredients.Matching(query: null, Language.En, 4);

        Assert.Equal(4, found.Count);
    }

    [Fact]
    public void EverySeededName_ShouldBeUniqueWithinItsLanguage()
    {
        var german = CommonIngredients.All.Select(entry => ItemName.Fold(entry.De)).ToList();
        var english = CommonIngredients.All.Select(entry => ItemName.Fold(entry.En)).ToList();

        // A duplicate name is one suggestion shown twice, the second never choosable.
        Assert.Equal(german.Count, german.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(english.Count, english.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void EverySeededName_ShouldBeAnIngredientNameTheDomainAccepts()
    {
        var rejected = CommonIngredients.All
            .SelectMany(entry => new[] { entry.De, entry.En })
            .Where(name => RecipeIngredient
                .Create(id: null, 0, Quantity.Unmeasured, name, note: null)
                .Match(_ => false, _ => true))
            .ToList();

        Assert.Empty(rejected);
    }

    [Fact]
    public void EverySeededIngredient_ShouldBeShelvedSomewhereReal()
    {
        var unplaced = CommonIngredients.All
            .Where(entry => entry.Section == ShoppingSection.Other)
            .ToList();

        // An entry filed under "other" tells the guesser nothing.
        Assert.Empty(unplaced);
    }
}
