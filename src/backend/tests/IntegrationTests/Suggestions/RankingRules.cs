using IntegrationTests.Fixtures;

namespace IntegrationTests.Suggestions;

/// <summary>
/// The two hosts a set of weights is checked through: one ranking without its
/// exploration jitter, and one ranking exactly as people get it.
/// </summary>
internal sealed record RankingHosts(CulinaApiFactory Steady, CulinaApiFactory Jittered);

/// <summary>
/// One ordering rule: a kitchen, a question, and the relation the answer must keep.
/// </summary>
/// <param name="Name">What the rule is called wherever a weight's comment cites it.</param>
/// <param name="BrokenAsync">Builds its kitchen in the world, asks, and says why the rule broke — or null when it held.</param>
/// <param name="Steady">
/// Whether the rule is proven with the exploration jitter held still.
/// <para>
/// Almost all of them are. The jitter is worth ±0.075 per recipe and is seeded
/// by the recipe's id, so between two recipes it spans ±0.15 — larger than some
/// of the very terms these rules exist to pin down. Rule 4 is the honest
/// example: a weekday separates its two recipes by 0.30 and is never in doubt,
/// while a weekend separates them by 0.06 and the jitter decided it instead,
/// which is how that test came to fail roughly one Sunday run in six. A rule is
/// proven with the other terms held still; the jitter is a term like any other.
/// </para>
/// <para>
/// The exception is the rule <i>about</i> the jitter, which held still would
/// hold for any exploration weight at all and so bound nothing.
/// </para>
/// </param>
internal sealed record RankingRule(string Name, Func<SuggestionWorld, Task<string?>> BrokenAsync, bool Steady = true)
{
    /// <summary>Runs the rule against whichever of the hosts it is proven on.</summary>
    internal async Task<string?> CheckAsync(PostgresFixture postgres, RankingHosts hosts)
    {
        var world = await SuggestionWorld.NewAsync(postgres, api: Steady ? hosts.Steady : hosts.Jittered);

        return await BrokenAsync(world);
    }

    public override string ToString() => Name;
}

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
/// Rules rather than tests, so that the same promises can be put to any weight
/// vector: <c>RankingOrderTests</c> puts them to the weights that ship, and a
/// calibration puts them to every vector it is about to propose. Restating them
/// there would be two specifications that drift.
/// </para>
/// <para>
/// Each rule asserts a <i>relation</i> between two recipes rather than an
/// absolute position, because an absolute position is a fact about the whole
/// fixture and breaks for reasons that have nothing to do with the rule.
/// </para>
/// </remarks>
internal static class RankingRules
{
    internal static readonly IReadOnlyList<RankingRule> All =
    [
        new(nameof(Rule1_ARecipeCookedYesterday_ShouldNotOutrankTheSameOneCookedThreeWeeksAgo), Rule1_ARecipeCookedYesterday_ShouldNotOutrankTheSameOneCookedThreeWeeksAgo),
        new(nameof(Rule2_AmongRecipesNobodyHasCooked_TheOneMatchingTheirTasteShouldWin), Rule2_AmongRecipesNobodyHasCooked_TheOneMatchingTheirTasteShouldWin),
        new(nameof(Rule2b_AnUntouchedRecipeMatchingTheirTaste_ShouldBeatOneTheyAteYesterday), Rule2b_AnUntouchedRecipeMatchingTheirTaste_ShouldBeatOneTheyAteYesterday),
        new(nameof(Rule3_ALovedRecipeUnmadeForMonths_ShouldBeatAMediocreOneMadeLastMonth), Rule3_ALovedRecipeUnmadeForMonths_ShouldBeatAMediocreOneMadeLastMonth),
        new(nameof(Rule3b_RediscoveryShouldNotLift_ARecipeTheyNeverActuallyLiked), Rule3b_RediscoveryShouldNotLift_ARecipeTheyNeverActuallyLiked),
        new(nameof(Rule4_AWeeknightShouldPreferTheQuickerOfTwoEqualRecipes), Rule4_AWeeknightShouldPreferTheQuickerOfTwoEqualRecipes),
        new(nameof(Rule5_ARecipeAlwaysPlannedForBreakfast_ShouldNotLeadADinnerList), Rule5_ARecipeAlwaysPlannedForBreakfast_ShouldNotLeadADinnerList),
        new(nameof(Rule6_AnotherMembersFavourite_ShouldAppearWithoutTakingOver), Rule6_AnotherMembersFavourite_ShouldAppearWithoutTakingOver),
        new(nameof(Rule7_ExplorationShouldNotPromoteSomethingThatLost), Rule7_ExplorationShouldNotPromoteSomethingThatLost, Steady: false),
        new(nameof(Rule8_AnEmptyHistoryShouldStillProduceAnOrder_AndNotAnAlphabeticalOne), Rule8_AnEmptyHistoryShouldStillProduceAnOrder_AndNotAnAlphabeticalOne),
        new(nameof(Rule9_DiversityShouldBreakUpARunOfNearlyIdenticalRecipes), Rule9_DiversityShouldBreakUpARunOfNearlyIdenticalRecipes),
        new(nameof(Rule10_TheSameQuestionOnTheSameDay_ShouldGiveTheSameAnswer), Rule10_TheSameQuestionOnTheSameDay_ShouldGiveTheSameAnswer),
        new(nameof(Rule11_CloseToThisOne_ShouldMeanCloseToThisOne_NotWhatTheyCookMost), Rule11_CloseToThisOne_ShouldMeanCloseToThisOne_NotWhatTheyCookMost),
        new(nameof(IdfShouldSilenceAnIngredientEveryRecipeHas), IdfShouldSilenceAnIngredientEveryRecipeHas)
    ];

