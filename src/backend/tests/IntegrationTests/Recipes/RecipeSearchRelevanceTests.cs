using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Domain.Search;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Recipes;
using IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IntegrationTests.Recipes;

/// <summary>
/// A hand-written golden set of queries and the recipes they must find, over a deliberately awkward library.
/// </summary>
[Collection(RequiresDatabase.Name)]
public class RecipeSearchRelevanceTests(PostgresFixture postgres)
{
    /// <summary>One query and what it must (and must not) return; <c>Top</c> is ordered, <c>TopSet</c> is not.</summary>
    private sealed record Golden(
        string Query,
        string Class,
        string[]? Top = null,
        string[]? TopSet = null,
        string[]? Contains = null,
        string[]? Excludes = null,
        string[]? NotInTop = null,
        int? Count = null);

    private static readonly Golden[] GoldenSet =
    [
        new("Spaghetti Bolognese", "known-item exact", Top: ["Spaghetti Bolognese"]),
        new("Kartoffelgratin", "known-item exact", Top: ["Kartoffelgratin"]),
        new("spaghetti bolognese", "known-item exact", Top: ["Spaghetti Bolognese"]),
        new("Bolognese", "known-item partial",
            TopSet: ["Spaghetti Bolognese", "Lasagne Bolognese"],
            Contains: ["Bolognese-Sauce auf Vorrat"],
            NotInTop: ["Gemüselasagne"]),
        new("Curry", "known-item partial",
            Contains: ["Süßkartoffelcurry", "Chicken Curry"]),

        new("Bolgnese", "typo", Contains: ["Spaghetti Bolognese", "Lasagne Bolognese"]),
        new("Bolognäse", "typo", Contains: ["Spaghetti Bolognese", "Lasagne Bolognese"]),
        new("Kartoffelgratn", "typo", Contains: ["Kartoffelgratin"]),

        new("Müsliriegel", "spelling variant", Top: ["Müsliriegel"]),
        new("Muesliriegel", "spelling variant", Top: ["Müsliriegel"]),
        new("Musliriegel", "spelling variant", Top: ["Müsliriegel"]),
        new("Süßkartoffelcurry", "spelling variant", Top: ["Süßkartoffelcurry"]),
        new("Suesskartoffelcurry", "spelling variant", Top: ["Süßkartoffelcurry"]),

        new("Tomaten", "morphology", Top: ["Tomatensuppe"]),
        // Tomatensuppe's ingredient list says "Tomate", singular.
        new("Tomate", "morphology", Contains: ["Tomatensuppe", "Spaghetti Bolognese"]),
        new("Zwiebeln", "morphology", Contains: ["Zwiebelkuchen", "Tomatensuppe"]),

        new("Hähnchen", "compound", Contains: ["Hähnchenbrustfilet mit Reis"]),
        new("Haehnchen", "compound", Contains: ["Hähnchenbrustfilet mit Reis"]),
        new("Hahnchen", "compound", Contains: ["Hähnchenbrustfilet mit Reis"]),
        new("Kartoffel", "compound", Contains: ["Kartoffelgratin", "Süßkartoffelcurry"]),
        new("Lasagne", "compound",
            Top: ["Lasagne Bolognese"],
            Contains: ["Gemüselasagne"]),
        new("Müsli", "compound", Contains: ["Müsliriegel"]),

        // Zwiebelkuchen is named after it; Tomatensuppe merely contains one.
        new("Zwiebel", "field weighting",
            Top: ["Zwiebelkuchen"],
            Contains: ["Tomatensuppe"]),
        new("Reis", "field weighting", Top: ["Hähnchenbrustfilet mit Reis"]),
        new("Sauce", "field weighting", Top: ["Bolognese-Sauce auf Vorrat"]),

        new("Hackfleisch", "ingredient",
            Contains: ["Spaghetti Bolognese", "Lasagne Bolognese", "Bolognese-Sauce auf Vorrat"]),
        new("Kokosmilch", "ingredient", Contains: ["Süßkartoffelcurry"]),
        new("vegetarisch", "tag",
            Contains: ["Gemüselasagne", "Kartoffelgratin", "Tomatensuppe"],
            Excludes: ["Spaghetti Bolognese"]),
        new("italienisch", "tag",
            Contains: ["Spaghetti Bolognese", "Lasagne Bolognese"]),

        // Chicken Curry never says Hähnchen; the German recipe never says chicken.
        new("chicken", "cross-language",
            Top: ["Chicken Curry"],
            Contains: ["Hähnchenbrustfilet mit Reis"]),
        new("Hähnchen", "cross-language", Contains: ["Chicken Curry"]),
        new("italian", "cross-language", Contains: ["Spaghetti Bolognese", "Lasagne Bolognese"]),
        // Nothing in the library says Geflügel or Nudeln.
        new("Geflügel", "concept", Contains: ["Hähnchenbrustfilet mit Reis", "Chicken Curry"]),
        new("Nudeln", "concept",
            Contains: ["Spaghetti Bolognese", "Lasagne Bolognese", "Gemüselasagne"],
            Excludes: ["Kartoffelgratin"]),

        new("vegetarisch unter 30 Minuten", "constraint",
            Contains: ["Tomatensuppe", "Müsliriegel"],
            Excludes: ["Hähnchenbrustfilet mit Reis", "Kartoffelgratin", "Gemüselasagne"]),
        new("was kann ich mit Kartoffeln machen?", "ingredient-led",
            Top: ["Kartoffelgratin"],
            Excludes: ["Spaghetti Bolognese", "Müsliriegel"]),
        new("ohne Zwiebeln", "negation",
            Contains: ["Kartoffelgratin"],
            Excludes: ["Zwiebelkuchen", "Tomatensuppe", "Spaghetti Bolognese"]),
        new("Hähnchen ohne Reis", "negation",
            Contains: ["Chicken Curry"],
            Excludes: ["Hähnchenbrustfilet mit Reis"]),
        new("vegetarisch mit Lachs", "conflict", Count: 0),

        new("abgelöscht", "step text", Contains: ["Spaghetti Bolognese"]),

        new("Schnitzel", "no result", Count: 0),
        new("qwertzuiop", "no result", Count: 0)
    ];

