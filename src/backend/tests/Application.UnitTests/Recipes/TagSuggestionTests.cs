using Application.Abstractions;
using Application.Recipes.GetTagSuggestions;
using Domain.Shared;

namespace Application.UnitTests.Recipes;

/// <summary>Which tags a recipe is offered: what it is, in the household's words where it has them, nothing it already says.</summary>
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
        var offered = Names(GetTagSuggestionsQueryHandler.Suggest(
            new Suggesting(
                "Lasagne Bolognese",
                ["pasta", "italienisch"],
                ["Hackfleisch", "Tomaten", "Lasagneplatten", "Béchamel"],
                Language.De),
            PastaKitchen));

        // A Lasagne is a pasta bake: the tag somebody filtering for one would expect.
        Assert.Contains("Auflauf", offered);
        Assert.DoesNotContain("Italienisch", offered, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("pasta", offered, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Suggest_ShouldOfferTheHouseholdsOwnTag_WhereItHasOneForTheThing()
    {
        var offered = GetTagSuggestionsQueryHandler.Suggest(
            new Suggesting("Pizza Margherita", [], ["Mehl", "Tomaten", "Mozzarella"], Language.De),
            PastaKitchen);

        // The household's own "italienisch" wins over the lexicon's "Italienisch": one kitchen, one word.
        var first = offered[0];
        Assert.Equal("italienisch", first.Name);
        Assert.Equal("italienisch", first.Slug);
        Assert.DoesNotContain(offered, one => one.Name == "Italienisch");
    }

    [Fact]
    public void Suggest_ShouldNeverOfferAnIngredient_OrSomethingTrueOfHalfOfEverything()
    {
        var offered = Names(GetTagSuggestionsQueryHandler.Suggest(
            new Suggesting("Hähnchen-Curry", [], ["Hähnchenschenkel", "Kokosmilch", "Currypaste", "Reis"], Language.De),
            []));

        Assert.Contains("Curry", offered);
        Assert.Contains("Asiatisch", offered);
        Assert.DoesNotContain("Hähnchen", offered);
        Assert.DoesNotContain("Reis", offered);
        Assert.DoesNotContain("warm", offered);
    }

    [Fact]
    public void Suggest_ShouldWordANewTagInTheRecipesLanguage_AndOfferAtMostFive()
    {
        var offered = GetTagSuggestionsQueryHandler.Suggest(
            new Suggesting(
                "Vegetarian Lasagne Casserole with Pesto for Dinner",
                [],
                ["lasagne sheets", "courgette", "pesto"],
                Language.En),
            []);

        Assert.InRange(offered.Count, 1, 5);
        Assert.Contains("vegetarian", Names(offered));
        Assert.All(offered, one => Assert.Null(one.Slug));
    }

    private static List<string> Names(IReadOnlyList<Contracts.Recipes.GetTagSuggestions.TagSuggestion> offered) =>
        [.. offered.Select(one => one.Name)];
}
