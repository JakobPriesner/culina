using System.Diagnostics;
using System.Globalization;
using System.Text;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Cookbooks;

/// <summary>
/// Smart cookbook reads at the product plan's reference workload: 10,000 recipes in one household.
/// Explicit because it is a measurement (about a minute); run it after changing SmartShelfSql or the shelf queries.
/// </summary>
/// <remarks>
/// The numbers and the budgets they set are in docs/domain-model.md under Cookbook. A smart shelf's count and cover
/// evaluate its rules over the whole library, once per card on GET /cookbooks.
/// </remarks>
[Collection(RequiresDatabase.Name)]
public class SmartCookbookLoadTests(PostgresFixture postgres)
{
    private const int Recipes = 10_000;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The rules of the ten smart shelves: tags, ingredients and time, alone and together.</summary>
    private static readonly (string Name, string[] Tags, string[] Ingredients, int? MaxMinutes)[] Shelves =
    [
        ("Schnell", [], [], 20),
        ("Vegetarisch", ["vegetarisch"], [], null),
        ("Hähnchen", [], ["Hähnchenbrust"], null),
        ("Kokos-Curry", [], ["Kokosmilch", "Ingwer"], null),
        ("Schnelle Pasta", ["pasta"], [], 30),
        ("Ofen mit Käse", ["ofen"], ["Parmesan"], null),
        ("Drei Zutaten", [], ["Zwiebel", "Knoblauch", "Tomaten"], null),
        ("Feierabend", ["feierabend", "einfach"], [], 40),
        ("Suppe", ["suppe"], ["Brühe"], 45),
        ("Linsen", [], ["Linsen"], null)
    ];

    [Fact(Explicit = true)]
    public async Task SmartShelves_ShouldReadWithinBudget_OverTenThousandRecipes()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        await SeedAsync(kitchen);

        List<Guid> smart = [];

        foreach (var (name, tags, ingredients, maxMinutes) in Shelves)
        {
            var created = await kitchen.Client.PostAsync(
                "/api/v1/cookbooks",
                new { householdId = kitchen.HouseholdId, name, rules = new { tags, ingredients, maxMinutes } },
                Token);

            smart.Add(created.Json!.Value.GetProperty("cookbookId").GetGuid());
        }

        await ManualShelvesAsync(kitchen);
        await postgres.ExecuteAsync("analyze;", Token);

        var report = new StringBuilder();
        report.AppendLine(CultureInfo.InvariantCulture, $"Smart cookbooks over {Recipes} recipes");

        // Act
        var list = await TimeAsync(10, () => kitchen.Client.GetAsync($"/api/v1/cookbooks?householdId={kitchen.HouseholdId}&limit=24", Token));
        var detail = await TimeAsync(3, smart, id => kitchen.Client.GetAsync($"/api/v1/cookbooks/{id}", Token));
        var firstPage = await TimeAsync(3, smart, id => kitchen.Client.GetAsync(
            $"/api/v1/recipes?householdId={kitchen.HouseholdId}&cookbookId={id}&limit=24", Token));
        var searched = await TimeAsync(3, smart, id => kitchen.Client.GetAsync(
            $"/api/v1/recipes?householdId={kitchen.HouseholdId}&cookbookId={id}&query=Curry&limit=24", Token));

        report.AppendLine(Line("GET /cookbooks (20 shelves, 10 smart)", list));
        report.AppendLine(Line("GET /cookbooks/{id} (smart)", detail));
        report.AppendLine(Line("GET /recipes?cookbookId (smart, page 1)", firstPage));
        report.AppendLine(Line("GET /recipes?cookbookId&query (smart)", searched));

        var path = Path.Combine(Path.GetTempPath(), "culina-smart-cookbook-load.txt");
        await File.WriteAllTextAsync(path, report.ToString(), Token);
        TestContext.Current.SendDiagnosticMessage(report.ToString());