    [Fact]
    public async Task Search_ShouldAnswerTheGoldenSet()
    {
        // Arrange
        var world = await SeedAsync();
        var report = new StringBuilder();
        var failures = 0;

        // Act
        foreach (var group in GoldenSet.GroupBy(one => one.Class))
        {
            var failed = 0;

            foreach (var golden in group)
            {
                var titles = Titles(await SearchAsync(world, golden.Query));
                var problem = Check(golden, titles);

                if (problem is null)
                {
                    continue;
                }

                failed++;
                failures++;
                report.Append(CultureInfo.InvariantCulture, $"\n  ✗ “{golden.Query}” — {problem}");
                report.Append(CultureInfo.InvariantCulture, $"\n      got: {string.Join(" · ", titles)}");
            }

            report.Insert(0, string.Empty);
            report.Append(CultureInfo.InvariantCulture,
                $"\n  {group.Key,-20} {group.Count() - failed}/{group.Count()}");
        }

        // Assert
        Assert.True(failures == 0, $"{failures} of {GoldenSet.Length} golden queries failed:{report}");
    }

    /// <summary>An exact title is never outranked by something that merely resembles the query, over every query in the set.</summary>
    [Fact]
    public async Task Search_ShouldNeverRankAResemblanceAboveTheRecipeThatIsNamed()
    {
        // Arrange
        var world = await SeedAsync();

        // Act & Assert
        foreach (var golden in GoldenSet.Where(one => one.Top is { Length: > 0 }))
        {
            var titles = Titles(await SearchAsync(world, golden.Query));

            Assert.True(
                titles.Count > 0 && titles[0] == golden.Top![0],
                $"“{golden.Query}” should lead with “{golden.Top![0]}” but returned: "
                + string.Join(" · ", titles));
        }
    }

    [Fact]
    public async Task Search_ShouldRankByRelevance_WithoutBeingAskedTo()
    {
        // Arrange
        var world = await SeedAsync();

        // Act
        // No sort parameter: a query defaults to best first.
        var titles = Titles(await SearchAsync(world, "Bolognese"));

        // Assert
        Assert.Equal(3, titles.Count);
        Assert.Equal("Bolognese-Sauce auf Vorrat", titles[^1]);
    }

    [Fact]
    public async Task Search_ShouldFindARecipe_InTheSameBreathAsSavingIt()
    {
        // Arrange
        // The search document is written in the recipe's own transaction, so the index never lags.
        var world = await SeedAsync();

        // Act
        await world.SaveAsync("Ofengemüse mit Feta", "de", 15, 30,
            [("Paprika", null), ("Feta", null)], ["ofen", "vegetarisch"], "In den Ofen damit.");

        // Assert
        Assert.Equal(["Ofengemüse mit Feta"], Titles(await SearchAsync(world, "Ofengemüse")));
        Assert.Equal(["Ofengemüse mit Feta"], Titles(await SearchAsync(world, "Feta")));
    }

    [Fact]
    public async Task Search_ShouldForgetTheOldTitle_WhenARecipeIsRenamed()
    {
        // Arrange
        var world = await SeedAsync();
        var recipeId = await world.SaveAsync("Pfannkuchen", "de", 10, 10,
            [("Mehl", null)], [], "Backen.");

        // Act
        await RenameAsync(world, recipeId, "Kaiserschmarrn");

        // Assert
        Assert.Empty(Titles(await SearchAsync(world, "Pfannkuchen")));
        Assert.Equal(["Kaiserschmarrn"], Titles(await SearchAsync(world, "Kaiserschmarrn")));
    }

    [Fact]
    public async Task Search_ShouldFindADessert_WhenAskedForNachtisch()
    {
        // Arrange
        // culina-v2-dku9: nothing about Waffeln says "Nachtisch"; the lexicon knows waffles are a dessert.
        var world = await SeedAsync();
        await world.SaveAsync("Waffeln", "de", 10, 15,
            [("Mehl", "g"), ("Eier", null), ("Milch", "ml"), ("Zucker", "g")], [], "Ausbacken.");

        // Act
        var titles = Titles(await SearchAsync(world, "Nachtisch"));

        // Assert
        Assert.Equal(["Waffeln"], titles);
    }

    [Fact]
    public async Task Search_ShouldRankAConceptMatch_BelowEveryRecipeThatSaysTheWord()
    {
        // Arrange
        var world = await SeedAsync();

        // Act
        var titles = Titles(await SearchAsync(world, "Hähnchen"));

        // Assert
        // Chicken Curry is found only by meaning, so it comes after the recipe that says the word.
        var said = titles.IndexOf("Hähnchenbrustfilet mit Reis");
        var meant = titles.IndexOf("Chicken Curry");

        Assert.True(said >= 0 && meant > said, string.Join(" · ", titles));
    }

