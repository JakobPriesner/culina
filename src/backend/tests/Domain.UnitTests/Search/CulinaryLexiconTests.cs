using Domain.Search;

namespace Domain.UnitTests.Search;

/// <summary>
/// The lexicon relates words; these hold it to relating the right ones, and to
/// never being ambiguous about which.
/// </summary>
public class CulinaryLexiconTests
{
    [Fact]
    public void Lexicon_ShouldHaveNoSurfaceFormInTwoConcepts()
    {
        // Arrange
        // Compared folded, both ways, because that is how text meets them: two
        // forms that differ only by an umlaut are one form to the matcher.
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

        // Act
        var ambiguous = owners
            .Where(pair => pair.Value.Count > 1)
            .Select(pair => $"{pair.Key}: {string.Join(", ", pair.Value)}")
            .ToList();

        // Assert
        Assert.Empty(ambiguous);
    }

    [Fact]
    public void Lexicon_ShouldGiveEveryConceptAUniqueKey_AndAWordInBothLanguages()
    {
        // Act
        var duplicates = CulinaryLexicon.All.GroupBy(concept => concept.Key).Where(group => group.Count() > 1);
        var unnamed = CulinaryLexicon.All.Where(concept => concept.De.Count == 0 || concept.En.Count == 0);

        // Assert
        Assert.Empty(duplicates);
        Assert.Empty(unnamed);
    }

    [Fact]
    public void Lexicon_ShouldOnlyNameParentsThatExist()
    {
        // Act
        var orphans = CulinaryLexicon.All
            .SelectMany(concept => concept.Parents.Select(parent => (concept.Key, parent)))
            .Where(edge => CulinaryLexicon.Find(edge.parent) is null)
            .Select(edge => $"{edge.Key} → {edge.parent}");

        // Assert
        Assert.Empty(orphans);
    }

    [Fact]
    public void Lexicon_ShouldHaveNoParentCycle()
    {
        // Act
        // A concept that is its own ancestor would make everything under it
        // "a kind of" everything else in the loop.
        var cyclic = CulinaryLexicon.All
            .Where(concept => concept.Parents.Any(parent =>
                CulinaryLexicon.Lineage(parent).Contains(concept.Key, StringComparer.Ordinal)))
            .Select(concept => concept.Key);

        // Assert
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
    [InlineData("sweet potatoes", "sweet_potato")]
    [InlineData("Gockel", "rooster")]
    public void Recognise_ShouldFindTheConceptAQueryWordNames(string query, string expected)
    {
        // Act
        var found = CulinaryLexicon.Recognise(query);

        // Assert
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
        // Act & Assert
        Assert.Empty(CulinaryLexicon.Recognise(query));
    }

    [Fact]
    public void Recognise_ShouldNameWhatTheQueryNames_NotWhatThatIsAKindOf()
    {
        // Act
        var found = CulinaryLexicon.Recognise("Hähnchen");

        // Assert
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
        // Act & Assert
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
    public void Describe_ShouldNotFindWhatAWordMerelyContains(string text, string unexpected)
    {
        // Act & Assert
        Assert.DoesNotContain(unexpected, CulinaryLexicon.Describe(text, [], []));
    }

    [Fact]
    public void Recognise_ShouldFindNothing_InTextThatIsNotAboutFood()
    {
        // Act & Assert
        Assert.Empty(CulinaryLexicon.Recognise("qwertzuiop"));
        Assert.Empty(CulinaryLexicon.Recognise(string.Empty));
        Assert.Empty(CulinaryLexicon.Recognise("%%%"));
    }

    [Fact]
    public void Describe_ShouldIndexARecipe_UnderWhatItIsAKindOf()
    {
        // Act
        var concepts = CulinaryLexicon.Describe(
            "Hähnchenbrustfilet mit Reis",
            ["schnell"],
            ["Hähnchenbrust", "Reis", "Zitrone"]);

        // Assert
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
        // Act
        // The case a household reported: "Nachtisch" should find Waffeln.
        var concepts = CulinaryLexicon.Describe("Waffeln", [], ["Mehl", "Eier", "Milch", "Zucker"]);

        // Assert
        Assert.Contains("dessert", concepts);
        Assert.Contains("sweet", concepts);
        Assert.Contains("egg", concepts);
    }

    [Fact]
    public void Describe_ShouldTakeADiet_FromTheTitleAndTags_NeverFromAnIngredient()
    {
        // Act
        var fromIngredient = CulinaryLexicon.Describe("Kartoffelsuppe", [], ["pflanzliche Sahne"]);
        var fromTag = CulinaryLexicon.Describe("Kartoffelsuppe", ["vegan"], []);
        var fromTitle = CulinaryLexicon.Describe("Vegane Lasagne", [], []);

        // Assert
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
    public void Describe_ShouldKnowWhatIsAnAnimal(string ingredient)
    {
        // Act
        // What a vegetarian search will exclude on: these must all reach one
        // of the families, or the exclusion quietly lets them through.
        var concepts = CulinaryLexicon.Describe("Gericht", [], [ingredient]);

        // Assert
        Assert.True(
            concepts.Contains("meat") || concepts.Contains("fish") || concepts.Contains("seafood"),
            $"{ingredient}: {string.Join(", ", concepts)}");
    }
}