        // Assert
        // Budgets from the first measurement (docs/domain-model.md), with room for a slower machine.
        Assert.True(P95(list) <= Budget.List, report.ToString());
        Assert.True(P95(detail) <= Budget.Detail, report.ToString());
        Assert.True(P95(firstPage) <= Budget.Page, report.ToString());
        Assert.True(P95(searched) <= Budget.Page, report.ToString());
    }

    /// <summary>Milliseconds, p95, on the reference workload; measured 133, 17 and 365 on 2026-10-14 (arm64 Docker Desktop).</summary>
    private static class Budget
    {
        internal const double List = 250;

        internal const double Detail = 60;

        internal const double Page = 500;
    }

    private async Task SeedAsync(Kitchen kitchen)
    {
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
                select gen_random_uuid(), '{{kitchen.HouseholdId}}',
                       w.title_word || d.dish || ' ' || i, 'de', 4, 'servings',
                       5 + (i % 4) * 5, 5 + (i % 7) * 10,
                       (select user_id from household_members where household_id = '{{kitchen.HouseholdId}}' limit 1),
                       now(), now()
                from generate_series(1, {{Recipes}}) as i
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

            insert into tags (id, household_id, name, slug)
            select gen_random_uuid(), '{{kitchen.HouseholdId}}', slug, slug
            from unnest(array['vegetarisch', 'pasta', 'ofen', 'feierabend', 'einfach', 'suppe',
                              'scharf', 'sommer', 'winter', 'party']) as slug
            on conflict do nothing;

            -- One to three tags a recipe, spread so every rule matches some hundreds.
            insert into recipe_tags (recipe_id, tag_id)
            select distinct r.id, t.id
            from recipes r
            cross join generate_series(0, 2) as k
            join lateral (
                select id from tags
                where household_id = '{{kitchen.HouseholdId}}'
                order by slug
                offset abs(hashtext(r.id::text || k)) % 10 limit 1) t on true
            where r.household_id = '{{kitchen.HouseholdId}}'
              and (k = 0 or abs(hashtext(r.id::text || 'more' || k)) % 3 = 0);
            """,
            Token);

        // Search documents in batches: building ten thousand in one statement outlasts the command timeout.
        foreach (var batch in Enumerable.Range(0, Recipes / 1000))
        {
            await postgres.ExecuteAsync(
                $"""
                insert into recipe_search_documents (
                    recipe_id, household_id, language, document, fuzzy_text,
                    title_ae, title_a, ingredient_count, analyzer_version)
                select recipe_id, household_id, language, document, fuzzy_text,
                       title_ae, title_a, ingredient_count, analyzer_version
                from recipe_search_input
                where recipe_id in (
                    select id from recipes where household_id = '{kitchen.HouseholdId}'
                    order by id offset {batch * 1000} limit 1000)
                on conflict (recipe_id) do nothing;
                """,
                Token);
        }
    }

    // Ten chosen shelves of a hundred recipes each, so the list reads both kinds as it would in a real kitchen.
    private async Task ManualShelvesAsync(Kitchen kitchen) =>
        await postgres.ExecuteAsync(
            $"""
            with made as (
                insert into cookbooks (id, household_id, name, kind, created_by, created_at, updated_at, version)
                select gen_random_uuid(), '{kitchen.HouseholdId}', 'Regal ' || n, 'manual',
                       (select user_id from household_members where household_id = '{kitchen.HouseholdId}' limit 1), now(), now(), 1
                from generate_series(1, 10) as n
                returning id)
            insert into cookbook_recipes (cookbook_id, recipe_id, added_at, sort_order)
            select m.id, r.id, now(), row_number() over (partition by m.id order by r.id)
            from made m
            cross join lateral (
                select id from recipes
                where household_id = '{kitchen.HouseholdId}'
                order by md5(m.id::text || id::text)
                limit 100) r;
            """,
            Token);

    private static async Task<List<double>> TimeAsync(int rounds, Func<Task<ApiResponse>> call) =>
        await TimeAsync(rounds, [0], _ => call());

    // The first round warms connections and plans, and is left out.
    private static async Task<List<double>> TimeAsync<T>(int rounds, IReadOnlyList<T> each, Func<T, Task<ApiResponse>> call)
    {
        List<double> timings = [];

        foreach (var round in Enumerable.Range(0, rounds + 1))
        {
            foreach (var one in each)
            {
                var clock = Stopwatch.StartNew();
                var response = await call(one);
                clock.Stop();

                Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

                if (round > 0)
                {
                    timings.Add(clock.Elapsed.TotalMilliseconds);
                }
            }
        }

        return timings;
    }

    private static double P95(List<double> timings)
    {
        var sorted = timings.Order().ToList();

        return sorted[(int)Math.Ceiling(sorted.Count * 0.95) - 1];
    }

    private static string Line(string what, List<double> timings)
    {
        var sorted = timings.Order().ToList();

        return string.Create(
            CultureInfo.InvariantCulture,
            $"  {what}: median {sorted[sorted.Count / 2]:F1} ms, p95 {P95(timings):F1} ms, max {sorted[^1]:F1} ms (n={sorted.Count})");
    }
}