    [Fact]
    public async Task Startup_ShouldRebuildTheConcepts_ThatAnotherLexiconIndexed()
    {
        // Arrange
        // Documents built by an older lexicon version, as after an upgrade.
        var world = await SeedAsync();
        await postgres.ExecuteAsync(
            "update recipe_search_documents set concepts = '{}', lexicon_version = 0;",
            Token);
        Assert.Empty(Titles(await SearchAsync(world, "Geflügel")));

        var reindex = postgres.Api.Services
            .GetServices<IHostedService>()
            .OfType<LexiconReindexService>()
            .Single();

        // Act
        await reindex.StartAsync(Token);

        // Assert
        Assert.Contains("Chicken Curry", Titles(await SearchAsync(world, "Geflügel")));

        var session = postgres.NewSession();
        await using var _ = session.ConfigureAwait(false);
        var stale = await new DbExecutor(session).ExecuteScalarAsync<long>(
            "select count(*) from recipe_search_documents where lexicon_version <> @version;",
            new { version = CulinaryLexicon.Version },
            Token);

        Assert.Equal(0, stale);
    }

    [Theory]
    [InlineData("Hähnchenbrust in Zitronensoße")]
    [InlineData("Müsli, Muesli & MUSLI")]
    [InlineData("Crème brûlée ÀÉÎÕÜ æ Æ ẞ")]
    [InlineData("100% Roggen_brot -- ohne Zucker!")]
    [InlineData("  Łódź 北京 Ñandú  ")]
    public async Task Fold_ShouldAgreeWithTheDatabase_CharacterForCharacter(string text)
    {
        // Arrange
        // The lexicon folds text in C# and the lanes in SQL; the two must agree.
        var session = postgres.NewSession();
        await using var _ = session.ConfigureAwait(false);
        var executor = new DbExecutor(session);

        // Act
        var ae = await executor.ExecuteScalarAsync<string>("select culina_fold_ae(@text);", new { text }, Token);
        var a = await executor.ExecuteScalarAsync<string>("select culina_fold_a(@text);", new { text }, Token);

        // Assert
        Assert.Equal(ae, SearchText.FoldAe(text));
        Assert.Equal(a, SearchText.FoldA(text));
    }