    internal static RankingRule Named(string name) => All.Single(rule => rule.Name == name);

    /// <summary>Every rule the weights behind these hosts break, each with why. Empty when they keep them all.</summary>
    internal static async Task<IReadOnlyList<string>> BrokenAsync(PostgresFixture postgres, RankingHosts hosts)
    {
        List<string> broken = [];

        foreach (var rule in All)
        {
            if (await rule.CheckAsync(postgres, hosts) is { } why)
            {
                broken.Add($"{rule.Name}: {why}");
            }
        }

        return broken;
    }

    /// <summary>Null when the relation held; the promise it broke when it did not.</summary>
    private static string? Verdict(bool held, string promise) => held ? null : promise;

    private static bool Before(ApiResponse response, string first, string second) =>
        SuggestionWorld.PositionOf(response, first) < SuggestionWorld.PositionOf(response, second);

    private static async Task<string?> Rule1_ARecipeCookedYesterday_ShouldNotOutrankTheSameOneCookedThreeWeeksAgo(SuggestionWorld world)
    {
        // Fixes the repetition weight against the affinity weight. Two recipes
        // the household likes identically; only when they last ate them differs.
        var fresh = await world.WriteAsync("Yesterday", ingredients: ["rice", "egg"]);
        var rested = await world.WriteAsync("Three weeks ago", ingredients: ["rice", "egg"]);

        foreach (var daysAgo in new[] { 200, 240, 280 })
        {
            await world.CookedAsync(fresh, daysAgo);
            await world.CookedAsync(rested, daysAgo);
        }

        await world.CookedAsync(fresh, daysAgo: 1);
        await world.CookedAsync(rested, daysAgo: 21);

        var response = await world.SuggestAsync("&limit=5");

        return Verdict(
            Before(response, "Three weeks ago", "Yesterday"),
            "A recipe eaten yesterday must sink below the same recipe eaten three weeks ago.");
    }

    private static async Task<string?> Rule2_AmongRecipesNobodyHasCooked_TheOneMatchingTheirTasteShouldWin(SuggestionWorld world)
    {
        // Fixes the content weight, and it is stated between two UNTOUCHED
        // recipes on purpose. Comparing an untouched recipe against a cooked one
        // measures content plus affinity plus rediscovery all at once, and an
        // assertion about three terms cannot fix any of them.
        //
        // This is the rule that makes week one useful: a library nobody has
        // worked through can otherwise only be ordered by chance.
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

        var response = await world.SuggestAsync("&limit=5");

        return Verdict(
            Before(response, "Untouched curry", "Untouched, unrelated"),
            "Among recipes nobody has cooked, the one matching their taste must come first.");
    }

    private static async Task<string?> Rule2b_AnUntouchedRecipeMatchingTheirTaste_ShouldBeatOneTheyAteYesterday(SuggestionWorld world)
    {
        // The interaction worth pinning: content has to be strong enough to lift
        // something nobody has tried over something they like but had last night.
        // Without it, a household's rotation is self-reinforcing and nothing new
        // is ever surfaced.
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

        var response = await world.SuggestAsync("&limit=5");

        return Verdict(
            Before(response, "Untouched curry", "Known curry"),
            "Something they have not tried must beat their favourite the day after they ate it.");
    }

