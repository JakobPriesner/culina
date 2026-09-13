using Domain.Shared;
using Domain.Shopping;

namespace Domain.Recipes;

/// <summary>
/// The things a home kitchen actually buys, in both languages.
/// </summary>
/// <remarks>
/// <para>
/// What a new kitchen is offered before it has written anything of its own.
/// After a few recipes a household's own words are the better suggestion — they
/// are how <em>this</em> kitchen talks — so this list is the floor, not the
/// vocabulary. Nothing is limited to it and nothing has to be chosen from it.
/// </para>
/// <para>
/// Deliberately short. A list of nine hundred ingredients is a list where the
/// thing you want is the ninth suggestion, and the long tail is exactly where a
/// generic list is least likely to have the words a particular kitchen uses.
/// </para>
/// <para>
/// This is not the same thing as <see cref="SectionKeywords"/>, which the two
/// overlap with and neither replaces. That table matches <em>stems</em> inside
/// a phrase — "strawberr" so that both "strawberry" and "strawberries" find the
/// produce aisle — which is right for guessing and wrong for offering. These
/// are names a person would be happy to see typed into their recipe, which is
/// right for offering and useless for matching. Merging them would make one of
/// the two worse.
/// </para>
/// </remarks>
public static class CommonIngredients
{
    /// <summary>One thing to buy, named in both languages.</summary>
    /// <param name="De">Its German name.</param>
    /// <param name="En">Its English name.</param>
    /// <param name="Section">Where in a shop it is found.</param>
    public sealed record Entry(string De, string En, ShoppingSection Section)
    {
        /// <summary>Its name in this language.</summary>
        /// <param name="language">Which language.</param>
        public string In(Language language) => language == Language.De ? De : En;
    }

    /// <summary>
    /// The ones whose name begins with or contains what was typed, best first.
    /// </summary>
    /// <param name="query">What has been typed, which may be empty.</param>
    /// <param name="language">Which language to name them in.</param>
    /// <param name="limit">At most this many.</param>
    /// <remarks>
    /// A name that <em>starts</em> with the query comes before one that merely
    /// contains it: somebody typing "oil" means the oil, not the boiled
    /// potatoes.
    /// </remarks>
    public static IReadOnlyList<Entry> Matching(string? query, Language language, int limit)
    {
        var wanted = ItemName.Fold(query ?? string.Empty);

        if (wanted.Length == 0)
        {
            return [.. All.Take(limit)];
        }

        return
        [
            .. All
                .Select(entry => (entry, key: ItemName.Fold(entry.In(language))))
                .Where(found => found.key.Contains(wanted, StringComparison.Ordinal))
                .OrderByDescending(found => found.key.StartsWith(wanted, StringComparison.Ordinal))
                .ThenBy(found => found.key.Length)
                .Take(limit)
                .Select(found => found.entry)
        ];
    }

