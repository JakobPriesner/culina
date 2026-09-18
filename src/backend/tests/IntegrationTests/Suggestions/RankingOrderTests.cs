using IntegrationTests.Fixtures;

namespace IntegrationTests.Suggestions;

/// <summary>
/// The ordering rules, which are the actual specification of the weights.
/// </summary>
/// <remarks>
/// <para>
/// The numbers in <c>RankingWeights</c> are not taste; they are the solution to
/// the constraints in this file. Change a weight and one of these tells you
/// which product promise you broke — which is the difference between a tuned
/// system and a fiddled one, and the same move the theme contract test makes
/// with colour.
/// </para>
/// <para>
/// Each test asserts a <i>relation</i> between two recipes rather than an
/// absolute position, because an absolute position is a fact about the whole
/// fixture and breaks for reasons that have nothing to do with the rule.
/// </para>
/// </remarks>
[Collection(RequiresDatabase.Name)]
public class RankingOrderTests(PostgresFixture postgres)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Rule1_ARecipeCookedYesterday_ShouldNotOutrankTheSameOneCookedThreeWeeksAgo()
    {
        // Arrange
        // Fixes the repetition weight against the affinity weight. Two recipes
        // the household likes identically; only when they last ate them differs.
        var world = await SuggestionWorld.NewAsync(postgres);

        var fresh = await world.WriteAsync("Yesterday", ingredients: ["rice", "egg"]);
        var rested = await world.WriteAsync("Three weeks ago", ingredients: ["rice", "egg"]);

        foreach (var daysAgo in new[] { 200, 240, 280 })
        {
            await world.CookedAsync(fresh, daysAgo);
            await world.CookedAsync(rested, daysAgo);
        }

        await world.CookedAsync(fresh, daysAgo: 1);
        await world.CookedAsync(rested, daysAgo: 21);

        // Act
        var response = await world.SuggestAsync("&limit=5");

        // Assert
        Assert.True(
            SuggestionWorld.PositionOf(response, "Three weeks ago")
            < SuggestionWorld.PositionOf(response, "Yesterday"),
            "A recipe eaten yesterday must sink below the same recipe eaten three weeks ago.");
    }

    [Fact]
    public async Task Rule2_AmongRecipesNobodyHasCooked_TheOneMatchingTheirTasteShouldWin()
    {
        // Arrange
        // Fixes the content weight, and it is stated between two UNTOUCHED
        // recipes on purpose. Comparing an untouched recipe against a cooked one
        // measures content plus affinity plus rediscovery all at once, and an
        // assertion about three terms cannot fix any of them.
        //
        // This is the rule that makes week one useful: a library nobody has
        // worked through can otherwise only be ordered by chance.
        var world = await SuggestionWorld.NewAsync(postgres);

        var curry = await world.WriteAsync(
            "Known curry",
            ingredients: ["aubergine", "coconut milk", "curry paste"],
            tags: ["curry"]);
        await world.CookedAsync(curry, daysAgo: 120);
        await world.CookedAsync(curry, daysAgo: 150);
        await world.CookedAsync(curry, daysAgo: 200);

        await world.WriteAsync(
            "Untouched curry",
            ingredients: ["aubergine", "coconut milk", "curry paste"],
            tags: ["curry"]);

        await world.WriteAsync(
            "Untouched, unrelated",
            ingredients: ["cod", "potato"],
            tags: ["fisch"]);

        // Act
        var response = await world.SuggestAsync("&limit=5");

        // Assert
        Assert.True(
            SuggestionWorld.PositionOf(response, "Untouched curry")
            < SuggestionWorld.PositionOf(response, "Untouched, unrelated"),
            "Among recipes nobody has cooked, the one matching their taste must come first.");
    }

    [Fact]
    public async Task Rule2b_AnUntouchedRecipeMatchingTheirTaste_ShouldBeatOneTheyAteYesterday()
    {
        // Arrange
        // The interaction worth pinning: content has to be strong enough to lift
        // something nobody has tried over something they like but had last night.
        // Without it, a household's rotation is self-reinforcing and nothing new
        // is ever surfaced.
        var world = await SuggestionWorld.NewAsync(postgres);

        var curry = await world.WriteAsync(
            "Known curry",
            ingredients: ["aubergine", "coconut milk", "curry paste"],
            tags: ["curry"]);
        await world.CookedAsync(curry, daysAgo: 1);
        await world.CookedAsync(curry, daysAgo: 30);
        await world.CookedAsync(curry, daysAgo: 60);

        await world.WriteAsync(
            "Untouched curry",
            ingredients: ["aubergine", "coconut milk", "curry paste"],
            tags: ["curry"]);

        // Act
        var response = await world.SuggestAsync("&limit=5");

        // Assert
        Assert.True(
            SuggestionWorld.PositionOf(response, "Untouched curry")
            < SuggestionWorld.PositionOf(response, "Known curry"),
            "Something they have not tried must beat their favourite the day after they ate it.");
    }

    [Fact]
    public async Task Rule3_ALovedRecipeUnmadeForMonths_ShouldBeatAMediocreOneMadeLastMonth()
    {
        // Arrange
        // Fixes the rediscovery weight. The thing a small library is uniquely
        // good at: a household keeps three hundred recipes and cooks twenty.
        var world = await SuggestionWorld.NewAsync(postgres);

        var loved = await world.WriteAsync("Forgotten favourite", ingredients: ["lamb", "apricot"]);

        foreach (var daysAgo in new[] { 240, 280, 320, 360, 400 })
        {
            await world.CookedAsync(loved, daysAgo);
        }

        var recent = await world.WriteAsync("Fine, made recently", ingredients: ["tofu", "noodles"]);
        await world.CookedAsync(recent, daysAgo: 30);

        // Act
        var response = await world.SuggestAsync("&limit=5");

        // Assert
        Assert.True(
            SuggestionWorld.PositionOf(response, "Forgotten favourite")
            < SuggestionWorld.PositionOf(response, "Fine, made recently"),
            "Something they loved and have not made for months must come back up.");
    }

    [Fact]
    public async Task Rule3b_RediscoveryShouldNotLift_ARecipeTheyNeverActuallyLiked()
    {
        // Arrange
        // The other half of the rule, and the one that keeps the list from
        // turning into archaeology: rediscovery is scaled by affinity, so a
        // recipe nobody ever cooked is not "overdue", it is simply untouched.
        var world = await SuggestionWorld.NewAsync(postgres);

        var untouched = await world.WriteAsync("Never made", ingredients: ["okra"]);
        var loved = await world.WriteAsync("Loved and overdue", ingredients: ["lamb"]);

        foreach (var daysAgo in new[] { 240, 300, 360 })
        {
            await world.CookedAsync(loved, daysAgo);
        }

        // Act
        var response = await world.SuggestAsync("&limit=5");

        // Assert
        Assert.True(
            SuggestionWorld.PositionOf(response, "Loved and overdue")
            < SuggestionWorld.PositionOf(response, "Never made"),
            "Rediscovery must lift what was liked, not merely what is old.");
        Assert.NotEqual(Guid.Empty, untouched);
    }

    [Fact]
    public async Task Rule4_AWeeknightShouldPreferTheQuickerOfTwoEqualRecipes()
    {
        // Arrange
        // Fixes the effort weight. Both are unknown to the household, so time
        // is the only thing separating them.
        var world = await SuggestionWorld.NewAsync(postgres);

        await world.WriteAsync("Twenty minutes", ingredients: ["egg"], prep: 10, cook: 10, steps: 2);
        await world.WriteAsync("Three hours", ingredients: ["egg"], prep: 30, cook: 150, steps: 9);

        // Act
        var response = await world.SuggestAsync("&limit=5");

        // Assert
        // Asserted as a relation rather than a first place, because on a
        // Saturday the sign of this term flips by design and the rule is about
        // its magnitude either way.
        var quick = SuggestionWorld.PositionOf(response, "Twenty minutes");
        var slow = SuggestionWorld.PositionOf(response, "Three hours");

        var weekend = DateTime.UtcNow.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

        Assert.True(
            weekend ? slow < quick : quick < slow,
            "A weekday should prefer the quicker recipe, and a weekend should tolerate the project.");
    }

    [Fact]
    public async Task Rule5_ARecipeAlwaysPlannedForBreakfast_ShouldNotLeadADinnerList()
    {
        // Arrange
        // Fixes the slot weight. Meal type is not a column on a recipe; it is
        // what this household's plan says about it.
        var world = await SuggestionWorld.NewAsync(postgres);

        var porridge = await world.WriteAsync("Porridge", ingredients: ["oats"]);
        var stew = await world.WriteAsync("Stew", ingredients: ["oats"]);

        var monday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-28);

        for (var week = 0; week < 4; week++)
        {
            await world.PlanAsync(porridge, monday.AddDays(week * 7), "breakfast");
            await world.PlanAsync(stew, monday.AddDays((week * 7) + 1), "dinner");
        }

        // Act
        var response = await world.SuggestAsync("&limit=5&slot=dinner");

        // Assert
        Assert.True(
            SuggestionWorld.PositionOf(response, "Stew")
            < SuggestionWorld.PositionOf(response, "Porridge"),
            "A recipe this household only ever plans for breakfast must not lead a dinner list.");
    }

    [Fact]
    public async Task Rule6_AnotherMembersFavourite_ShouldAppearWithoutTakingOver()
    {
        // Arrange
        // Bounds the household weight from BOTH sides, which is the point. It
        // has to be big enough that your partner's cooking is discoverable and
        // small enough that two people's different tastes are not flattened
        // into one household average.
        var world = await SuggestionWorld.NewAsync(postgres);
        var guest = await world.InviteAsync(postgres, "bob@example.com", "Bob");

        var theirs = await world.WriteAsync("Bob's favourite", ingredients: ["liver"]);

        for (var week = 1; week <= 8; week++)
        {
            await world.CookedAsync(theirs, daysAgo: week * 7, by: guest);
        }

        var mine = await world.WriteAsync("Mine", ingredients: ["mushroom", "cream"]);
        await world.CookedAsync(mine, daysAgo: 100);
        await world.CookedAsync(mine, daysAgo: 160);

        // A few others so "top three" means something.
        for (var index = 0; index < 4; index++)
        {
            await world.WriteAsync($"Filler {index}", ingredients: ["mushroom"]);
        }

        // Act
        var response = await world.SuggestAsync("&limit=12");

        // Assert
        var position = SuggestionWorld.PositionOf(response, "Bob's favourite");

        Assert.True(position >= 0, "Another member's favourite must be discoverable.");
        Assert.True(
            position >= 2,
            "Another member's favourite must not lead this person's own suggestions.");
    }

    [Fact]
    public async Task Rule7_ExplorationShouldNotPromoteSomethingThatLost()
    {
        // Arrange
        // Bounds the exploration weight. It exists to reshuffle near-equals, and
        // a jitter large enough to move a clear winner is noise rather than
        // variety.
        var world = await SuggestionWorld.NewAsync(postgres);

        var loved = await world.WriteAsync("Clear winner", ingredients: ["beef", "onion"]);

        foreach (var daysAgo in new[] { 120, 150, 180, 210, 240 })
        {
            await world.CookedAsync(loved, daysAgo);
        }

        for (var index = 0; index < 10; index++)
        {
            await world.WriteAsync($"Stranger {index}", ingredients: [$"thing {index}"]);
        }

        // Act
        var response = await world.SuggestAsync("&limit=3");

        // Assert
        Assert.Equal(0, SuggestionWorld.PositionOf(response, "Clear winner"));
    }

    [Fact]
    public async Task Rule8_AnEmptyHistoryShouldStillProduceAnOrder_AndNotAnAlphabeticalOne()
    {
        // Arrange
        // Titles chosen so that alphabetical order and insertion order are both
        // recognisable, and neither is what should come out.
        var world = await SuggestionWorld.NewAsync(postgres);

        foreach (var title in new[] { "Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot" })
        {
            await world.WriteAsync(title, ingredients: [title.ToLowerInvariant()]);
        }

        // Act
        var response = await world.SuggestAsync("&limit=6");

        // Assert
        var titles = SuggestionWorld.Titles(response);

        Assert.Equal(6, titles.Count);
        Assert.NotEqual(["Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot"], titles);
    }

    [Fact]
    public async Task Rule9_DiversityShouldBreakUpARunOfNearlyIdenticalRecipes()
    {
        // Arrange
        // Five suggestions that are five pasta dishes is what a small library
        // produces most often, and it is the scoring being faithful to a taste
        // that really is narrow. The diversity pass is what stops faithful from
        // becoming useless.
        var world = await SuggestionWorld.NewAsync(postgres);

        for (var index = 0; index < 6; index++)
        {
            await world.WriteAsync(
                $"Pasta {index}",
                ingredients: ["pasta", "tomato", "garlic", "olive oil", "basil"],
                tags: ["pasta", "italienisch"]);
        }

        for (var index = 0; index < 3; index++)
        {
            await world.WriteAsync(
                $"Not pasta {index}",
                ingredients: [$"fish {index}", "lemon", "butter"],
                tags: ["fisch"]);
        }

        // Act
        var response = await world.SuggestAsync("&limit=4");

        // Assert
        var titles = SuggestionWorld.Titles(response);

        Assert.Contains(titles, title => title.StartsWith("Not pasta", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Rule11_CloseToThisOne_ShouldMeanCloseToThisOne_NotWhatTheyCookMost()
    {
        // Arrange
        // The rule that keeps a heading honest. "Close to this one" is a
        // question about the recipe on screen, and with personal taste left at
        // full strength a favourite outscores a genuine resemblance — so the
        // strip quietly fills with the same recipes the home page already
        // suggests, under a heading that promises something else.
        var world = await SuggestionWorld.NewAsync(postgres);

        var reading = await world.WriteAsync(
            "Linsensuppe",
            ingredients: ["rote linsen", "ingwer", "kokosmilch"],
            tags: ["suppe"]);

        await world.WriteAsync(
            "Kürbissuppe",
            ingredients: ["hokkaido", "ingwer", "kokosmilch"],
            tags: ["suppe"]);

        // Cooked constantly, and nothing like a soup.
        var favourite = await world.WriteAsync(
            "Carbonara",
            ingredients: ["spaghetti", "guanciale", "ei"],
            tags: ["pasta"]);

        foreach (var daysAgo in new[] { 20, 50, 80, 110, 140, 170 })
        {
            await world.CookedAsync(favourite, daysAgo);
        }

        // Act
        var response = await world.SuggestAsync($"&limit=3&likeRecipeId={reading}");

        // Assert
        Assert.True(
            SuggestionWorld.PositionOf(response, "Kürbissuppe")
            < SuggestionWorld.PositionOf(response, "Carbonara"),
            "What resembles the recipe on screen must beat what this person cooks most.");
    }

    [Fact]
    public async Task Rule10_TheSameQuestionOnTheSameDay_ShouldGiveTheSameAnswer()
    {
        // Arrange
        var world = await SuggestionWorld.NewAsync(postgres);

        for (var index = 0; index < 10; index++)
        {
            var recipeId = await world.WriteAsync($"Recipe {index}", ingredients: ["salt", $"x{index}"]);

            if (index % 3 == 0)
            {
                await world.CookedAsync(recipeId, daysAgo: 30 + index);
            }
        }

        // Act
        var first = await world.SuggestAsync("&limit=6");
        var second = await world.SuggestAsync("&limit=6");
        var slotted = await world.SuggestAsync("&limit=6&slot=dinner");

        // Assert
        Assert.Equal(SuggestionWorld.Ids(first), SuggestionWorld.Ids(second));
        // A different question may legitimately give a different answer; what
        // must not happen is the same question giving two.
        Assert.Equal(6, SuggestionWorld.Ids(slotted).Count);
    }

    [Fact]
    public async Task IdfShouldSilenceAnIngredientEveryRecipeHas()
    {
        // Arrange
        // In a kitchen where nine recipes in ten contain salt, salt must carry
        // no information at all — otherwise the taste profile is dominated by
        // the store cupboard and every recipe looks equally like every other.
        var world = await SuggestionWorld.NewAsync(postgres);

        var cooked = await world.WriteAsync("Cooked", ingredients: ["salt", "saffron"]);
        await world.CookedAsync(cooked, daysAgo: 60);
        await world.CookedAsync(cooked, daysAgo: 90);

        var sharesSaffron = await world.WriteAsync("Shares the rare thing", ingredients: ["salt", "saffron"]);
        var sharesSalt = await world.WriteAsync("Shares only salt", ingredients: ["salt", "cabbage"]);

        for (var index = 0; index < 6; index++)
        {
            await world.WriteAsync($"Also salted {index}", ingredients: ["salt", $"thing {index}"]);
        }

        // Act
        var response = await world.SuggestAsync("&limit=12");

        // Assert
        Assert.True(
            SuggestionWorld.PositionOf(response, "Shares the rare thing")
            < SuggestionWorld.PositionOf(response, "Shares only salt"),
            "Sharing a rare ingredient must count for more than sharing one everything has.");
        Assert.NotEqual(sharesSaffron, sharesSalt);
    }
}