    [Fact]
    public async Task Search_ShouldNeverPresumeAMeatDish_Vegetarian()
    {
        // Arrange
        var world = await SeedAsync();

        // Act
        var vegetarian = Titles(await SearchAsync(world, "vegetarisch"));
        var withoutMeat = Titles(await SearchAsync(world, "Gericht ohne Fleisch"));

        // Assert
        // Zwiebelkuchen has Speck in it; untagged, but the ingredient is enough to exclude it.
        Assert.DoesNotContain("Zwiebelkuchen", vegetarian);
        Assert.DoesNotContain("Chicken Curry", vegetarian);
        // Presumed vegetarian, because nothing in it says otherwise.
        Assert.Contains("Müsliriegel", vegetarian);
        Assert.Equal(vegetarian.Order(StringComparer.Ordinal), withoutMeat.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Search_ShouldSayWhatItUnderstood_WithTheCharactersItReadEachFrom()
    {
        // Arrange
        var world = await SeedAsync();
        const string query = "vegetarisch unter 30 Minuten mit Kartoffeln";

        // Act
        var response = await SearchAsync(world, query);
        var interpretation = response.Json!.Value.GetProperty("interpretation");

        // Assert
        Assert.Equal(string.Empty, interpretation.GetProperty("freeText").GetString());
        Assert.Equal(
            ["diet:vegetarian:vegetarisch", "time:30:unter 30 Minuten", "ingredient:potato:mit Kartoffeln"],
            Chips(response));

        foreach (var chip in interpretation.GetProperty("applied").EnumerateArray())
        {
            var start = chip.GetProperty("start").GetInt32();
            var end = chip.GetProperty("end").GetInt32();

            Assert.Equal(chip.GetProperty("text").GetString(), query[start..end]);
        }
    }

    [Fact]
    public async Task Search_ShouldClaimNothing_WhenNothingWasInferred()
    {
        // Arrange
        var world = await SeedAsync();

        // Act
        var dish = await SearchAsync(world, "Nudeln mit Tomatensoße");
        var browse = await world.Client.GetAsync($"/api/v1/recipes?householdId={world.HouseholdId}", Token);

        // Assert
        // "mit" joins two foods in a dish's name here, so no chips are inferred.
        Assert.Empty(Chips(dish));
        // The words may be corrected to ones the library has, but that is a recovery, not a reading.
        var interpretation = dish.Json!.Value.GetProperty("interpretation");
        Assert.True(
            interpretation.GetProperty("freeText").GetString() == "Nudeln mit Tomatensoße"
            || interpretation.GetProperty("correctedFrom").GetString() == "Nudeln mit Tomatensoße");
        // A plain listing carries no interpretation.
        Assert.False(browse.Json!.Value.TryGetProperty("interpretation", out var none)
                     && none.ValueKind != System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task Search_ShouldCorrectAMisspelling_AgainstTheHouseholdsOwnWords()
    {
        // Arrange
        var world = await SeedAsync();

        // Act
        // Corrected against the household's own ingredients ("Kokosmilch").
        var corrected = await SearchAsync(world, "Kokosmlich");
        var asTyped = await world.Client.GetAsync(
            $"/api/v1/recipes?householdId={world.HouseholdId}&query=Kokosmlich&asTyped=true",
            Token);

        // Assert
        var interpretation = corrected.Json!.Value.GetProperty("interpretation");

        Assert.Contains("Süßkartoffelcurry", Titles(corrected));
        Assert.Equal("Kokosmlich", interpretation.GetProperty("correctedFrom").GetString());
        Assert.Equal("Kokosmilch", interpretation.GetProperty("freeText").GetString());
        // Turned down, the words are searched exactly as typed.
        Assert.Empty(Titles(asTyped));
    }

    [Fact]
    public async Task Search_ShouldSetAsideTheWeakestReading_AndSayWhich()
    {
        // Arrange
        var world = await SeedAsync();

        // Act
        // Nothing in the library is a dinner; the weaker meal reading is dropped and reported.
        var response = await SearchAsync(world, "schnelles Abendessen");

        // Assert
        Assert.NotEmpty(Titles(response));
        Assert.Equal(["meal:dinner:Abendessen"], Relaxed(response));
    }

    [Fact]
    public async Task Search_ShouldKeepAMealItSetAside_AsAPreference()
    {
        // Arrange
        // Both quick, neither a dinner; the yoghurt is breakfast, the soup is warm like a dinner.
        var world = await SeedAsync();
        await world.SaveAsync("Joghurt mit Honig", "de", 5, null,
            [("Joghurt", "g"), ("Honig", "EL")], ["frühstück"], "Verrühren.");
        await world.SaveAsync("Erbsensuppe", "de", 10, 15,
            [("Erbsen", "g"), ("Gemüsebrühe", "ml")], ["suppe"], "Pürieren.");

        // Act
        var titles = Titles(await SearchAsync(world, "schnelles Abendessen"));

        // Assert
        var soup = titles.IndexOf("Erbsensuppe");
        var breakfast = titles.IndexOf("Joghurt mit Honig");

        Assert.True(soup >= 0 && breakfast > soup, string.Join(" · ", titles));
    }

    [Fact]
    public async Task Search_ShouldAnswerADish_ByTheDishItIsAKindOf()
    {
        // Arrange
        // No goulash falls back to a stew; no risotto does not fall back to every rice dish.
        var world = await SeedAsync();
        await world.SaveAsync("Beef Stew", "en", 20, 150,
            [("beef", "g"), ("potatoes", "g"), ("red wine", "ml")], [], "Braise slowly.");

        // Act
        var goulash = await SearchAsync(world, "Gulasch");
        var risotto = Titles(await SearchAsync(world, "Risotto"));

        // Assert
        Assert.Equal(["Beef Stew"], Titles(goulash));
        Assert.Equal("concept:stew", Reasons(goulash)["Beef Stew"]);
        Assert.DoesNotContain("Hähnchenbrustfilet mit Reis", risotto);
    }

    [Fact]
    public async Task Search_ShouldPreferTheHouseholdsOwnTag_ToWhatTheLexiconInfers()
    {
        // Arrange
        // Both are summer dishes to the lexicon; only one is tagged so by the household.
        var world = await SeedAsync();
        await world.SaveAsync("Gurkensalat", "de", 10, null,
            [("Gurke", null), ("Dill", null)], [], "Hobeln.");
        await world.SaveAsync("Wassermelone mit Feta", "de", 10, null,
            [("Wassermelone", "g"), ("Feta", "g")], ["Sommer"], "Würfeln.");

        // Act
        var response = await SearchAsync(world, "Sommergericht");
        var titles = Titles(response);

        // Assert
        Assert.Equal(["Wassermelone mit Feta", "Gurkensalat"], titles);
        Assert.Equal("tag:Sommer", Reasons(response)["Wassermelone mit Feta"]);
        Assert.Equal("concept:Sommer", Reasons(response)["Gurkensalat"]);
    }

    [Fact]
    public async Task Search_ShouldSayWhichDietIsOnlyPresumed_AndTakeATagForAnAnswer()
    {
        // Arrange
        // Müsliriegel is only presumed vegetarian, Gemüselasagne is tagged, and the household
        // answered for Kichererbsen-Eintopf (chicken stock) with a "nicht vegetarisch" tag.
        var world = await SeedAsync();
        await world.SaveAsync("Kichererbsen-Eintopf", "de", 10, 30,
            [("Kichererbsen", "g"), ("Tomaten", "g")], ["nicht vegetarisch"], "Köcheln.");

        // Act
        var response = await SearchAsync(world, "vegetarisch");
        var presumed = response.Json!.Value.GetProperty("items").EnumerateArray().ToDictionary(
            item => item.GetProperty("title").GetString()!,
            item => item.TryGetProperty("presumedDiet", out var diet) ? diet.GetString() : null);

        // Assert
        Assert.Equal("vegetarian", presumed["Müsliriegel"]);
        Assert.Null(presumed["Gemüselasagne"]);
        Assert.DoesNotContain("Kichererbsen-Eintopf", presumed.Keys);
    }

    [Fact]
    public async Task Search_ShouldNeverSetADietAside()
    {
        // Arrange
        var world = await SeedAsync();

        // Act
        var quick = await SearchAsync(world, "vegan unter 10 Minuten");
        var chicken = await SearchAsync(world, "vegan Hähnchen");

        // Assert
        // The time goes; the diet never does.
        Assert.Equal(["time:10:unter 10 Minuten"], Relaxed(quick));
        Assert.Contains("Süßkartoffelcurry", Titles(quick));
        Assert.DoesNotContain("Kartoffelgratin", Titles(quick));
        Assert.DoesNotContain("Hähnchenbrustfilet mit Reis", Titles(quick));
        Assert.Empty(Titles(chicken));
        Assert.Empty(Relaxed(chicken));
    }

    [Fact]
    public async Task Search_ShouldNameAContradiction_RatherThanGuessWhichHalfToDrop()
    {
        // Arrange
        var world = await SeedAsync();

        // Act
        var response = await SearchAsync(world, "vegetarisch mit Lachs");

        // Assert
        var conflict = response.Json!.Value.GetProperty("interpretation").GetProperty("conflict")
            .EnumerateArray()
            .Select(chip => $"{chip.GetProperty("kind").GetString()}:{chip.GetProperty("value").GetString()}");

        Assert.Empty(Titles(response));
        Assert.Equal(["diet:vegetarian", "ingredient:salmon"], conflict);
    }

    [Fact]
    public async Task Search_ShouldSayWhyARecipeIsHere_WhenItsTitleDoesNot()
    {
        // Arrange
        var world = await SeedAsync();

        // Act
        var coconut = Reasons(await SearchAsync(world, "Kokosmilch"));
        var poultry = Reasons(await SearchAsync(world, "Geflügel"));
        var step = Reasons(await SearchAsync(world, "abgelöscht"));
        var title = Reasons(await SearchAsync(world, "Bolognese"));

        // Assert
        Assert.Equal("ingredient:Kokosmilch", coconut["Süßkartoffelcurry"]);
        // Found through the lexicon alone, named in the recipe's own language.
        Assert.Equal("concept:Geflügel", poultry["Hähnchenbrustfilet mit Reis"]);
        Assert.Equal("concept:poultry", poultry["Chicken Curry"]);
        Assert.Equal("text:", step["Spaghetti Bolognese"]);
        // A title match needs no reason.
        Assert.Null(title["Spaghetti Bolognese"]);
    }

    [Fact]
    public async Task Search_ShouldOfferRefinements_ThatSplitTheResults()
    {
        // Arrange
        var world = await SeedAsync();

        // Act
        var bolognese = FacetTags(await SearchAsync(world, "Bolognese"));
        var bechamel = FacetTags(await SearchAsync(world, "Béchamel"));

        // Assert
        Assert.Contains("italienisch", bolognese);
        // Both lasagnes are pasta, so that chip would remove nothing. Queried by sauce, not "Lasagne",
        // because "Lasagne" also finds the gratin.
        Assert.DoesNotContain("pasta", bechamel);
    }

    [Theory]
    [InlineData("häh")]
    [InlineData("haeh")]
    [InlineData("Hah")]
    public async Task Completions_ShouldOfferTheHouseholdsOwnWords_InEitherSpelling(string typed)
    {
        // Arrange
        var world = await SeedAsync();

        // Act
        var items = await CompletionsAsync(world, typed);

        // Assert
        Assert.Equal("recipe:Hähnchenbrustfilet mit Reis", items[0]);
        Assert.Contains("ingredient:Hähnchenbrust:1", items);
    }

    [Fact]
    public async Task Completions_ShouldCompleteOnlyTheWordsStillBeingTyped()
    {
        // Arrange
        var world = await SeedAsync();

        // Act
        var tags = await CompletionsAsync(world, "veg");
        var afterADiet = await CompletionsAsync(world, "vegetarisch Kar");
        var oneLetter = await CompletionsAsync(world, "h");

        // Assert
        Assert.Contains("tag:vegetarisch:3", tags);
        Assert.Contains("tag:vegan:1", tags);
        // The diet is already understood; only "Kar" is completed.
        Assert.Equal("recipe:Kartoffelgratin", afterADiet[0]);
        Assert.Empty(oneLetter);
    }

    [Fact]
    public async Task Completions_ShouldOfferARefinement_OnlyWhenItWouldSplitWhatIsFound()
    {
        // Arrange
        var world = await SeedAsync();

        // Act
        // Two recipes say "Zwiebel"; one of them takes half an hour.
        var items = await CompletionsAsync(world, "Zwie");

        // Assert
        Assert.Contains("refinement:Zwiebel:1:30", items);
    }

    [Fact]
    public async Task Completions_ShouldBeNeitherCachedNorOpenToAnotherHousehold()
    {
        // Arrange
        var world = await SeedAsync();

        // Act
        var own = await world.Client.GetAsync(
            $"/api/v1/households/{world.HouseholdId}/completions?query=Bol", Token);
        var foreign = await world.Client.GetAsync(
            $"/api/v1/households/{Guid.NewGuid()}/completions?query=Bol", Token);

        // Assert
        Assert.Contains("no-store", own.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.Ordinal);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, foreign.StatusCode);
    }

    /// <summary>
    /// Completion latency budget. Explicit because it is a measurement; run after changing the completion queries.
    /// </summary>
    [Fact(Explicit = true)]
    public async Task Completions_ShouldAnswerWithinFortyMilliseconds_OverTwoThousandRecipes()
    {
        // Arrange
        var world = await SeedAsync();
        await postgres.ExecuteAsync(
            $$"""
            with words(title_word) as (
                select unnest(array['Hähnchen', 'Kartoffel', 'Tomaten', 'Linsen', 'Kürbis', 'Lachs', 'Rinder',
                                    'Gemüse', 'Spinat', 'Pilz', 'Paprika', 'Zucchini', 'Käse', 'Bohnen']) ),
            dishes(dish) as (
                select unnest(array['Curry', 'Suppe', 'Auflauf', 'Pfanne', 'Salat', 'Eintopf', 'Gratin',
                                    'Risotto', 'Lasagne', 'Bowl']) ),
            pool(name, n) as (
                select name, row_number() over () from unnest(array[
                    'Zwiebel', 'Knoblauch', 'Olivenöl', 'Salz', 'Pfeffer', 'Butter', 'Sahne', 'Milch', 'Mehl',
                    'Eier', 'Reis', 'Nudeln', 'Hähnchenbrust', 'Hackfleisch', 'Speck', 'Kartoffeln', 'Karotten',
                    'Sellerie', 'Lauch', 'Tomaten', 'Tomatenmark', 'Paprika', 'Chili', 'Ingwer', 'Kokosmilch',
                    'Linsen', 'Kichererbsen', 'Spinat', 'Feta', 'Parmesan', 'Mozzarella', 'Zitrone', 'Limette',
                    'Petersilie', 'Basilikum', 'Koriander', 'Brühe', 'Weißwein', 'Honig', 'Senf']) as name),
            made as (
                insert into recipes (id, household_id, title, language, yield_amount, yield_kind,
                                     prep_minutes, cook_minutes, created_by, created_at, updated_at)
                select gen_random_uuid(), '{{world.HouseholdId}}',
                       w.title_word || d.dish || ' ' || i, 'de', 4, 'servings',
                       10 + (i % 4) * 5, 10 + (i % 7) * 10,
                       (select created_by from recipes where household_id = '{{world.HouseholdId}}' limit 1),
                       now(), now()
                from generate_series(1, 2000) as i
                cross join lateral (select title_word from words offset (i % 14) limit 1) w
                cross join lateral (select dish from dishes offset (i % 10) limit 1) d
                returning id),
            grouped as (
                insert into ingredient_groups (id, recipe_id, sort_order)
                select gen_random_uuid(), id, 0 from made
                returning id, recipe_id)
            insert into recipe_ingredients (id, group_id, sort_order, name)
            select gen_random_uuid(), g.id, k, p.name
            from grouped g
            cross join generate_series(0, 7) as k
            join pool p on p.n = 1 + (abs(hashtext(g.recipe_id::text || k)) % 40);

            insert into recipe_search_documents (
                recipe_id, household_id, language, document, fuzzy_text,
                title_ae, title_a, ingredient_count, analyzer_version)
            select recipe_id, household_id, language, document, fuzzy_text,
                   title_ae, title_a, ingredient_count, analyzer_version
            from recipe_search_input
            on conflict (recipe_id) do nothing;

            analyze;
            """,
            Token);

        string[] prefixes = ["hä", "häh", "kar", "kart", "tom", "lin", "kür", "lac", "zwi", "koko", "pil", "boh"];
        var timings = new List<double>();

        // Act
        foreach (var round in Enumerable.Range(0, 5))
        {
            foreach (var prefix in prefixes)
            {
                var clock = System.Diagnostics.Stopwatch.StartNew();
                await CompletionsAsync(world, prefix);
                timings.Add(clock.Elapsed.TotalMilliseconds);
            }
        }

        // Assert
        // The first round warms the connection and plans.
        var steady = timings.Skip(prefixes.Length).Order().ToList();
        var p95 = steady[(int)Math.Ceiling(steady.Count * 0.95) - 1];

        TestContext.Current.SendDiagnosticMessage($"completions p95 {p95:F1} ms, median {steady[steady.Count / 2]:F1} ms");
        Assert.True(p95 <= 40, $"p95 was {p95:F1} ms");
    }

    [Fact]
    public async Task Search_ShouldTreatALikeMetacharacterAsInert()
    {
        // Arrange
        var world = await SeedAsync();

        // Act
        // The lanes build LIKE patterns by concatenation; the fold strips metacharacters
        // instead of escaping them at each call site.
        var plain = Titles(await SearchAsync(world, "Bolognese"));
        var withPercent = Titles(await SearchAsync(world, "Bolognese%"));
        var withUnderscore = Titles(await SearchAsync(world, "Bolo_nese"));

        // Assert
        Assert.NotEmpty(plain);
        Assert.Equal(plain, withPercent);
        // The underscore is a separator, never a single-character wildcard.
        Assert.True(withUnderscore.Count <= plain.Count);
        Assert.DoesNotContain("Gemüselasagne", withUnderscore, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Search_ShouldReadAContentlessQuery_AsNoQueryAtAll()
    {
        // Arrange
        var world = await SeedAsync();

        // Act
        // "%" and "..." fold to nothing and must behave exactly like an empty search box.
        var everything = Titles(await SearchAsync(world, string.Empty));
        var punctuation = Titles(await SearchAsync(world, "%"));
        var dots = Titles(await SearchAsync(world, "..."));

        // Assert
        Assert.NotEmpty(everything);
        Assert.Equal(everything, punctuation);
        Assert.Equal(everything, dots);
    }

    [Fact]
    public async Task Search_ShouldLeaveOutARecipe_WithAnExcludedWord()
    {
        // Arrange
        var world = await SeedAsync();
        await world.SaveAsync("Tomaten mit Reis", "de", 10, 20,
            [("Tomaten", null), ("Reis", null)], [], "Kochen.");
        await world.SaveAsync("Tomaten mit Nudeln", "de", 10, 20,
            [("Tomaten", null), ("Nudeln", null)], [], "Kochen.");

        // Act
        var response = await SearchAsync(world, "Tomaten -Reis");
        var titles = Titles(response);

        // Assert
        // The minus is an exclusion in every lane, reported as an undoable chip.
        Assert.Contains("Tomaten mit Nudeln", titles);
        Assert.DoesNotContain("Tomaten mit Reis", titles);
        Assert.DoesNotContain("Hähnchenbrustfilet mit Reis", titles);
        Assert.Equal(["exclusion:rice:-Reis"], Chips(response));
    }

    [Fact]
    public async Task Search_ShouldKeepPaging_StableAcrossRelevance()
    {
        // Arrange
        var world = await SeedAsync();

        // Act
        var first = await world.Client.GetAsync(
            $"/api/v1/recipes?householdId={world.HouseholdId}&query=Tomaten&limit=2",
            Token);
        var cursor = first.Json!.Value.GetProperty("nextCursor").GetString();
        var second = await world.Client.GetAsync(
            $"/api/v1/recipes?householdId={world.HouseholdId}&query=Tomaten&limit=2&cursor={Uri.EscapeDataString(cursor!)}",
            Token);

        // Assert
        // The relevance cursor carries tier and score, so page two resumes rather than re-ranks.
        var page1 = Titles(first);
        var page2 = Titles(second);

        Assert.Equal(2, page1.Count);
        Assert.NotEmpty(page2);
        Assert.Empty(page1.Intersect(page2, StringComparer.Ordinal));
    }

    [Fact]
    public async Task Indexing_ShouldProduceADocument_ForEveryRecipeInTheLibrary()
    {
        // Arrange
        var world = await SeedAsync();

        var session = postgres.NewSession();
        await using var _ = session.ConfigureAwait(false);
        var executor = new DbExecutor(session);

        // Act
        // recipe_search_input is the single definition of what is searchable; if it yielded nothing
        // for some recipe shape, an upgrade would leave that recipe unfindable.
        var recipes = await executor.ExecuteScalarAsync<long>(
            "select count(*) from recipes;", null, Token);

        var documents = await executor.ExecuteScalarAsync<long>(
            "select count(*) from recipe_search_documents;", null, Token);

        var buildable = await executor.ExecuteScalarAsync<long>(
            """
            select count(*) from recipe_search_input
            where document is not null and fuzzy_text <> '' and title_ae <> '';
            """,
            null,
            Token);

        var current = await executor.ExecuteScalarAsync<long>(
            """
            select count(*) from recipe_search_documents
            where analyzer_version = culina_search_analyzer_version();
            """,
            null,
            Token);

        // Assert
        Assert.Equal(11, recipes);
        Assert.Equal(recipes, documents);
        Assert.Equal(recipes, buildable);
        Assert.Equal(recipes, current);
    }

    [Fact]
    public async Task Search_ShouldStillListARecipe_WhenItsDocumentIsMissing()
    {
        // Arrange
        var world = await SeedAsync();

        await postgres.ExecuteAsync(
            """
            delete from recipe_search_documents
            where recipe_id in (select id from recipes where title = 'Zwiebelkuchen');
            """,
            Token);

        // Act
        var all = Titles(await SearchAsync(world, string.Empty));
        var byWord = Titles(await SearchAsync(world, "Zwiebelkuchen"));

        // Assert
        // A missing document costs the recipe its words, never its place in the library (left join).
        Assert.Contains("Zwiebelkuchen", all, StringComparer.Ordinal);
        Assert.DoesNotContain("Zwiebelkuchen", byWord, StringComparer.Ordinal);
    }

    private static string? Check(Golden golden, List<string> titles)
    {
        if (golden.Count is { } expected && titles.Count != expected)
        {
            return $"expected {expected} results, got {titles.Count}";
        }

        if (golden.Top is { } top && !titles.Take(top.Length).SequenceEqual(top, StringComparer.Ordinal))
        {
            return $"expected to lead with {string.Join(" · ", top)}";
        }

        if (golden.TopSet is { } set
            && !titles.Take(set.Length).OrderBy(one => one, StringComparer.Ordinal)
                .SequenceEqual(set.OrderBy(one => one, StringComparer.Ordinal), StringComparer.Ordinal))
        {
            return $"expected the first {set.Length} to be {string.Join(" · ", set)}";
        }

        if (golden.Contains?.FirstOrDefault(one => !titles.Contains(one, StringComparer.Ordinal)) is { } missing)
        {
            return $"“{missing}” is missing";
        }

        if (golden.Excludes?.FirstOrDefault(one => titles.Contains(one, StringComparer.Ordinal)) is { } unwanted)
        {
            return $"“{unwanted}” should not be here";
        }

        var top3 = titles.Take(3).ToList();

        return golden.NotInTop?.FirstOrDefault(one => top3.Contains(one, StringComparer.Ordinal)) is { } intruder
            ? $"“{intruder}” should not be in the first three"
            : null;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Task<ApiResponse> SearchAsync(Kitchen world, string query) =>
        world.Client.GetAsync(
            $"/api/v1/recipes?householdId={world.HouseholdId}&query={Uri.EscapeDataString(query)}",
            Token);

    private static List<string> Chips(ApiResponse response) =>
        response.Json!.Value.TryGetProperty("interpretation", out var interpretation)
        && interpretation.ValueKind == System.Text.Json.JsonValueKind.Object
            ? [.. interpretation.GetProperty("applied").EnumerateArray()
                .Select(chip => $"{chip.GetProperty("kind").GetString()}:{chip.GetProperty("value").GetString()}:"
                                + chip.GetProperty("text").GetString())]
            : [];

    private static List<string> Relaxed(ApiResponse response) =>
        response.Json!.Value.GetProperty("interpretation").TryGetProperty("relaxed", out var relaxed)
        && relaxed.ValueKind == System.Text.Json.JsonValueKind.Array
            ? [.. relaxed.EnumerateArray()
                .Select(chip => $"{chip.GetProperty("kind").GetString()}:{chip.GetProperty("value").GetString()}:"
                                + chip.GetProperty("text").GetString())]
            : [];

    private static Dictionary<string, string?> Reasons(ApiResponse response) =>
        response.Json!.Value.GetProperty("items").EnumerateArray().ToDictionary(
            item => item.GetProperty("title").GetString()!,
            item => item.TryGetProperty("matchReason", out var reason)
                    && reason.ValueKind == System.Text.Json.JsonValueKind.Object
                ? $"{reason.GetProperty("kind").GetString()}:"
                  + (reason.TryGetProperty("term", out var term) ? term.GetString() : null)
                : null);

    private static List<string> FacetTags(ApiResponse response) =>
        response.Json!.Value.TryGetProperty("facets", out var facets)
        && facets.ValueKind == System.Text.Json.JsonValueKind.Object
            ? [.. facets.GetProperty("tags").EnumerateArray().Select(tag => tag.GetProperty("value").GetString()!)]
            : [];

    private static async Task<List<string>> CompletionsAsync(Kitchen world, string typed)
    {
        var response = await world.Client.GetAsync(
            $"/api/v1/households/{world.HouseholdId}/completions?query={Uri.EscapeDataString(typed)}",
            Token);

        return [.. response.Json!.Value.GetProperty("items").EnumerateArray().Select(item =>
        {
            var kind = item.GetProperty("kind").GetString();
            var label = item.GetProperty("label").GetString();

            return kind switch
            {
                "recipe" => $"recipe:{label}",
                "refinement" => $"refinement:{label}:{item.GetProperty("recipeCount").GetInt32()}:"
                                + item.GetProperty("maxMinutes").GetInt32(),
                _ => $"{kind}:{label}:{item.GetProperty("recipeCount").GetInt32()}"
            };
        })];
    }

    private static List<string> Titles(ApiResponse response) =>
        [.. response.Json!.Value.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("title").GetString()!)];

    private async Task<Kitchen> SeedAsync()
    {
        var world = await Kitchen.OpenAsync(postgres);

        await world.SaveAsync("Spaghetti Bolognese", "de", 15, 30,
            [("Hackfleisch", "g"), ("passierte Tomaten", "ml"), ("Zwiebel", null), ("Spaghetti", "g")],
            ["pasta", "italienisch"],
            "Hackfleisch anbraten und mit Rotwein abgelöscht köcheln lassen.");

        await world.SaveAsync("Lasagne Bolognese", "de", 30, 60,
            [("Hackfleisch", "g"), ("Tomaten", "g"), ("Lasagneplatten", null), ("Béchamel", "ml")],
            ["pasta", "italienisch", "ofen"],
            "Schichten und backen.");

        await world.SaveAsync("Bolognese-Sauce auf Vorrat", "de", 20, 100,
            [("Hackfleisch", "g"), ("Tomaten", "g")],
            ["sauce"],
            "Lange köcheln lassen.");

        await world.SaveAsync("Gemüselasagne", "de", 25, 45,
            [("Zucchini", null), ("Tomaten", "g"), ("Béchamel", "ml")],
            ["pasta", "vegetarisch"],
            "Schichten und backen.");

        await world.SaveAsync("Hähnchenbrustfilet mit Reis", "de", 10, 20,
            [("Hähnchenbrust", "g"), ("Reis", "g"), ("Zitrone", null)],
            ["schnell"],
            "Braten und servieren.");

        await world.SaveAsync("Kartoffelgratin", "de", 20, 40,
            [("Kartoffeln", "g"), ("Sahne", "ml"), ("Käse", "g")],
            ["auflauf", "vegetarisch"],
            "In die Form und in den Ofen.");

        await world.SaveAsync("Süßkartoffelcurry", "de", 15, 20,
            [("Süßkartoffel", "g"), ("Kokosmilch", "ml"), ("Ingwer", null)],
            ["vegan"],
            "Alles köcheln lassen.");

        await world.SaveAsync("Müsliriegel", "de", 15, 10,
            [("Haferflocken", "g"), ("Honig", "g")],
            ["snack"],
            "Pressen und backen.");

        // Singular on purpose: the typed query is "Tomaten".
        await world.SaveAsync("Tomatensuppe", "de", 10, 20,
            [("Tomate", null), ("Zwiebel", null), ("Brühe", "ml")],
            ["vegetarisch", "suppe"],
            "Pürieren.");

        await world.SaveAsync("Zwiebelkuchen", "de", 30, 60,
            [("Zwiebeln", "g"), ("Speck", "g"), ("Hefeteig", null)],
            ["ofen"],
            "Belegen und backen.");

        // English in a German library: households write both.
        await world.SaveAsync("Chicken Curry", "en", 15, 25,
            [("chicken breast", "g"), ("coconut milk", "ml")],
            ["asian"],
            "Simmer until done.");

        return world;
    }

    private static async Task RenameAsync(Kitchen world, Guid recipeId, string title)
    {
        var read = await world.Client.GetAsync($"/api/v1/recipes/{recipeId}", Token);
        var body = read.Json!.Value;

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/recipes/{recipeId}")
        {
            Content = JsonContent.Create(new
            {
                title,
                language = "de",
                yieldAmount = body.GetProperty("yieldAmount").GetDecimal(),
                yieldKind = body.GetProperty("yieldKind").GetString(),
                groups = new[]
                {
                    new
                    {
                        name = (string?)null,
                        ingredients = new[] { new { name = "Mehl", unit = (string?)null } }
                    }
                },
                steps = new[]
                {
                    new { segments = new[] { new { type = "text", value = "Backen." } } }
                },
                tags = Array.Empty<string>()
            })
        };

        request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(read.ETag!));

        await world.Client.SendAsync(request, Token);
    }
}
