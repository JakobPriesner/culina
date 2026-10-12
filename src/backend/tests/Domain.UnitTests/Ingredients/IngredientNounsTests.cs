using System.Text.RegularExpressions;
using Domain.Ingredients;
using Domain.Shared;

namespace Domain.UnitTests.Ingredients;

public class IngredientNounsTests
{
    [Theory]
    [InlineData("Zwiebel", 2, "Zwiebeln")]
    [InlineData("Zwiebeln", 1, "Zwiebel")]
    [InlineData("Zwiebeln", 0.5, "Zwiebel")]
    [InlineData("Zwiebel", 1.5, "Zwiebeln")]
    [InlineData("Apfel", 3, "Äpfel")]
    [InlineData("Ei", 2, "Eier")]
    [InlineData("Eier", 1, "Ei")]
    [InlineData("Knoblauchzehe", 2, "Knoblauchzehen")]
    [InlineData("Bratäpfel", 1, "Bratapfel")]
    [InlineData("rote Zwiebel", 2, "rote Zwiebeln")]
    [InlineData("großer Apfel", 2, "große Äpfel")]
    [InlineData("große Äpfel", 1, "großer Apfel")]
    [InlineData("kleine Eier", 1, "kleines Ei")]
    [InlineData("Zwiebel, rot", 2, "Zwiebeln, rot")]
    [InlineData("Hähnchenbrustfilet", 2, "Hähnchenbrustfilets")]
    public void ForAmount_ShouldWriteTheFoodInTheNumberTheCountAsksFor(string name, double amount, string expected)
    {
        Assert.Equal(expected, IngredientNouns.ForAmount(name, (decimal)amount, hasUnit: false, Language.De));
    }

    [Theory]
    [InlineData("onion", 2, "onions")]
    [InlineData("Eggs", 1, "Egg")]
    [InlineData("garlic clove", 3, "garlic cloves")]
    public void ForAmount_ShouldKnowEnglishToo(string name, double amount, string expected)
    {
        Assert.Equal(expected, IngredientNouns.ForAmount(name, (decimal)amount, hasUnit: false, Language.En));
    }

    [Theory]
    // After a unit the recipe wrote what it meant: "2 Scheiben Brot", "500 g Tomaten".
    [InlineData("Brot", 2, true)]
    [InlineData("Tomate", 500, true)]
    // Mass nouns and unknown words have one form.
    [InlineData("Spargel", 2, false)]
    [InlineData("Kokosmilch", 2, false)]
    [InlineData("Zucchini", 3, false)]
    // "Ei" ends no compound: Rührei is not eggs.
    [InlineData("Rührei", 2, false)]
    public void ForAmount_ShouldLeaveTheNameAsWritten_WhenItHasNoOtherNumber(string name, double amount, bool hasUnit)
    {
        Assert.Equal(name, IngredientNouns.ForAmount(name, (decimal)amount, hasUnit, Language.De));
    }

    [Fact]
    public void ForAmount_ShouldLeaveTheName_WhenThereIsNoAmount()
    {
        Assert.Equal("Zwiebel", IngredientNouns.ForAmount("Zwiebel", null, hasUnit: false, Language.De));
    }

    [Fact]
    public void ForAmount_ShouldReadTheRecipesLanguage()
    {
        // "Orange" is a word in both; an English recipe's two oranges are not "Orangen".
        Assert.Equal("oranges", IngredientNouns.ForAmount("orange", 2, hasUnit: false, Language.En));
        Assert.Equal("Orangen", IngredientNouns.ForAmount("Orange", 2, hasUnit: false, Language.De));
    }

    [Theory]
    [InlineData("apfel", "aepfel")]
    [InlineData("ei", "eier")]
    [InlineData("knoblauchzehe", "knoblauchzehen")]
    [InlineData("walnuss", "walnuesse")]
    public void AreOneNoun_ShouldPairTheTwoNumbers(string one, string other)
    {
        Assert.True(IngredientNouns.AreOneNoun(one, other));
        Assert.True(IngredientNouns.AreOneNoun(other, one));
    }

    [Theory]
    [InlineData("ei", "eis")]
    [InlineData("apfel", "birnen")]
    [InlineData("ruehrei", "ruehreier")]
    public void AreOneNoun_ShouldNotPairOtherWords(string one, string other)
    {
        Assert.False(IngredientNouns.AreOneNoun(one, other));
    }

    [Fact]
    public void TheFrontendsCopy_ShouldListTheSameNouns()
    {
        // Arrange: the recipe page scales lines in the browser with its own copy of the table.
        var copy = File.ReadAllText(FrontendFile("src/lib/features/recipes/nounForms.ts"));
        var genders = new Dictionary<string, IngredientNouns.Gender>
        {
            ["m"] = IngredientNouns.Gender.Masculine,
            ["f"] = IngredientNouns.Gender.Feminine,
            ["n"] = IngredientNouns.Gender.Neuter
        };

        // Act
        var listed = Regex.Matches(copy, @"\['([^']+)', '([^']+)'(?:, '([mfn])')?\]")
            .Select(match => new IngredientNouns.Noun(
                match.Groups[1].Value,
                match.Groups[2].Value,
                match.Groups[3].Success ? genders[match.Groups[3].Value] : null))
            .ToList();

        // Assert
        Assert.Equal([.. IngredientNouns.German, .. IngredientNouns.English], listed);
    }

    private static string FrontendFile(string path)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "src", "frontend", path);

            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"No src/frontend/{path} above the test binaries.");
    }

    [Fact]
    public void EveryForm_ShouldBeListedOnce()
    {
        var german = IngredientNouns.German.SelectMany(noun => new[] { noun.One, noun.Many }).ToList();
        var english = IngredientNouns.English.SelectMany(noun => new[] { noun.One, noun.Many }).ToList();

        Assert.Equal(german.Count, german.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(english.Count, english.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void ForAmount_ShouldUseTheHouseholdsOwnNouns()
    {
        // Arrange
        var own = IngredientNouns.Own("Maultasche", "Maultaschen").Match(noun => noun, _ => null!);

        // Act
        var doubled = IngredientNouns.ForAmount("Maultasche", 2, hasUnit: false, Language.De, [own]);
        var single = IngredientNouns.ForAmount("Spinatmaultaschen", 1, hasUnit: false, Language.De, [own]);
        var without = IngredientNouns.ForAmount("Maultasche", 2, hasUnit: false, Language.De);

        // Assert
        Assert.Equal("Maultaschen", doubled);
        Assert.Equal("Spinatmaultasche", single);
        Assert.Equal("Maultasche", without);
    }

    [Theory]
    [InlineData("Maultasche", "Maultasche")]
    [InlineData("Maultasche", "maultasche")]
    [InlineData("Maul tasche", "Maultaschen")]
    [InlineData("", "Maultaschen")]
    [InlineData("Maultasche", "2 Maultaschen")]
    public void Own_ShouldRefuse_WhatIsNotTwoDifferentWords(string one, string many)
    {
        Assert.Equal(IngredientErrors.InvalidNoun.Code, IngredientNouns.Own(one, many).Match(_ => "valid", error => error.Code));
    }

    [Fact]
    public void Own_ShouldTrimBothForms()
    {
        var noun = IngredientNouns.Own(" Schupfnudel ", "Schupfnudeln ").Match(noun => noun, _ => null!);

        Assert.Equal(new IngredientNouns.Noun("Schupfnudel", "Schupfnudeln", null), noun);
    }
}