    /// <summary>Every seeded ingredient, grouped the way a shop is walked.</summary>
    public static IReadOnlyList<Entry> All { get; } =
    [
        // Produce
        new("Apfel", "Apple", ShoppingSection.Produce),
        new("Aubergine", "Aubergine", ShoppingSection.Produce),
        new("Avocado", "Avocado", ShoppingSection.Produce),
        new("Banane", "Banana", ShoppingSection.Produce),
        new("Basilikum", "Basil", ShoppingSection.Produce),
        new("Birne", "Pear", ShoppingSection.Produce),
        new("Blaubeeren", "Blueberries", ShoppingSection.Produce),
        new("Blumenkohl", "Cauliflower", ShoppingSection.Produce),
        new("Brokkoli", "Broccoli", ShoppingSection.Produce),
        new("Champignons", "Mushrooms", ShoppingSection.Produce),
        new("Chilischote", "Chilli", ShoppingSection.Produce),
        new("Erdbeeren", "Strawberries", ShoppingSection.Produce),
        new("Frühlingszwiebeln", "Spring onions", ShoppingSection.Produce),
        new("Gurke", "Cucumber", ShoppingSection.Produce),
        new("Ingwer", "Ginger", ShoppingSection.Produce),
        new("Karotten", "Carrots", ShoppingSection.Produce),
        new("Kartoffeln", "Potatoes", ShoppingSection.Produce),
        new("Kirschtomaten", "Cherry tomatoes", ShoppingSection.Produce),
        new("Knoblauch", "Garlic", ShoppingSection.Produce),
        new("Koriander", "Coriander", ShoppingSection.Produce),
        new("Kürbis", "Pumpkin", ShoppingSection.Produce),
        new("Lauch", "Leek", ShoppingSection.Produce),
        new("Limette", "Lime", ShoppingSection.Produce),
        new("Mango", "Mango", ShoppingSection.Produce),
        new("Minze", "Mint", ShoppingSection.Produce),
        new("Orange", "Orange", ShoppingSection.Produce),
        new("Paprika", "Bell pepper", ShoppingSection.Produce),
        new("Petersilie", "Parsley", ShoppingSection.Produce),
        new("Radieschen", "Radishes", ShoppingSection.Produce),
        new("Rosmarin", "Rosemary", ShoppingSection.Produce),
        new("Rote Zwiebel", "Red onion", ShoppingSection.Produce),
        new("Salat", "Lettuce", ShoppingSection.Produce),
        new("Sellerie", "Celery", ShoppingSection.Produce),
        new("Spinat", "Spinach", ShoppingSection.Produce),
        new("Süßkartoffel", "Sweet potato", ShoppingSection.Produce),
        new("Thymian", "Thyme", ShoppingSection.Produce),
        new("Tomaten", "Tomatoes", ShoppingSection.Produce),
        new("Trauben", "Grapes", ShoppingSection.Produce),
        new("Weißkohl", "White cabbage", ShoppingSection.Produce),
        new("Zitrone", "Lemon", ShoppingSection.Produce),
        new("Zucchini", "Courgette", ShoppingSection.Produce),
        new("Zwiebel", "Onion", ShoppingSection.Produce),

        // Dairy and eggs
        new("Butter", "Butter", ShoppingSection.DairyEggs),
        new("Crème fraîche", "Crème fraîche", ShoppingSection.DairyEggs),
        new("Eier", "Eggs", ShoppingSection.DairyEggs),
        new("Feta", "Feta", ShoppingSection.DairyEggs),
        new("Frischkäse", "Cream cheese", ShoppingSection.DairyEggs),
        new("Griechischer Joghurt", "Greek yoghurt", ShoppingSection.DairyEggs),
        new("Hafermilch", "Oat milk", ShoppingSection.DairyEggs),
        new("Halloumi", "Halloumi", ShoppingSection.DairyEggs),
        new("Joghurt", "Yoghurt", ShoppingSection.DairyEggs),
        new("Käse", "Cheese", ShoppingSection.DairyEggs),
        new("Mascarpone", "Mascarpone", ShoppingSection.DairyEggs),
        new("Milch", "Milk", ShoppingSection.DairyEggs),
        new("Mozzarella", "Mozzarella", ShoppingSection.DairyEggs),
        new("Parmesan", "Parmesan", ShoppingSection.DairyEggs),
        new("Quark", "Quark", ShoppingSection.DairyEggs),
        new("Ricotta", "Ricotta", ShoppingSection.DairyEggs),
        new("Sahne", "Cream", ShoppingSection.DairyEggs),
        new("Schmand", "Sour cream", ShoppingSection.DairyEggs),

        // Meat and fish
        new("Garnelen", "Prawns", ShoppingSection.MeatFish),
        new("Hackfleisch", "Minced meat", ShoppingSection.MeatFish),
        new("Hähnchenbrust", "Chicken breast", ShoppingSection.MeatFish),
        new("Hähnchenschenkel", "Chicken thighs", ShoppingSection.MeatFish),
        new("Lachs", "Salmon", ShoppingSection.MeatFish),
        new("Lammfleisch", "Lamb", ShoppingSection.MeatFish),
        new("Putenbrust", "Turkey breast", ShoppingSection.MeatFish),
        new("Rindfleisch", "Beef", ShoppingSection.MeatFish),
        new("Schinken", "Ham", ShoppingSection.MeatFish),
        new("Schweinefleisch", "Pork", ShoppingSection.MeatFish),
        new("Speck", "Bacon", ShoppingSection.MeatFish),
        new("Thunfisch", "Tuna", ShoppingSection.MeatFish),
        new("Wurst", "Sausage", ShoppingSection.MeatFish),

        // Bakery
        new("Baguette", "Baguette", ShoppingSection.Bakery),
        new("Brot", "Bread", ShoppingSection.Bakery),
        new("Brötchen", "Bread rolls", ShoppingSection.Bakery),
        new("Fladenbrot", "Flatbread", ShoppingSection.Bakery),
        new("Sauerteigbrot", "Sourdough bread", ShoppingSection.Bakery),
        new("Toastbrot", "Sliced bread", ShoppingSection.Bakery),
        new("Tortillas", "Tortillas", ShoppingSection.Bakery),

        // Dry goods
        new("Basmatireis", "Basmati rice", ShoppingSection.DryGoods),
        new("Bohnen", "Beans", ShoppingSection.DryGoods),
        new("Bulgur", "Bulgur", ShoppingSection.DryGoods),
        new("Cashewkerne", "Cashews", ShoppingSection.DryGoods),
        new("Couscous", "Couscous", ShoppingSection.DryGoods),
        new("Haferflocken", "Rolled oats", ShoppingSection.DryGoods),
        new("Kichererbsen", "Chickpeas", ShoppingSection.DryGoods),
        new("Linsen", "Lentils", ShoppingSection.DryGoods),
        new("Mandeln", "Almonds", ShoppingSection.DryGoods),
        new("Mehl", "Flour", ShoppingSection.DryGoods),
        new("Nudeln", "Pasta", ShoppingSection.DryGoods),
        new("Orzo", "Orzo", ShoppingSection.DryGoods),
        new("Polenta", "Polenta", ShoppingSection.DryGoods),
        new("Quinoa", "Quinoa", ShoppingSection.DryGoods),
        new("Reis", "Rice", ShoppingSection.DryGoods),
        new("Semmelbrösel", "Breadcrumbs", ShoppingSection.DryGoods),
        new("Sonnenblumenkerne", "Sunflower seeds", ShoppingSection.DryGoods),
        new("Spaghetti", "Spaghetti", ShoppingSection.DryGoods),
        new("Speisestärke", "Cornflour", ShoppingSection.DryGoods),
        new("Vollkornmehl", "Wholemeal flour", ShoppingSection.DryGoods),
        new("Walnüsse", "Walnuts", ShoppingSection.DryGoods),

        // Tins and jars
        new("Erdnussbutter", "Peanut butter", ShoppingSection.CannedJars),
        new("Gehackte Tomaten", "Chopped tomatoes", ShoppingSection.CannedJars),
        new("Gemüsebrühe", "Vegetable stock", ShoppingSection.CannedJars),
        new("Honig", "Honey", ShoppingSection.CannedJars),
        new("Hühnerbrühe", "Chicken stock", ShoppingSection.CannedJars),
        new("Kapern", "Capers", ShoppingSection.CannedJars),
        new("Ketchup", "Ketchup", ShoppingSection.CannedJars),
        new("Kokosmilch", "Coconut milk", ShoppingSection.CannedJars),
        new("Mayonnaise", "Mayonnaise", ShoppingSection.CannedJars),
        new("Oliven", "Olives", ShoppingSection.CannedJars),
        new("Passierte Tomaten", "Passata", ShoppingSection.CannedJars),
        new("Senf", "Mustard", ShoppingSection.CannedJars),
        new("Tomatenmark", "Tomato purée", ShoppingSection.CannedJars),

        // Frozen
        new("Blätterteig", "Puff pastry", ShoppingSection.Frozen),
        new("Tiefkühlerbsen", "Frozen peas", ShoppingSection.Frozen),
        new("Tiefkühlspinat", "Frozen spinach", ShoppingSection.Frozen),

        // Spices and baking
        new("Backpulver", "Baking powder", ShoppingSection.SpicesBaking),
        new("Balsamico", "Balsamic vinegar", ShoppingSection.SpicesBaking),
        new("Brauner Zucker", "Brown sugar", ShoppingSection.SpicesBaking),
        new("Chiliflocken", "Chilli flakes", ShoppingSection.SpicesBaking),
        new("Currypulver", "Curry powder", ShoppingSection.SpicesBaking),
        new("Essig", "Vinegar", ShoppingSection.SpicesBaking),
        new("Kakaopulver", "Cocoa powder", ShoppingSection.SpicesBaking),
        new("Kreuzkümmel", "Cumin", ShoppingSection.SpicesBaking),
        new("Kurkuma", "Turmeric", ShoppingSection.SpicesBaking),
        new("Lorbeerblätter", "Bay leaves", ShoppingSection.SpicesBaking),
        new("Misopaste", "Miso paste", ShoppingSection.SpicesBaking),
        new("Muskatnuss", "Nutmeg", ShoppingSection.SpicesBaking),
        new("Natron", "Bicarbonate of soda", ShoppingSection.SpicesBaking),
        new("Olivenöl", "Olive oil", ShoppingSection.SpicesBaking),
        new("Oregano", "Oregano", ShoppingSection.SpicesBaking),
        new("Paprikapulver", "Paprika", ShoppingSection.SpicesBaking),
        new("Pfeffer", "Pepper", ShoppingSection.SpicesBaking),
        new("Rapsöl", "Rapeseed oil", ShoppingSection.SpicesBaking),
        new("Salz", "Salt", ShoppingSection.SpicesBaking),
        new("Schokolade", "Chocolate", ShoppingSection.SpicesBaking),
        new("Sesam", "Sesame seeds", ShoppingSection.SpicesBaking),
        new("Sojasauce", "Soy sauce", ShoppingSection.SpicesBaking),
        new("Trockenhefe", "Dried yeast", ShoppingSection.SpicesBaking),
        new("Vanilleextrakt", "Vanilla extract", ShoppingSection.SpicesBaking),
        new("Zimt", "Cinnamon", ShoppingSection.SpicesBaking),
        new("Zucker", "Sugar", ShoppingSection.SpicesBaking),

        // Drinks
        new("Kaffee", "Coffee", ShoppingSection.Drinks),
        new("Rotwein", "Red wine", ShoppingSection.Drinks),
        new("Wasser", "Water", ShoppingSection.Drinks),
        new("Weißwein", "White wine", ShoppingSection.Drinks)
    ];
}