    private static async Task<string?> Rule3_ALovedRecipeUnmadeForMonths_ShouldBeatAMediocreOneMadeLastMonth(SuggestionWorld world)
    {
        // Fixes the rediscovery weight. The thing a small library is uniquely
        // good at: a household keeps three hundred recipes and cooks twenty.
        var loved = await world.WriteAsync("Forgotten favourite", ingredients: ["lamb", "apricot"]);

        foreach (var daysAgo in new[] { 240, 280, 320, 360, 400 })
        {
            await world.CookedAsync(loved, daysAgo);
        }

        var recent = await world.WriteAsync("Fine, made recently", ingredients: ["tofu", "noodles"]);
        await world.CookedAsync(recent, daysAgo: 30);

        var response = await world.SuggestAsync("&limit=5");

        return Verdict(
            Before(response, "Forgotten favourite", "Fine, made recently"),
            "Something they loved and have not made for months must come back up.");
    }

    private static async Task<string?> Rule3b_RediscoveryShouldNotLift_ARecipeTheyNeverActuallyLiked(SuggestionWorld world)
    {
        // The other half of the rule, and the one that keeps the list from
        // turning into archaeology: rediscovery is scaled by affinity, so a
        // recipe nobody ever cooked is not "overdue", it is simply untouched.
        await world.WriteAsync("Never made", ingredients: ["okra"]);
        var loved = await world.WriteAsync("Loved and overdue", ingredients: ["lamb"]);

        foreach (var daysAgo in new[] { 240, 300, 360 })
        {
            await world.CookedAsync(loved, daysAgo);
        }

        var response = await world.SuggestAsync("&limit=5");

        return Verdict(
            Before(response, "Loved and overdue", "Never made"),
            "Rediscovery must lift what was liked, not merely what is old.");
    }

    private static async Task<string?> Rule4_AWeeknightShouldPreferTheQuickerOfTwoEqualRecipes(SuggestionWorld world)
    {
        // Fixes the effort weight. Both are unknown to the household, so time
        // is the only thing separating them.
        await world.WriteAsync("Twenty minutes", ingredients: ["egg"], prep: 10, cook: 10, steps: 2);
        await world.WriteAsync("Three hours", ingredients: ["egg"], prep: 30, cook: 150, steps: 9);

        var response = await world.SuggestAsync("&limit=5");

        // A relation rather than a first place, because on a Saturday the sign
        // of this term flips by design and the rule is about its magnitude
        // either way.
        var weekend = DateTime.UtcNow.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

        return Verdict(
            weekend ? Before(response, "Three hours", "Twenty minutes") : Before(response, "Twenty minutes", "Three hours"),
            "A weekday should prefer the quicker recipe, and a weekend should tolerate the project.");
    }

    private static async Task<string?> Rule5_ARecipeAlwaysPlannedForBreakfast_ShouldNotLeadADinnerList(SuggestionWorld world)
    {
        // Fixes the slot weight. Meal type is not a column on a recipe; it is
        // what this household's plan says about it.
        var porridge = await world.WriteAsync("Porridge", ingredients: ["oats"]);
        var stew = await world.WriteAsync("Stew", ingredients: ["oats"]);

        var monday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-28);

        for (var week = 0; week < 4; week++)
        {
            await world.PlanAsync(porridge, monday.AddDays(week * 7), "breakfast");
            await world.PlanAsync(stew, monday.AddDays((week * 7) + 1), "dinner");
        }

        var response = await world.SuggestAsync("&limit=5&slot=dinner");

