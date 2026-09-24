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
/// What search is supposed to find, stated as queries somebody would type.
/// </summary>
/// <remarks>
/// <para>
/// A curated relevance set rather than a learned one. Eight users produce a few
/// hundred noisy clicks a month, most of them for the same twenty recipes and
/// all of them confounded by position bias; forty queries whose answers a
/// person wrote down are a better instrument, because they fail by name and
/// they fail in CI.
/// </para>
/// <para>
/// The library below is deliberately awkward. It holds three recipes that all
/// answer to "Bolognese", compounds that no stemmer will ever split, a recipe
/// that says <em>Tomate</em> where the query says <em>Tomaten</em>, and one
/// title carrying an umlaut that people spell three different ways. Every one
/// of those is a query the old <c>ilike '%q%'</c> search returned nothing for.
/// </para>
/// </remarks>
[Collection(RequiresDatabase.Name)]
public class RecipeSearchRelevanceTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    /// <summary>
    /// One case: a query, and what a person said it should find.
    /// </summary>
    /// <param name="Query">What is typed.</param>
    /// <param name="Class">Which kind of query this is, for the report.</param>
    /// <param name="Top">Titles that must be the first results, in this order.</param>
    /// <param name="TopSet">Titles that must be the first results, in any order.</param>
    /// <param name="Contains">Titles that must appear somewhere.</param>
    /// <param name="Excludes">Titles that must not appear at all.</param>
    /// <param name="NotInTop">Titles that must not be among the first three.</param>
    /// <param name="Count">The exact number of results, when that is the point.</param>
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
        // ── Known item, exact and partial ───────────────────────────────────
        new("Spaghetti Bolognese", "known-item exact", Top: ["Spaghetti Bolognese"]),
        new("Kartoffelgratin", "known-item exact", Top: ["Kartoffelgratin"]),
        new("spaghetti bolognese", "known-item exact", Top: ["Spaghetti Bolognese"]),
        new("Bolognese", "known-item partial",
            TopSet: ["Spaghetti Bolognese", "Lasagne Bolognese"],
            Contains: ["Bolognese-Sauce auf Vorrat"],
            NotInTop: ["Gemüselasagne"]),
        new("Curry", "known-item partial",
            Contains: ["Süßkartoffelcurry", "Chicken Curry"]),

        // ── Misspelled: a genuine typo, and the same word spelt differently ──
        new("Bolgnese", "typo", Contains: ["Spaghetti Bolognese", "Lasagne Bolognese"]),
        new("Bolognäse", "typo", Contains: ["Spaghetti Bolognese", "Lasagne Bolognese"]),
        new("Kartoffelgratn", "typo", Contains: ["Kartoffelgratin"]),

        // ── The same word, written the three ways German writes it ──────────
        new("Müsliriegel", "spelling variant", Top: ["Müsliriegel"]),
        new("Muesliriegel", "spelling variant", Top: ["Müsliriegel"]),
        new("Musliriegel", "spelling variant", Top: ["Müsliriegel"]),
        new("Süßkartoffelcurry", "spelling variant", Top: ["Süßkartoffelcurry"]),
        new("Suesskartoffelcurry", "spelling variant", Top: ["Süßkartoffelcurry"]),

        // ── Morphology: what the stemmer is for, in both directions ─────────
        new("Tomaten", "morphology", Top: ["Tomatensuppe"]),
        // Tomatensuppe's ingredient list says "Tomate", singular. The old
        // search could only find a substring, so the plural found nothing.
        new("Tomate", "morphology", Contains: ["Tomatensuppe", "Spaghetti Bolognese"]),
        new("Zwiebeln", "morphology", Contains: ["Zwiebelkuchen", "Tomatensuppe"]),

        // ── Compounds: what the stemmer will never do, and trigram does ─────
        new("Hähnchen", "compound", Contains: ["Hähnchenbrustfilet mit Reis"]),
        new("Haehnchen", "compound", Contains: ["Hähnchenbrustfilet mit Reis"]),
        new("Hahnchen", "compound", Contains: ["Hähnchenbrustfilet mit Reis"]),
        new("Kartoffel", "compound", Contains: ["Kartoffelgratin", "Süßkartoffelcurry"]),
        new("Lasagne", "compound",
            Top: ["Lasagne Bolognese"],
            Contains: ["Gemüselasagne"]),
        new("Müsli", "compound", Contains: ["Müsliriegel"]),

        // ── Where a word is decides how much it counts ──────────────────────
        // Zwiebelkuchen is named after it; Tomatensuppe merely contains one.
        new("Zwiebel", "field weighting",
            Top: ["Zwiebelkuchen"],
            Contains: ["Tomatensuppe"]),
        new("Reis", "field weighting", Top: ["Hähnchenbrustfilet mit Reis"]),
        new("Sauce", "field weighting", Top: ["Bolognese-Sauce auf Vorrat"]),

        // ── Ingredients and tags are searched, and rank below titles ────────
        new("Hackfleisch", "ingredient",
            Contains: ["Spaghetti Bolognese", "Lasagne Bolognese", "Bolognese-Sauce auf Vorrat"]),
        new("Kokosmilch", "ingredient", Contains: ["Süßkartoffelcurry"]),
        new("vegetarisch", "tag",
            Contains: ["Gemüselasagne", "Kartoffelgratin", "Tomatensuppe"],
            Excludes: ["Spaghetti Bolognese"]),
        new("italienisch", "tag",
            Contains: ["Spaghetti Bolognese", "Lasagne Bolognese"]),

        // ── What a recipe is, not only what it says: the lexicon ────────────
        // Chicken Curry is written in English and never says Hähnchen; the
        // German recipe never says chicken. One concept, both languages.
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

        // ── Understood: a diet, a time, what to use and what to leave out ──
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
        // Contradictory, and nothing is invented to paper over it.
        new("vegetarisch mit Lachs", "conflict", Count: 0),

        // ── Steps are searched, which they never used to be ─────────────────
        new("abgelöscht", "step text", Contains: ["Spaghetti Bolognese"]),

        // ── Nothing matches, and nothing is invented ────────────────────────
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

    /// <summary>
    /// The invariant that makes the ranking explainable: an exact title is
    /// never outranked by something that merely resembles the query.
    /// </summary>
    /// <remarks>
    /// Asserted over every query in the set rather than case by case, because
    /// it is a property of the ordering and not of any one query. It is what
    /// stops a future weight change from quietly letting three weak signals
    /// outvote one strong one.
    /// </remarks>
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
        // No sort parameter at all. Asking a question is asking to be answered
        // best first; before this, a search fell back to "most recently edited"
        // and typing "Bolognese" returned whichever one had last been touched.
        var titles = Titles(await SearchAsync(world, "Bolognese"));

        // Assert
        Assert.Equal(3, titles.Count);
        Assert.Equal("Bolognese-Sauce auf Vorrat", titles[^1]);
    }

    [Fact]
    public async Task Search_ShouldFindARecipe_InTheSameBreathAsSavingIt()
    {
        // Arrange
        // The search document is written inside the recipe's own transaction,
        // so there is no window in which a recipe exists and cannot be found.
        // An index that lags its source is an index that is occasionally wrong
        // with nothing to say so.
        var world = await SeedAsync();

        // Act
        await SaveAsync(world, "Ofengemüse mit Feta", "de", 15, 30,
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
        var recipeId = await SaveAsync(world, "Pfannkuchen", "de", 10, 10,
            [("Mehl", null)], [], "Backen.");

        // Act
        await RenameAsync(world, recipeId, "Kaiserschmarrn");

        // Assert
        // A document that only ever grew would keep answering to a name the
        // recipe no longer has, which is the failure mode of every index that
        // is appended to rather than replaced.
        Assert.Empty(Titles(await SearchAsync(world, "Pfannkuchen")));
        Assert.Equal(["Kaiserschmarrn"], Titles(await SearchAsync(world, "Kaiserschmarrn")));
    }

    [Fact]
    public async Task Search_ShouldFindADessert_WhenAskedForNachtisch()
    {
        // Arrange
        // What a household reported (culina-v2-dku9): nothing about Waffeln
        // says "Nachtisch", and the search found nothing. The lexicon knows
        // waffles are a dessert.
        var world = await SeedAsync();
        await SaveAsync(world, "Waffeln", "de", 10, 15,
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
        // Chicken Curry is found only by what "Hähnchen" means, and so comes
        // after the recipe that actually says it. The lexicon's guess is never
        // allowed to outrank a fact about the text.
        var said = titles.IndexOf("Hähnchenbrustfilet mit Reis");
        var meant = titles.IndexOf("Chicken Curry");

        Assert.True(said >= 0 && meant > said, string.Join(" · ", titles));
    }

    [Fact]
    public async Task Startup_ShouldRebuildTheConcepts_ThatAnotherLexiconIndexed()
    {
        // Arrange
        // As a container that has just been upgraded to a new lexicon finds
        // its documents: built by a version it no longer is.
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
        // The lexicon reads text in C# and the lanes read it in SQL; a letter
        // they fold differently is a word the two halves disagree about.
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
        // Zwiebelkuchen has Speck in it. Nobody tagged it either way, and the
        // ingredient says enough: a vegetarian shown bacon has been failed in
        // a way a missing result never fails them.
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
        // "mit" joins two foods in a dish's name here. Showing no chips is as
        // much the design as showing four: a parser that invents a reading of
        // every query teaches people to distrust the readings it means.
        Assert.Empty(Chips(dish));
        Assert.Equal(
            "Nudeln mit Tomatensoße",
            dish.Json!.Value.GetProperty("interpretation").GetProperty("freeText").GetString());
        // And the plain library listing is exactly what it was.
        Assert.False(browse.Json!.Value.TryGetProperty("interpretation", out var none)
                     && none.ValueKind != System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task Search_ShouldCorrectAMisspelling_AgainstTheHouseholdsOwnWords()
    {
        // Arrange
        var world = await SeedAsync();

        // Act
        // Nothing is called "Kokosmlich". The household's own ingredients say
        // Kokosmilch, which is a better dictionary than any word list.
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
        // Nothing in the library is a dinner. The meal is the weaker guess, so
        // it goes, and the response says it went.
        var response = await SearchAsync(world, "schnelles Abendessen");

        // Assert
        Assert.NotEmpty(Titles(response));
        Assert.Equal(["meal:dinner:Abendessen"], Relaxed(response));
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
        // Nothing vegan is chicken, and nothing is invented to say otherwise.
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
        // Through the lexicon alone, named in the recipe's own language.
        Assert.Equal("concept:Geflügel", poultry["Hähnchenbrustfilet mit Reis"]);
        Assert.Equal("concept:poultry", poultry["Chicken Curry"]);
        Assert.Equal("text:", step["Spaghetti Bolognese"]);
        // "It is called that" is not worth a line.
        Assert.Null(title["Spaghetti Bolognese"]);
    }

    [Fact]
    public async Task Search_ShouldOfferRefinements_ThatSplitTheResults()
    {
        // Arrange
        var world = await SeedAsync();

        // Act
        var bolognese = FacetTags(await SearchAsync(world, "Bolognese"));
        var lasagne = FacetTags(await SearchAsync(world, "Lasagne"));

        // Assert
        // Two of the three Bolognese are Italian: a chip worth a tap.
        Assert.Contains("italienisch", bolognese);
        // Both lasagnes are pasta: a chip that removes nothing is not offered.
        Assert.DoesNotContain("pasta", lasagne);
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
        // A recipe to go to comes first, then what to filter by.
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
        // The diet has been understood; "Kar" has not.
        Assert.Equal("recipe:Kartoffelgratin", afterADiet[0]);
        // One letter is a prefix of half the library.
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
    /// The budget a completion has: it is asked on every pause in typing, so
    /// it has to be back before the next keystroke is.
    /// </summary>
    /// <remarks>
    /// Explicit, because seeding two thousand recipes is a measurement rather
    /// than a check. Run it after changing the completion queries:
    /// <c>dotnet test --filter-method *TwoThousandRecipes*</c> with the
    /// explicit tests included.
    /// </remarks>
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
        // The first round warms the connection and the plans; what is measured
        // is what somebody typing meets.
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
        // The lanes build LIKE patterns by concatenation, so a query carrying a
        // metacharacter would otherwise be a wildcard nobody asked for. They are
        // removed by the fold that every string already passes through, rather
        // than escaped at each call site, which is the version that gets
        // forgotten once and is then a way to read the whole library.
        var plain = Titles(await SearchAsync(world, "Bolognese"));
        var withPercent = Titles(await SearchAsync(world, "Bolognese%"));
        var withUnderscore = Titles(await SearchAsync(world, "Bolo_nese"));

        // Assert
        Assert.NotEmpty(plain);
        Assert.Equal(plain, withPercent);
        // "Bolo_nese" folds to two words and finds less, never more: the
        // underscore is a separator, never a single-character wildcard.
        Assert.True(withUnderscore.Count <= plain.Count);
        Assert.DoesNotContain("Gemüselasagne", withUnderscore, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Search_ShouldReadAContentlessQuery_AsNoQueryAtAll()
    {
        // Arrange
        var world = await SeedAsync();

        // Act
        // "%" and "..." carry nothing to search for once folded, and an empty
        // string is a prefix of every title. Rather than let that fall out of
        // the LIKE by accident — or return nothing, which would be a different
        // answer to the same non-question — a query with no content behaves
        // exactly like an empty search box.
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
        await SaveAsync(world, "Tomaten mit Reis", "de", 10, 20,
            [("Tomaten", null), ("Reis", null)], [], "Kochen.");
        await SaveAsync(world, "Tomaten mit Nudeln", "de", 10, 20,
            [("Tomaten", null), ("Nudeln", null)], [], "Kochen.");

        // Act
        var response = await SearchAsync(world, "Tomaten -Reis");
        var titles = Titles(response);

        // Assert
        // The minus used to be a full-text operator, which only the full-text
        // lane understood: the substring lane still found the rice dish by
        // "Tomaten", so it was demoted rather than removed. Read as an
        // exclusion it is removed, and says so as a chip that can be undone.
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
        // The relevance cursor carries the tier and the score, so the second
        // page resumes exactly where the first ended rather than re-ranking.
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
        // recipe_search_input is the one definition of what is searchable about
        // a recipe: the per-recipe write, the migration's backfill and every
        // future re-index are the same INSERT over it. If it produced nothing
        // for some shape of recipe, an upgrade would leave that recipe
        // permanently unfindable and nothing would have said so.
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
        // A document that has somehow gone missing costs the recipe its words,
        // never its place in the library. The join is a left join for exactly
        // this: a recipe vanishing from the collection is a far worse failure
        // than one that cannot be found by typing, and "this cannot happen" is
        // a poor reason to let it.
        //
        // The other onion recipes still answer, which is the fuzzy lane doing
        // its job — and they answer from the bottom, because a recipe reached
        // only by resemblance is two tiers below one the query names.
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

    private static Task<ApiResponse> SearchAsync(World world, string query) =>
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

    private static async Task<List<string>> CompletionsAsync(World world, string typed)
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

    private sealed record World(ApiClient Client, Guid HouseholdId);

    private async Task<World> SeedAsync()
    {
        await postgres.ResetAsync(Token);

        var client = postgres.Api.NewApiClient();
        await client.PostAsync(
            "/api/v1/users",
            new { email = "ada@example.com", displayName = "Ada", password = Password },
            Token);
        await client.PostAsync(
            "/api/v1/sessions",
            new { email = "ada@example.com", password = Password },
            Token);

        var householdId = (await client.GetAsync("/api/v1/households", Token))
            .Json!.Value.GetProperty("items")[0].GetProperty("householdId").GetGuid();

        var world = new World(client, householdId);

        await SaveAsync(world, "Spaghetti Bolognese", "de", 15, 30,
            [("Hackfleisch", "g"), ("passierte Tomaten", "ml"), ("Zwiebel", null), ("Spaghetti", "g")],
            ["pasta", "italienisch"],
            "Hackfleisch anbraten und mit Rotwein abgelöscht köcheln lassen.");

        await SaveAsync(world, "Lasagne Bolognese", "de", 30, 60,
            [("Hackfleisch", "g"), ("Tomaten", "g"), ("Lasagneplatten", null), ("Béchamel", "ml")],
            ["pasta", "italienisch", "ofen"],
            "Schichten und backen.");

        await SaveAsync(world, "Bolognese-Sauce auf Vorrat", "de", 20, 100,
            [("Hackfleisch", "g"), ("Tomaten", "g")],
            ["sauce"],
            "Lange köcheln lassen.");

        await SaveAsync(world, "Gemüselasagne", "de", 25, 45,
            [("Zucchini", null), ("Tomaten", "g"), ("Béchamel", "ml")],
            ["pasta", "vegetarisch"],
            "Schichten und backen.");

        await SaveAsync(world, "Hähnchenbrustfilet mit Reis", "de", 10, 20,
            [("Hähnchenbrust", "g"), ("Reis", "g"), ("Zitrone", null)],
            ["schnell"],
            "Braten und servieren.");

        await SaveAsync(world, "Kartoffelgratin", "de", 20, 40,
            [("Kartoffeln", "g"), ("Sahne", "ml"), ("Käse", "g")],
            ["auflauf", "vegetarisch"],
            "In die Form und in den Ofen.");

        await SaveAsync(world, "Süßkartoffelcurry", "de", 15, 20,
            [("Süßkartoffel", "g"), ("Kokosmilch", "ml"), ("Ingwer", null)],
            ["vegan"],
            "Alles köcheln lassen.");

        await SaveAsync(world, "Müsliriegel", "de", 15, 10,
            [("Haferflocken", "g"), ("Honig", "g")],
            ["snack"],
            "Pressen und backen.");

        // Singular, on purpose: the query people type is "Tomaten".
        await SaveAsync(world, "Tomatensuppe", "de", 10, 20,
            [("Tomate", null), ("Zwiebel", null), ("Brühe", "ml")],
            ["vegetarisch", "suppe"],
            "Pürieren.");

        await SaveAsync(world, "Zwiebelkuchen", "de", 30, 60,
            [("Zwiebeln", "g"), ("Speck", "g"), ("Hefeteig", null)],
            ["ofen"],
            "Belegen und backen.");

        // English, in a German library: a household writes both.
        await SaveAsync(world, "Chicken Curry", "en", 15, 25,
            [("chicken breast", "g"), ("coconut milk", "ml")],
            ["asian"],
            "Simmer until done.");

        return world;
    }

    private static async Task<Guid> SaveAsync(
        World world,
        string title,
        string language,
        int? prep,
        int? cook,
        (string Name, string? Unit)[] ingredients,
        string[] tags,
        string step)
    {
        var created = await world.Client.PostAsync(
            "/api/v1/recipes",
            new { householdId = world.HouseholdId, title },
            Token);

        var recipeId = created.Json!.Value.GetProperty("recipeId").GetGuid();
        var read = await world.Client.GetAsync($"/api/v1/recipes/{recipeId}", Token);

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/recipes/{recipeId}")
        {
            Content = JsonContent.Create(new
            {
                title,
                language,
                yieldAmount = 4,
                yieldKind = "servings",
                prepMinutes = prep,
                cookMinutes = cook,
                groups = new[]
                {
                    new
                    {
                        name = (string?)null,
                        ingredients = ingredients
                            .Select(line => new { name = line.Name, unit = line.Unit })
                            .ToArray()
                    }
                },
                steps = new[]
                {
                    new { segments = new[] { new { type = "text", value = step } } }
                },
                tags
            })
        };

        request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(read.ETag!));

        await world.Client.SendAsync(request, Token);

        return recipeId;
    }

    private static async Task RenameAsync(World world, Guid recipeId, string title)
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
