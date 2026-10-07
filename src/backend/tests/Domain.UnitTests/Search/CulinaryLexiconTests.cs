using Domain.Search;

namespace Domain.UnitTests.Search;

/// <summary>
/// The lexicon relates words; these hold it to relating the right ones, and never ambiguously.
/// </summary>
public class CulinaryLexiconTests
{
    [Fact]
    public void EnglishLabel_ShouldBeCapitalised_ForEveryCuisine()
    {
        // "Also: italian" reads as a typo to an English reader: labels are written as a person
        // writes them; English ones were lower-case matching forms until they doubled as labels.
        var cuisines = CulinaryLexicon.All.Where(concept => concept.Kind == ConceptKind.Cuisine);

        var lowercase = cuisines.Select(concept => concept.En[0]).Where(label => !char.IsUpper(label[0])).ToList();

        Assert.Empty(lowercase);
    }

    [Fact]
    public void Lexicon_ShouldHaveNoSurfaceFormInTwoConcepts()
    {
        // Compared folded, both ways, as text meets them: forms differing only by an umlaut are one
        // form to the matcher.
        var owners = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var concept in CulinaryLexicon.All)
        {
            foreach (var form in concept.De.Concat(concept.En))
            {
                foreach (var folded in new[] { SearchText.FoldAe(form), SearchText.FoldA(form) })
                {
                    if (!owners.TryGetValue(folded, out var keys))
                    {
                        owners[folded] = keys = [];
                    }

                    keys.Add(concept.Key);
                }
            }
        }

        var ambiguous = owners
            .Where(pair => pair.Value.Count > 1)
            .Select(pair => $"{pair.Key}: {string.Join(", ", pair.Value)}")
            .ToList();

