using System.Globalization;
using System.Text;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Suggestions.Replay;

/// <summary>
/// Two years of a two-person kitchen, simulated, for the replay to predict.
/// </summary>
/// <remarks>
/// <para>
/// <b>This proves the harness measures something, not that the weights are
/// right.</b> The habits below are a guess at an ordinary household — a weekday
/// rotation, soups in winter and salads in summer, a project at the weekend, a
/// new recipe that gets made a few times and then settles down — and a ranker
/// tuned to them is tuned to this file. A weight change is defended with a
/// replay of a real cook log; this kitchen is what the harness is tested on,
/// and what a calibration runs on when there is no real one to hand.
/// </para>
/// <para>
/// The history is deterministic — a fixed seed and fixed dates, so the same
/// code cooks the same meals on every machine, on every day it runs. The recipe
/// ids are not, and the ranker's exploration jitter is seeded by them, so two
/// runs differ in the third decimal of recall. Two weight vectors replayed in
/// the same run see the same ids, which is the comparison that matters.
/// </para>
/// </remarks>
internal static class ReplayKitchen
{
    /// <summary>The first day anything is cooked.</summary>
    internal static readonly DateTimeOffset Opens = new(2024, 9, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>The day after the last day anything is cooked.</summary>
    internal static readonly DateTimeOffset Closes = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    private const int Seed = 20240901;

    private enum Kind
    {
        Staple,
        Soup,
        Summer,
        Project,
        Occasional,
        Arrival,
        Untouched
    }

    private enum Cook
    {
        Ada,
        Bob
    }

    /// <param name="ArrivesOnDay">Days after <see cref="Opens"/> the recipe is written down.</param>
    /// <param name="FavouriteOf">Whose rotation it is in, when it is in one person's more than the other's.</param>
    private sealed record Dish(
        string Title,
        Kind Kind,
        string[] Ingredients,
        string[] Tags,
        int Minutes,
        int Steps = 3,
        int ArrivesOnDay = 0,
        Cook? FavouriteOf = null);

    private sealed record Cooked(Dish Dish, Cook By, DateTimeOffset At);

    private static readonly Dish[] Dishes =
    [
        new("Spaghetti aglio e olio", Kind.Staple, ["spaghetti", "knoblauch", "olivenöl", "chili"], ["pasta", "schnell"], 20, FavouriteOf: Cook.Ada),
        new("Penne all'arrabbiata", Kind.Staple, ["penne", "tomate", "knoblauch", "chili"], ["pasta", "schnell"], 25, FavouriteOf: Cook.Ada),
        new("Gebratener Reis", Kind.Staple, ["reis", "ei", "frühlingszwiebel", "sojasauce"], ["reis", "schnell"], 20, FavouriteOf: Cook.Ada),
        new("Omelette mit Kräutern", Kind.Staple, ["ei", "schnittlauch", "butter"], ["ei", "schnell"], 10, FavouriteOf: Cook.Ada),
        new("Chili sin carne", Kind.Staple, ["kidneybohnen", "tomate", "paprika", "kreuzkümmel"], ["eintopf"], 40, FavouriteOf: Cook.Bob),
        new("Hähnchen-Curry", Kind.Staple, ["hähnchen", "kokosmilch", "currypaste", "reis"], ["curry", "reis"], 35, FavouriteOf: Cook.Bob),
        new("Bratkartoffeln mit Spiegelei", Kind.Staple, ["kartoffel", "ei", "zwiebel", "speck"], ["kartoffel"], 30, FavouriteOf: Cook.Bob),
        new("Pasta mit Pesto", Kind.Staple, ["spaghetti", "basilikum", "pinienkerne", "parmesan"], ["pasta", "schnell"], 15),

        new("Linsensuppe", Kind.Soup, ["rote linsen", "karotte", "ingwer", "brühe"], ["suppe", "winter"], 45, 4),
        new("Kürbissuppe", Kind.Soup, ["hokkaido", "ingwer", "kokosmilch", "brühe"], ["suppe", "winter"], 40, 4),
        new("Kartoffelsuppe", Kind.Soup, ["kartoffel", "lauch", "brühe", "sahne"], ["suppe", "winter"], 45, 4),
        new("Minestrone", Kind.Soup, ["zucchini", "bohnen", "tomate", "brühe"], ["suppe"], 50, 5),
        new("Erbseneintopf", Kind.Soup, ["erbsen", "kartoffel", "speck", "brühe"], ["suppe", "eintopf", "winter"], 60, 4),
        new("Tomatensuppe", Kind.Soup, ["tomate", "zwiebel", "brühe", "basilikum"], ["suppe"], 35, 3),

        new("Griechischer Salat", Kind.Summer, ["gurke", "tomate", "feta", "oliven"], ["salat", "sommer"], 15, 2),
        new("Caprese", Kind.Summer, ["tomate", "mozzarella", "basilikum", "olivenöl"], ["salat", "sommer"], 10, 1),
        new("Gegrillte Zucchini", Kind.Summer, ["zucchini", "olivenöl", "zitrone", "minze"], ["grill", "sommer"], 25, 2),
        new("Kalte Gurkensuppe", Kind.Summer, ["gurke", "joghurt", "dill", "knoblauch"], ["sommer", "suppe"], 15, 2),
        new("Nudelsalat", Kind.Summer, ["fusilli", "paprika", "mais", "mayonnaise"], ["salat", "sommer", "pasta"], 25, 3),
        new("Gegrillter Halloumi", Kind.Summer, ["halloumi", "paprika", "zitrone"], ["grill", "sommer"], 20, 2),

        new("Rinderschmorbraten", Kind.Project, ["rind", "rotwein", "karotte", "sellerie"], ["wochenende", "schmoren"], 210, 9),
        new("Lasagne", Kind.Project, ["lasagneblätter", "hackfleisch", "tomate", "béchamel"], ["wochenende", "pasta"], 120, 8, FavouriteOf: Cook.Bob),
        new("Selbstgemachte Pizza", Kind.Project, ["mehl", "hefe", "tomate", "mozzarella"], ["wochenende"], 150, 7),
        new("Ramen", Kind.Project, ["schweinebauch", "ramen-nudeln", "ei", "brühe"], ["wochenende", "suppe"], 240, 10, FavouriteOf: Cook.Bob),
        new("Gulasch", Kind.Project, ["rind", "zwiebel", "paprikapulver", "kümmel"], ["wochenende", "schmoren", "eintopf"], 180, 6),
        new("Brathähnchen", Kind.Project, ["hähnchen", "zitrone", "thymian", "kartoffel"], ["wochenende"], 100, 6),

        new("Shakshuka", Kind.Occasional, ["ei", "tomate", "paprika", "kreuzkümmel"], ["ei"], 30),
        new("Falafel-Wrap", Kind.Occasional, ["kichererbsen", "fladenbrot", "tahini", "petersilie"], [], 40, 5),
        new("Käsespätzle", Kind.Occasional, ["spätzle", "bergkäse", "zwiebel"], ["kartoffel"], 35),
        new("Fischstäbchen mit Kartoffelpüree", Kind.Occasional, ["fischstäbchen", "kartoffel", "milch"], ["fisch", "kartoffel"], 30),
        new("Lachs mit Ofengemüse", Kind.Occasional, ["lachs", "brokkoli", "zitrone"], ["fisch"], 35),
        new("Risotto ai funghi", Kind.Occasional, ["risottoreis", "champignons", "parmesan", "weißwein"], ["reis"], 45, 5),
        new("Dal", Kind.Occasional, ["gelbe linsen", "kurkuma", "ingwer", "kokosmilch"], ["curry"], 40),
        new("Quiche Lorraine", Kind.Occasional, ["mürbeteig", "speck", "ei", "sahne"], [], 70, 6),
        new("Burritos", Kind.Occasional, ["tortilla", "kidneybohnen", "reis", "avocado"], [], 35),
        new("Pfannkuchen", Kind.Occasional, ["mehl", "ei", "milch"], ["ei", "schnell"], 20),

        new("Ofengemüse mit Aubergine", Kind.Arrival, ["aubergine", "zucchini", "paprika", "feta"], ["ofen"], 45, ArrivesOnDay: 120),
        new("Bibimbap", Kind.Arrival, ["reis", "ei", "spinat", "gochujang"], ["reis"], 40, 5, ArrivesOnDay: 250),
        new("Pad Thai", Kind.Arrival, ["reisnudeln", "tofu", "erdnüsse", "limette"], ["schnell"], 30, ArrivesOnDay: 380),
        new("Moussaka", Kind.Arrival, ["aubergine", "hackfleisch", "kartoffel", "béchamel"], ["ofen", "wochenende"], 120, 8, ArrivesOnDay: 470),
        new("Miso-Aubergine", Kind.Arrival, ["aubergine", "miso", "reis", "sesam"], ["reis"], 35, ArrivesOnDay: 560),
        new("Tteokbokki", Kind.Arrival, ["reiskuchen", "gochujang", "ei", "frühlingszwiebel"], ["schnell"], 25, ArrivesOnDay: 650),

        new("Beef Wellington", Kind.Untouched, ["rind", "blätterteig", "champignons", "schinken"], ["wochenende"], 180, 12),
        new("Bouillabaisse", Kind.Untouched, ["fisch", "safran", "fenchel", "brühe"], ["fisch", "suppe"], 120, 9),
        new("Sushi", Kind.Untouched, ["sushireis", "nori", "lachs", "gurke"], ["fisch", "reis"], 90, 8),
        new("Cassoulet", Kind.Untouched, ["weiße bohnen", "entenkeule", "wurst"], ["schmoren"], 300, 10),
        new("Tarte Tatin", Kind.Untouched, ["apfel", "blätterteig", "zucker", "butter"], [], 75, 6),
        new("Szechuan-Tofu", Kind.Untouched, ["tofu", "szechuanpfeffer", "chili", "hackfleisch"], ["schnell"], 30),
    ];

    /// <summary>
    /// Writes the kitchen through the API, backdates the recipes to when they
    /// arrived, and records every meal.
    /// </summary>
    /// <returns>The household, for the replay to ask about.</returns>
    internal static async Task<Guid> BuildAsync(PostgresFixture postgres, CancellationToken cancellationToken)
    {
        var world = await SuggestionWorld.NewAsync(postgres);
        var bob = await world.InviteAsync("bob@example.com", "Bob");

        Dictionary<Dish, Guid> ids = [];

        foreach (var dish in Dishes)
        {
            ids[dish] = await world.WriteAsync(
                dish.Title,
                dish.Ingredients,
                dish.Tags,
                prep: dish.Minutes / 3,
                cook: dish.Minutes - (dish.Minutes / 3),
                steps: dish.Steps);
        }

        // The API writes a recipe as of now, which is the right thing for it to
        // do and the one fact about this history it cannot express. Reaching
        // past it for the date is the only way to have a book that grew.
        await postgres.ExecuteAsync(BackdateSql(ids), cancellationToken);

        foreach (var meal in History())
        {
            await world.CookedAtAsync(ids[meal.Dish], meal.At, meal.By == Cook.Bob ? bob : null);
        }

        // Statistics, as autovacuum would have gathered them on a kitchen this
        // old. Without them every scoring CTE is estimated at one row, the
        // planner nests loops over all of them, and a shortlist that takes a
        // few milliseconds in a real household takes seventy here.
        await postgres.ExecuteAsync("analyze;", cancellationToken);

        return world.HouseholdId;
    }

    private static string BackdateSql(Dictionary<Dish, Guid> ids)
    {
        var values = new StringBuilder();

        foreach (var (dish, id) in ids)
        {
            var arrived = Opens.AddDays(dish.ArrivesOnDay - 1).ToString("O", CultureInfo.InvariantCulture);

            values.Append(values.Length == 0 ? string.Empty : ",\n")
                .Append(CultureInfo.InvariantCulture, $"('{id}'::uuid, '{arrived}'::timestamptz)");
        }

        return $"""
            update recipes
            set created_at = v.arrived, updated_at = v.arrived
            from (values {values}) as v(id, arrived)
            where recipes.id = v.id;
            """;
    }

#pragma warning disable CA5394 // A seeded simulation, not a secret: the same history every run is the point.
    /// <summary>Every dinner, in order.</summary>
    private static List<Cooked> History()
    {
        var random = new Random(Seed);
        Dictionary<Dish, DateTimeOffset> last = [];
        List<Cooked> history = [];

        for (var day = Opens; day < Closes; day = day.AddDays(1))
        {
            // Not every evening is a recipe from the book: leftovers, a take-away,
            // somebody else's kitchen.
            if (random.NextDouble() > 0.6)
            {
                continue;
            }

            var by = random.NextDouble() < 0.7 ? Cook.Ada : Cook.Bob;
            var at = day.AddHours(18 + random.Next(0, 3));
            var dish = Choose(random, day, by, last);

            last[dish] = at;
            history.Add(new Cooked(dish, by, at));
        }

        return history;
    }

    private static Dish Choose(Random random, DateTimeOffset day, Cook by, Dictionary<Dish, DateTimeOffset> last)
    {
        var appetites = Dishes.Select(dish => Appetite(dish, day, by, last)).ToArray();
        var pick = random.NextDouble() * appetites.Sum();

        for (var index = 0; index < Dishes.Length; index++)
        {
            pick -= appetites[index];

            if (pick <= 0)
            {
                return Dishes[index];
            }
        }

        return Dishes[^1];
    }

#pragma warning restore CA5394

    /// <summary>How much this person feels like this dish tonight. Relative, in no unit.</summary>
    private static double Appetite(Dish dish, DateTimeOffset day, Cook by, Dictionary<Dish, DateTimeOffset> last)
    {
        var weekend = day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        var winter = day.Month is >= 10 or <= 3;
        var summer = day.Month is >= 5 and <= 8;
        var daysSinceArrival = (day - Opens).TotalDays - dish.ArrivesOnDay;

        var appetite = dish.Kind switch
        {
            Kind.Staple => weekend ? 1.0 : 3.0,
            Kind.Soup => winter ? 2.5 : 0.1,
            Kind.Summer => summer ? 2.5 : 0.1,
            Kind.Project => weekend ? 3.0 : 0.05,
            Kind.Occasional => 0.4,
            Kind.Arrival when daysSinceArrival < 0 => 0,
            Kind.Arrival => daysSinceArrival < 30 ? 4.0 : 0.5,
            _ => 0
        };

        if (dish.FavouriteOf == by)
        {
            appetite *= 2;
        }

        return appetite * Weariness(dish, day, last);
    }

    /// <summary>Nobody wants Tuesday's dinner on Wednesday, and not much on Saturday either.</summary>
    private static double Weariness(Dish dish, DateTimeOffset day, Dictionary<Dish, DateTimeOffset> last)
    {
        if (!last.TryGetValue(dish, out var cooked))
        {
            return 1;
        }

        var daysAgo = (day - cooked).TotalDays;

        if (daysAgo < 4)
        {
            return 0;
        }

        return daysAgo < 10 ? 0.5 : 1;
    }
}