        return Verdict(
            Before(response, "Stew", "Porridge"),
            "A recipe this household only ever plans for breakfast must not lead a dinner list.");
    }

    private static async Task<string?> Rule6_AnotherMembersFavourite_ShouldAppearWithoutTakingOver(SuggestionWorld world)
    {
        // Bounds the household weight from BOTH sides, which is the point. It
        // has to be big enough that your partner's cooking is discoverable and
        // small enough that two people's different tastes are not flattened
        // into one household average.
        var guest = await world.InviteAsync("bob@example.com", "Bob");

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

        var response = await world.SuggestAsync("&limit=12");
        var position = SuggestionWorld.PositionOf(response, "Bob's favourite");

        return Verdict(position >= 0, "Another member's favourite must be discoverable.")
            ?? Verdict(position >= 2, "Another member's favourite must not lead this person's own suggestions.");
    }

    private static async Task<string?> Rule7_ExplorationShouldNotPromoteSomethingThatLost(SuggestionWorld world)
    {
        // Bounds the exploration weight. It exists to reshuffle near-equals, and
        // a jitter large enough to move a clear winner is noise rather than
        // variety.
        var loved = await world.WriteAsync("Clear winner", ingredients: ["beef", "onion"]);

        foreach (var daysAgo in new[] { 120, 150, 180, 210, 240 })
        {
            await world.CookedAsync(loved, daysAgo);
        }

        for (var index = 0; index < 10; index++)
        {
            await world.WriteAsync($"Stranger {index}", ingredients: [$"thing {index}"]);
        }

        var response = await world.SuggestAsync("&limit=3");

        return Verdict(
            SuggestionWorld.PositionOf(response, "Clear winner") == 0,
            "Exploration must reshuffle near-equals, never unseat a clear winner.");
    }

    private static async Task<string?> Rule8_AnEmptyHistoryShouldStillProduceAnOrder_AndNotAnAlphabeticalOne(SuggestionWorld world)
    {
        // Titles chosen so that alphabetical order and insertion order are both
        // recognisable, and neither is what should come out.
        string[] alphabetical = ["Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot"];

        foreach (var title in alphabetical)
        {
            await world.WriteAsync(title, ingredients: [title.ToLowerInvariant()]);
        }

        var titles = SuggestionWorld.Titles(await world.SuggestAsync("&limit=6"));

        return Verdict(titles.Count == 6, "An empty history must still rank every recipe.")
            ?? Verdict(!titles.SequenceEqual(alphabetical), "An empty history must not fall back to alphabetical order.");
    }

    private static async Task<string?> Rule9_DiversityShouldBreakUpARunOfNearlyIdenticalRecipes(SuggestionWorld world)
    {
        // Five suggestions that are five pasta dishes is what a small library
        // produces most often, and it is the scoring being faithful to a taste
        // that really is narrow. The diversity pass is what stops faithful from
        // becoming useless.
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

        var titles = SuggestionWorld.Titles(await world.SuggestAsync("&limit=4"));

        return Verdict(
            titles.Exists(title => title.StartsWith("Not pasta", StringComparison.Ordinal)),
            "Four near-identical pasta dishes must not fill a shortlist when something else exists.");
    }

    private static async Task<string?> Rule10_TheSameQuestionOnTheSameDay_ShouldGiveTheSameAnswer(SuggestionWorld world)
    {
        for (var index = 0; index < 10; index++)
        {
            var recipeId = await world.WriteAsync($"Recipe {index}", ingredients: ["salt", $"x{index}"]);

            if (index % 3 == 0)
            {
                await world.CookedAsync(recipeId, daysAgo: 30 + index);
            }
        }

        var first = await world.SuggestAsync("&limit=6");
        var second = await world.SuggestAsync("&limit=6");
        var slotted = await world.SuggestAsync("&limit=6&slot=dinner");

        // A different question may legitimately give a different answer; what
        // must not happen is the same question giving two.
        return Verdict(
                SuggestionWorld.Ids(first).SequenceEqual(SuggestionWorld.Ids(second)),
                "The same question on the same day must give the same answer.")
            ?? Verdict(SuggestionWorld.Ids(slotted).Count == 6, "A slotted question must still fill its list.");
    }

    private static async Task<string?> Rule11_CloseToThisOne_ShouldMeanCloseToThisOne_NotWhatTheyCookMost(SuggestionWorld world)
    {
        // The rule that keeps a heading honest. "Close to this one" is a
        // question about the recipe on screen, and with personal taste left at
        // full strength a favourite outscores a genuine resemblance — so the
        // strip quietly fills with the same recipes the home page already
        // suggests, under a heading that promises something else.
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

        var response = await world.SuggestAsync($"&limit=3&likeRecipeId={reading}");

        return Verdict(
            Before(response, "Kürbissuppe", "Carbonara"),
            "What resembles the recipe on screen must beat what this person cooks most.");
    }

    private static async Task<string?> IdfShouldSilenceAnIngredientEveryRecipeHas(SuggestionWorld world)
    {
        // In a kitchen where nine recipes in ten contain salt, salt must carry
        // no information at all — otherwise the taste profile is dominated by
        // the store cupboard and every recipe looks equally like every other.
        var cooked = await world.WriteAsync("Cooked", ingredients: ["salt", "saffron"]);
        await world.CookedAsync(cooked, daysAgo: 60);
        await world.CookedAsync(cooked, daysAgo: 90);

        await world.WriteAsync("Shares the rare thing", ingredients: ["salt", "saffron"]);
        await world.WriteAsync("Shares only salt", ingredients: ["salt", "cabbage"]);

        for (var index = 0; index < 6; index++)
        {
            await world.WriteAsync($"Also salted {index}", ingredients: ["salt", $"thing {index}"]);
        }

        var response = await world.SuggestAsync("&limit=12");

        return Verdict(
            Before(response, "Shares the rare thing", "Shares only salt"),
            "Sharing a rare ingredient must count for more than sharing one everything has.");
    }
}