        Assert.Empty(ambiguous);
    }

    [Fact]
    public void Lexicon_ShouldGiveEveryConceptAUniqueKey_AndAWordInBothLanguages()
    {
        var duplicates = CulinaryLexicon.All.GroupBy(concept => concept.Key).Where(group => group.Count() > 1);
        var unnamed = CulinaryLexicon.All.Where(concept => concept.De.Count == 0 || concept.En.Count == 0);

        Assert.Empty(duplicates);
        Assert.Empty(unnamed);
    }

    [Fact]
    public void Lexicon_ShouldOnlyNameParentsThatExist()
    {
        var orphans = CulinaryLexicon.All
            .SelectMany(concept => concept.Parents.Select(parent => (concept.Key, parent)))
            .Where(edge => CulinaryLexicon.Find(edge.parent) is null)
            .Select(edge => $"{edge.Key} → {edge.parent}");

        Assert.Empty(orphans);
    }

    [Fact]
    public void Lexicon_ShouldHaveNoParentCycle()
    {
        // A concept that is its own ancestor would make everything under it
        // "a kind of" everything else in the loop.
        var cyclic = CulinaryLexicon.All
            .Where(concept => concept.Parents.Any(parent =>
                CulinaryLexicon.Lineage(parent).Contains(concept.Key, StringComparer.Ordinal)))
            .Select(concept => concept.Key);

        Assert.Empty(cyclic);
    }

    [Theory]
    [InlineData("Hähnchen", "chicken")]
    [InlineData("Haehnchen", "chicken")]
    [InlineData("Hahnchen", "chicken")]
    [InlineData("chicken", "chicken")]
    [InlineData("Huhn", "chicken")]
    [InlineData("Geflügel", "poultry")]
    [InlineData("Nachtisch", "dessert")]
    [InlineData("Waffeln", "waffle")]
    [InlineData("vegetarisch", "vegetarian")]
    [InlineData("Vegetarische Küche", "vegetarian")]
    [InlineData("ohne Fleisch", "vegetarian")]
    [InlineData("italienische", "italian")]
    [InlineData("italian", "italian")]
    [InlineData("brussels sprouts", "brussels_sprouts")]
    [InlineData("sweet potatoes", "sweet_potato")]
    [InlineData("Gockel", "chicken")]
    [InlineData("Winteressen", "winter")]
    [InlineData("Sommergericht", "summer")]
    [InlineData("comfort food", "comfort")]
    public void Recognise_ShouldFindTheConceptAQueryWordNames(string query, string expected)
    {
        var found = CulinaryLexicon.Recognise(query);

        Assert.Contains(expected, found);
    }

    [Theory]
    // A compound somebody types is the name of what they want, not a list of
    // its parts: splitting "Ofengemüse" would answer it with half the library.
    [InlineData("Ofengemüse")]
    [InlineData("Kartoffelgratin")]
    [InlineData("Tomatensuppe")]
    public void Recognise_ShouldNotSplitAQueryWord_IntoItsParts(string query)
    {
        Assert.Empty(CulinaryLexicon.Recognise(query));
    }

    [Fact]
    public void Recognise_ShouldNameWhatTheQueryNames_NotWhatThatIsAKindOf()
    {
        var found = CulinaryLexicon.Recognise("Hähnchen");

        Assert.DoesNotContain("poultry", found);
        Assert.DoesNotContain("meat", found);
    }

    [Theory]
    [InlineData("Basmatireis", "rice")]
    [InlineData("Rinderhack", "mince")]
    [InlineData("Rinderhack", "beef")]
    [InlineData("Kirschtomaten", "tomato")]
    [InlineData("Hähnchenbrustfilet", "chicken")]
    [InlineData("Schweinebraten", "pork")]
    [InlineData("Schweinebraten", "roast")]
    public void Describe_ShouldFindTheConceptsInsideACompound(string ingredient, string expected)
    {
        Assert.Contains(expected, CulinaryLexicon.Describe(ingredient, [], []));
    }

    [Theory]
    // A compound that means something its parts do not wins over its parts.
    [InlineData("Kokosmilch", "milk")]
    [InlineData("Zwiebelkuchen", "cake")]
    [InlineData("Süßkartoffel", "potato")]
    [InlineData("Tortellini", "cake")]
    [InlineData("Palatschinken", "ham")]
    [InlineData("Mangold", "mango")]
    // A short form is not found in the middle of a word.
    [InlineData("Studentenfutter", "duck")]
    [InlineData("Eisbein", "ice_cream")]
    // "ohne Fleisch" is a diet, not a request for meat.
    [InlineData("ohne Fleisch", "meat")]
    // A condiment, not a sauce anybody cooks: a Pad Thai is not a sauce dish.
    [InlineData("Fischsauce", "sauce")]
    public void Describe_ShouldNotFindWhatAWordMerelyContains(string text, string unexpected)
    {
        Assert.DoesNotContain(unexpected, CulinaryLexicon.Describe(text, [], []));
    }

    [Fact]
    public void Recognise_ShouldFindNothing_InTextThatIsNotAboutFood()
    {
        Assert.Empty(CulinaryLexicon.Recognise("qwertzuiop"));
        Assert.Empty(CulinaryLexicon.Recognise(string.Empty));
        Assert.Empty(CulinaryLexicon.Recognise("%%%"));
    }

    [Fact]
    public void Describe_ShouldIndexARecipe_UnderWhatItIsAKindOf()
    {
        var concepts = CulinaryLexicon.Describe(
            "Hähnchenbrustfilet mit Reis",
            ["schnell"],
            ["Hähnchenbrust", "Reis", "Zitrone"]);

        Assert.Contains("chicken", concepts);
        Assert.Contains("poultry", concepts);
        Assert.Contains("meat", concepts);
        Assert.Contains("rice", concepts);
        Assert.Contains("lemon", concepts);
        Assert.Contains("quick", concepts);
    }

    [Fact]
    public void Describe_ShouldMakeWaffles_ADessert()
    {
        // The case a household reported: "Nachtisch" should find Waffeln.
        var concepts = CulinaryLexicon.Describe("Waffeln", [], ["Mehl", "Eier", "Milch", "Zucker"]);

        Assert.Contains("dessert", concepts);
        Assert.Contains("sweet", concepts);
        Assert.Contains("egg", concepts);
    }

    [Fact]
    public void Describe_ShouldTakeADiet_FromTheTitleAndTags_NeverFromAnIngredient()
    {
        var fromIngredient = CulinaryLexicon.Describe("Kartoffelsuppe", [], ["pflanzliche Sahne"]);
        var fromTag = CulinaryLexicon.Describe("Kartoffelsuppe", ["vegan"], []);
        var fromTitle = CulinaryLexicon.Describe("Vegane Lasagne", [], []);

        Assert.DoesNotContain("vegan", fromIngredient);
        Assert.Contains("vegan", fromTag);
        Assert.Contains("vegetarian", fromTag);
        Assert.Contains("vegan", fromTitle);
    }

    [Theory]
    [InlineData("Hackfleisch")]
    [InlineData("Speck")]
    [InlineData("Lachs")]
    [InlineData("Garnelen")]
    [InlineData("Hühnerbrühe")]
    [InlineData("Salami")]
    [InlineData("Putenbrust")]
    [InlineData("Thunfisch")]
    [InlineData("Fischsauce")]
    [InlineData("Austernsauce")]
    public void Describe_ShouldKnowWhatIsAnAnimal(string ingredient)
    {
        // What a vegetarian search excludes on: these must reach a family, or the exclusion lets
        // them through.
        var concepts = CulinaryLexicon.Describe("Gericht", [], [ingredient]);

        Assert.True(
            concepts.Contains("meat") || concepts.Contains("fish") || concepts.Contains("seafood"),
            $"{ingredient}: {string.Join(", ", concepts)}");
    }

    [Theory]
    // A dish is answered by the dish it is a kind of.
    [InlineData("goulash", new[] { "goulash", "stew" })]
    [InlineData("tomato_sauce", new[] { "tomato_sauce", "sauce" })]
    // One level only: a Bolognese by a pasta sauce, not by every sauce.
    [InlineData("bolognese", new[] { "bolognese", "pasta_sauce" })]
    // Not by what it is made from, nor by its cuisine.
    [InlineData("risotto", new[] { "risotto" })]
    // And an ingredient only by itself: Tomaten do not want every vegetable.
    [InlineData("tomato", new[] { "tomato" })]
    public void AnsweredBy_ShouldLetADishStandInForTheDishItIsAKindOf(string key, string[] expected)
    {
        Assert.Equal(expected, CulinaryLexicon.AnsweredBy(key));
    }

    [Fact]
    public void MealRules_ShouldTellWhatADinnerLooksLike_AndWhatItIsNot()
    {
        var like = MealRules.LookLike("dinner");
        var unlike = MealRules.Unlike("dinner");

        Assert.Contains("lunch", like);
        Assert.Contains("warm", like);
        Assert.Contains("breakfast", unlike);
        Assert.Contains("dessert", unlike);
        Assert.DoesNotContain("lunch", unlike);
        Assert.DoesNotContain("dinner", unlike);
    }

    [Theory]
    // Nobody tags a recipe "comfort food": a character is found through the
    // dishes that have it.
    [InlineData("Rindereintopf", "comfort")]
    [InlineData("Rindereintopf", "winter")]
    [InlineData("Nudelauflauf", "comfort")]
    [InlineData("Kartoffelpüree", "comfort")]
    [InlineData("Gurkensalat", "summer")]
    public void Describe_ShouldGiveADish_TheCharacterItHas(string title, string expected)
    {
        Assert.Contains(expected, CulinaryLexicon.Describe(title, [], []));
    }

    [Fact]
    public void Describe_ShouldReadANegativeAnswer_AsNoDiet()
    {
        // The answer "Nein" to "Ist das vegetarisch?" is a tag, and a tag that
        // mentions a diet must not be read as keeping it.
        var concepts = CulinaryLexicon.Describe("Kichererbsen-Eintopf", ["nicht vegetarisch"], []);

        Assert.Contains("not_vegetarian", concepts);
        Assert.DoesNotContain("vegetarian", concepts);
        Assert.Contains("not_vegetarian", DietRules.RefutedBy("vegetarian")!);
        Assert.Contains("not_vegetarian", DietRules.RefutedBy("vegan")!);
    }
}
