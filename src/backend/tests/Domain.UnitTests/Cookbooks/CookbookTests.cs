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
        // Arrange
        var name = NameOf("Weihnachten");

        // Act
        var created = Cookbook.Create(Guid.NewGuid(), name, description: null, Guid.NewGuid(), Now);

        // Assert
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
        // Act
        var name = CookbookName.Create(value);

        // Assert
        name.ShouldBeFailure(CookbookErrors.InvalidName);
    }

    [Fact]
    public void Name_ShouldRefuseOneTooLongForItsColumn()
    {
        // Act
        var name = CookbookName.Create(new string('a', CookbookName.MaxLength + 1));

        // Assert
        name.ShouldBeFailure(CookbookErrors.InvalidName);
    }

    [Fact]
    public void Name_ShouldKeepWhatWasWrittenWithoutItsSurroundingSpace()
    {
        // Act
        var name = CookbookName.Create("  Sonntagsbraten  ");

        // Assert
        Assert.Equal("Sonntagsbraten", name.ShouldBeSuccess().Value);
    }

    [Fact]
    public void Create_ShouldRefuseADescriptionLongerThanAShelfLabel()
    {
        // Act
        var created = Cookbook.Create(
            Guid.NewGuid(),
            NameOf("Lang"),
            new string('a', Cookbook.MaxDescriptionLength + 1),
            Guid.NewGuid(),
            Now);

        // Assert
        created.ShouldBeFailure(CookbookErrors.InvalidDescription);
    }

    [Fact]
    public void Create_ShouldTreatAnEmptyDescriptionAsNone()
    {
        // Arrange
        // Two ways to say the same thing is two things every reader has to
        // check for.
        // Act
        var created = Cookbook.Create(Guid.NewGuid(), NameOf("Leer"), "   ", Guid.NewGuid(), Now);

        // Assert
        Assert.Null(created.ShouldBeSuccess().Description);
    }

    [Fact]
    public void Rename_ShouldRecordWhenItChanged()
    {
        // Arrange
        var cookbook = Made();
        var later = Now.AddDays(1);

        // Act
        var renamed = cookbook.Revise(NameOf("Anders"), "Jetzt mit Beschreibung", rules: null, later);

        // Assert
        renamed.ShouldBeSuccess();
        Assert.Equal("Anders", cookbook.Name.Value);
        Assert.Equal("Jetzt mit Beschreibung", cookbook.Description);
        Assert.Equal(later, cookbook.UpdatedAt);
    }

    [Fact]
    public void Rename_ShouldLeaveTheCookbookAloneWhenItFails()
    {
        // Arrange
        var cookbook = Made();

        // Act
        var renamed = cookbook.Revise(
            NameOf("Anders"),
            new string('a', Cookbook.MaxDescriptionLength + 1),
            rules: null,
            Now.AddDays(1));

        // Assert
        renamed.ShouldBeFailure(CookbookErrors.InvalidDescription);
        Assert.Equal("Weihnachten", cookbook.Name.Value);
        Assert.Equal(Now, cookbook.UpdatedAt);
    }

    [Fact]
    public void Touch_ShouldRecordThatWhatIsOnItChanged()
    {
        // Arrange
        // Adding a recipe changes the cookbook as anybody reading it sees it —
        // the count, and the pictures on its cover.
        var cookbook = Made();
        var later = Now.AddHours(3);

        // Act
        cookbook.Touch(later);

        // Assert
        Assert.Equal(later, cookbook.UpdatedAt);
    }

    [Fact]
    public void CreateSmart_ShouldRefuseAShelfThatAsksForNothing()
    {
        // Arrange
        // A shelf with no rules is every recipe you have, which is the screen
        // it would be reached from.
        var rules = RulesOf([], [], null);

        // Act
        var created = Cookbook.CreateSmart(
            Guid.NewGuid(), NameOf("Alles"), null, rules, Guid.NewGuid(), Now);

        // Assert
        created.ShouldBeFailure(CookbookErrors.RulesRequired);
    }

    [Fact]
    public void CreateSmart_ShouldKeepWhatItAsksFor()
    {
        // Act
        var created = Cookbook.CreateSmart(
            Guid.NewGuid(),
            NameOf("Hähnchen"),
            null,
            RulesOf(["hauptspeise"], ["Hähnchen"], 45),
            Guid.NewGuid(),
            Now);

        // Assert
        var cookbook = created.ShouldBeSuccess();

        Assert.Equal(CookbookKind.Smart, cookbook.Kind);
        Assert.Equal(["hauptspeise"], cookbook.Rules.Tags);
        Assert.Equal(["Hähnchen"], cookbook.Rules.Ingredients);
        Assert.Equal(45, cookbook.Rules.MaxMinutes);
    }

    [Fact]
    public void Rules_ShouldCountTheSameTermTwiceAsOneCondition()
    {
        // Arrange
        // The same word typed twice is one question asked twice, and a shelf
        // reporting "2 rules" for it would be counting the typing.
        // Act
        var rules = CookbookRules.Create(["quick", "Quick", " quick "], [], null);

        // Assert
        Assert.Equal(["quick"], rules.ShouldBeSuccess().Tags);
    }

    [Fact]
    public void Rules_ShouldRefuseATimeNoRecipeCouldTake()
    {
        // Act
        var rules = CookbookRules.Create([], [], 0);

        // Assert
        rules.ShouldBeFailure(CookbookErrors.InvalidRule);
    }

    [Fact]
    public void Revise_ShouldRefuseToEmptyASmartCookbooksRules()
    {
        // Arrange
        // Clearing the last rule would silently turn a shelf that fills itself
        // into one containing everything.
        var cookbook = Smart();

        // Act
        var revised = cookbook.Revise(NameOf("Immer noch"), null, RulesOf([], [], null), Now);

        // Assert
        revised.ShouldBeFailure(CookbookErrors.RulesRequired);
        Assert.Equal(["hauptspeise"], cookbook.Rules.Tags);
    }

    [Fact]
    public void Revise_ShouldRefuseToGiveAManualCookbookRules()
    {
        // Arrange
        // Which kind a shelf is answers "why is this recipe here?", and a shelf
        // that changed its mind would have two answers for the recipes already
        // on it.
        var cookbook = Made();

        // Act
        var revised = cookbook.Revise(NameOf("Egal"), null, RulesOf(["schnell"], [], null), Now);

        // Assert
        revised.ShouldBeFailure(CookbookErrors.RulesDecideMembership);
        Assert.Equal(CookbookKind.Manual, cookbook.Kind);
    }

    [Fact]
    public void Revise_ShouldChangeWhatASmartCookbookAsksFor()
    {
        // Arrange
        var cookbook = Smart();

        // Act
        var revised = cookbook.Revise(
            NameOf("Schneller"), null, RulesOf(["hauptspeise"], [], 20), Now.AddDays(1));

        // Assert
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
