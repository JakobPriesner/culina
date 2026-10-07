using Domain.Cookbooks;
using TestSupport;

namespace Domain.UnitTests.Cookbooks;

/// <summary>What a cookbook insists on, and what it deliberately does not.</summary>
public class CookbookTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_ShouldAcceptANameAndNothingElse()
    {
        var name = NameOf("Weihnachten");

        var created = Cookbook.Create(Guid.NewGuid(), name, description: null, Guid.NewGuid(), Now);

        var cookbook = created.ShouldBeSuccess();

        Assert.Equal("Weihnachten", cookbook.Name.Value);
        Assert.Null(cookbook.Description);
        Assert.Equal(1, cookbook.Version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Name_ShouldRefuseNothingToCallIt(string? value)
    {
        var name = CookbookName.Create(value);

        name.ShouldBeFailure(CookbookErrors.InvalidName);
    }

    [Fact]
    public void Name_ShouldRefuseOneTooLongForItsColumn()
    {
        var name = CookbookName.Create(new string('a', CookbookName.MaxLength + 1));

        name.ShouldBeFailure(CookbookErrors.InvalidName);
    }

    [Fact]
    public void Name_ShouldKeepWhatWasWrittenWithoutItsSurroundingSpace()
    {
        var name = CookbookName.Create("  Sonntagsbraten  ");

        Assert.Equal("Sonntagsbraten", name.ShouldBeSuccess().Value);
    }

    [Fact]
    public void Create_ShouldRefuseADescriptionLongerThanAShelfLabel()
    {
        var created = Cookbook.Create(
            Guid.NewGuid(),
            NameOf("Lang"),
            new string('a', Cookbook.MaxDescriptionLength + 1),
            Guid.NewGuid(),
            Now);

        created.ShouldBeFailure(CookbookErrors.InvalidDescription);
    }

    [Fact]
    public void Create_ShouldTreatAnEmptyDescriptionAsNone()
    {
        // Two ways to say the same thing is two things every reader must check for.
        var created = Cookbook.Create(Guid.NewGuid(), NameOf("Leer"), "   ", Guid.NewGuid(), Now);

        Assert.Null(created.ShouldBeSuccess().Description);
    }

    [Fact]
    public void Rename_ShouldRecordWhenItChanged()
    {
        var cookbook = Made();
        var later = Now.AddDays(1);

        var renamed = cookbook.Revise(NameOf("Anders"), "Jetzt mit Beschreibung", rules: null, later);

        renamed.ShouldBeSuccess();
        Assert.Equal("Anders", cookbook.Name.Value);
        Assert.Equal("Jetzt mit Beschreibung", cookbook.Description);
        Assert.Equal(later, cookbook.UpdatedAt);
    }

    [Fact]
    public void Rename_ShouldLeaveTheCookbookAloneWhenItFails()
    {
        var cookbook = Made();

        var renamed = cookbook.Revise(
            NameOf("Anders"),
            new string('a', Cookbook.MaxDescriptionLength + 1),
            rules: null,
            Now.AddDays(1));

        renamed.ShouldBeFailure(CookbookErrors.InvalidDescription);
        Assert.Equal("Weihnachten", cookbook.Name.Value);
        Assert.Equal(Now, cookbook.UpdatedAt);
    }

    [Fact]
    public void Touch_ShouldRecordThatWhatIsOnItChanged()
    {
        // Adding a recipe changes the cookbook as readers see it: the count and the cover pictures.
        var cookbook = Made();
        var later = Now.AddHours(3);

        cookbook.Touch(later);

        Assert.Equal(later, cookbook.UpdatedAt);
    }

    [Fact]
    public void CreateSmart_ShouldRefuseAShelfThatAsksForNothing()
    {
        // A shelf with no rules is every recipe you have, the screen it is reached from.
        var rules = RulesOf([], [], null);

        var created = Cookbook.CreateSmart(
            Guid.NewGuid(), NameOf("Alles"), null, rules, Guid.NewGuid(), Now);

        created.ShouldBeFailure(CookbookErrors.RulesRequired);
    }

    [Fact]
    public void CreateSmart_ShouldKeepWhatItAsksFor()
    {
        var created = Cookbook.CreateSmart(
            Guid.NewGuid(),
            NameOf("Hähnchen"),
            null,
            RulesOf(["hauptspeise"], ["Hähnchen"], 45),
            Guid.NewGuid(),
            Now);

        var cookbook = created.ShouldBeSuccess();

        Assert.Equal(CookbookKind.Smart, cookbook.Kind);
        Assert.Equal(["hauptspeise"], cookbook.Rules.Tags);
        Assert.Equal(["Hähnchen"], cookbook.Rules.Ingredients);
        Assert.Equal(45, cookbook.Rules.MaxMinutes);
    }

    [Fact]
    public void Rules_ShouldCountTheSameTermTwiceAsOneCondition()
    {
        // The same word typed twice is one question asked twice; "2 rules" would count the typing.
        var rules = CookbookRules.Create(["quick", "Quick", " quick "], [], null);

        Assert.Equal(["quick"], rules.ShouldBeSuccess().Tags);
    }

    [Fact]
    public void Rules_ShouldSkipANullTerm_WhenTheRequestCarriedOne()
    {
        // The JSON reader lets ["quick", null] through, so a null reaches here.
        IReadOnlyList<string> terms = ["quick", null!];

        var rules = CookbookRules.Create(terms, terms, null);

        Assert.Equal(["quick"], rules.ShouldBeSuccess().Tags);
        Assert.Equal(["quick"], rules.ShouldBeSuccess().Ingredients);
    }

    [Fact]
    public void Rules_ShouldRefuseATimeNoRecipeCouldTake()
    {
        var rules = CookbookRules.Create([], [], 0);

        rules.ShouldBeFailure(CookbookErrors.InvalidRule);
    }

    [Fact]
    public void Revise_ShouldRefuseToEmptyASmartCookbooksRules()
    {
        // Clearing the last rule would silently turn a self-filling shelf into one containing
        // everything.
        var cookbook = Smart();

        var revised = cookbook.Revise(NameOf("Immer noch"), null, RulesOf([], [], null), Now);

        revised.ShouldBeFailure(CookbookErrors.RulesRequired);
        Assert.Equal(["hauptspeise"], cookbook.Rules.Tags);
    }

    [Fact]
    public void Revise_ShouldRefuseToGiveAManualCookbookRules()
    {
        // A shelf's kind answers "why is this recipe here?"; one that changed its mind would give
        // the recipes already on it two answers.
        var cookbook = Made();

        var revised = cookbook.Revise(NameOf("Egal"), null, RulesOf(["schnell"], [], null), Now);

        revised.ShouldBeFailure(CookbookErrors.RulesDecideMembership);
        Assert.Equal(CookbookKind.Manual, cookbook.Kind);
    }

    [Fact]
    public void Revise_ShouldChangeWhatASmartCookbookAsksFor()
    {
        var cookbook = Smart();

        var revised = cookbook.Revise(
            NameOf("Schneller"), null, RulesOf(["hauptspeise"], [], 20), Now.AddDays(1));

        revised.ShouldBeSuccess();
        Assert.Equal(20, cookbook.Rules.MaxMinutes);
    }

    private static Cookbook Smart() =>
        Cookbook
            .CreateSmart(
                Guid.NewGuid(),
                NameOf("Hauptspeisen"),
                null,
                RulesOf(["hauptspeise"], [], null),
                Guid.NewGuid(),
                Now)
            .Match(cookbook => cookbook, error => throw new InvalidOperationException(error.Code));

    private static CookbookRules RulesOf(string[] tags, string[] ingredients, int? maxMinutes) =>
        CookbookRules
            .Create(tags, ingredients, maxMinutes)
            .Match(rules => rules, error => throw new InvalidOperationException(error.Code));

    private static Cookbook Made() =>
        Cookbook
            .Create(Guid.NewGuid(), NameOf("Weihnachten"), description: null, Guid.NewGuid(), Now)
            .Match(cookbook => cookbook, error => throw new InvalidOperationException(error.Code));

    private static CookbookName NameOf(string value) =>
        CookbookName.Create(value).Match(name => name, error => throw new InvalidOperationException(error.Code));
}
