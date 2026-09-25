using Application.Abstractions;
using Application.Recipes.GetTagSuggestions;
using Domain.Shared;

namespace Application.UnitTests.Recipes;

/// <summary>
/// Which tags a recipe is offered: what it is, in the household's own words
/// where it has them, and nothing it already says.
/// </summary>
public class TagSuggestionTests
{
    private static readonly TagUsage[] PastaKitchen =
    [
        new("pasta", "pasta", 9),
        new("italienisch", "italienisch", 7)
    ];

    [Fact]
    public void Suggest_ShouldOfferWhatARecipeIs_AndNotWhatItsTagsAlreadySay()
    {
        // Act
        var offered = Names(GetTagSuggestionsQueryHandler.Suggest(
            new Suggesting(
                "Lasagne Bolognese",
                ["pasta", "italienisch"],
                ["Hackfleisch", "Tomaten", "Lasagneplatten", "Béchamel"],
                Language.De),
            PastaKitchen));

        // Assert
        // A Lasagne is a pasta bake: the tag somebody filtering for one would
        // want it to carry.
        Assert.Contains("Auflauf", offered);
        Assert.DoesNotContain("Italienisch", offered, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("pasta", offered, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Suggest_ShouldOfferTheHouseholdsOwnTag_WhereItHasOneForTheThing()
    {
        // Act
        var offered = GetTagSuggestionsQueryHandler.Suggest(
            new Suggesting("Pizza Margherita", [], ["Mehl", "Tomaten", "Mozzarella"], Language.De),
            PastaKitchen);

        // Assert
        // Its "italienisch", lower-case and all, rather than the lexicon's
        // "Italienisch" beside it: one kitchen, one word for it.
        var first = offered[0];
        Assert.Equal("italienisch", first.Name);
        Assert.Equal("italienisch", first.Slug);
        Assert.DoesNotContain(offered, one => one.Name == "Italienisch");
    }

    [Fact]
    public void Suggest_ShouldNeverOfferAnIngredient_OrSomethingTrueOfHalfOfEverything()
    {
        // Act
        var offered = Names(GetTagSuggestionsQueryHandler.Suggest(
            new Suggesting("Hähnchen-Curry", [], ["Hähnchenschenkel", "Kokosmilch", "Currypaste", "Reis"], Language.De),
            []));

        // Assert
        Assert.Contains("Curry", offered);
        Assert.Contains("Asiatisch", offered);
        Assert.DoesNotContain("Hähnchen", offered);
        Assert.DoesNotContain("Reis", offered);
        Assert.DoesNotContain("warm", offered);
    }

    [Fact]
    public void Suggest_ShouldWordANewTagInTheRecipesLanguage_AndOfferAtMostFive()
    {
        // Act
        var offered = GetTagSuggestionsQueryHandler.Suggest(
            new Suggesting(
                "Vegetarian Lasagne Casserole with Pesto for Dinner",
                [],
                ["lasagne sheets", "courgette", "pesto"],
                Language.En),
            []);

        // Assert
        Assert.InRange(offered.Count, 1, 5);
        Assert.Contains("vegetarian", Names(offered));
        Assert.All(offered, one => Assert.Null(one.Slug));
    }

    private static List<string> Names(IReadOnlyList<Contracts.Recipes.GetTagSuggestions.TagSuggestion> offered) =>
        [.. offered.Select(one => one.Name)];
}
