namespace Domain.Shopping;

/// <summary>
/// Guesses where in a shop a thing is found, from its name.
/// </summary>
/// <remarks>
/// <para>
/// A default that is right most of the time and corrected in one tap, rather
/// than a configuration screen nobody opens. When it is wrong the cost is that
/// one line sits under the wrong heading, which nobody will notice while
/// holding a basket.
/// </para>
/// <para>
/// German and English together, because a household writes both — half the
/// recipes came from a German blog and half from an English one, and the
/// shopping list does not care which.
/// </para>
/// </remarks>
public static class SectionKeywords
{
    /// <summary>Where a thing called this is found, or <c>Other</c>.</summary>
    /// <param name="name">The item's name, already folded for comparison.</param>
    public static ShoppingSection SectionFor(ItemName name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var key = name.ComparisonKey;

        foreach (var (keyword, section) in Table)
        {
            // Contains rather than equals: "rote zwiebel" and "spring onion"
            // both have to find their way, and a shopping list is written in
            // phrases rather than lemmas.
            if (key.Contains(keyword, StringComparison.Ordinal))
            {
                return section;
            }
        }

        return ShoppingSection.Other;
    }

    /// <summary>
    /// The keyword table, longest keywords first.
    /// </summary>
    /// <remarks>
    /// Order matters: "kokosmilch" must be found before "milch", or coconut
    /// milk ends up in the dairy aisle.
    /// </remarks>
    private static readonly (string Keyword, ShoppingSection Section)[] Table = Build();

    private static (string, ShoppingSection)[] Build()
    {
        var entries = new List<(string, ShoppingSection)>();

        void Add(ShoppingSection section, params string[] keywords)
        {
            foreach (var keyword in keywords)
            {
                entries.Add((keyword, section));
            }
        }

        Add(ShoppingSection.Produce,
            "apfel", "apple", "banane", "banana", "zitrone", "lemon", "limette", "lime",
            "orange", "beere", "berry", "erdbeer", "strawberr", "tomate", "tomato",
            "gurke", "cucumber", "zwiebel", "onion", "knoblauch", "garlic", "kartoffel",
            "potato", "karotte", "carrot", "moehre", "paprika", "pepper", "salat",
            "lettuce", "spinat", "spinach", "zucchini", "courgette", "aubergine",
            "eggplant", "brokkoli", "broccoli", "blumenkohl", "cauliflower", "pilz",
            "mushroom", "champignon", "lauch", "leek", "sellerie", "celery", "ingwer",
            "ginger", "petersilie", "parsley", "basilikum", "basil", "koriander",
            "coriander", "cilantro", "minze", "mint", "thymian", "thyme", "rosmarin",
            "rosemary", "avocado", "kuerbis", "pumpkin", "squash", "birne", "pear",
            "traube", "grape", "mango", "kraut", "cabbage", "radieschen", "radish");

        Add(ShoppingSection.DairyEggs,
            "kokosmilch", "coconut milk", "mandelmilch", "almond milk", "hafermilch",
            "oat milk", "milch", "milk", "sahne", "cream", "butter", "kaese", "cheese",
            "parmesan", "mozzarella", "feta", "joghurt", "yoghurt", "yogurt", "quark",
            "schmand", "creme fraiche", "ei", "egg", "frischkaese", "ricotta",
            "mascarpone", "margarine");

        Add(ShoppingSection.MeatFish,
            "haehnchen", "chicken", "huhn", "rind", "beef", "schwein", "pork", "hack",
            "mince", "speck", "bacon", "wurst", "sausage", "schinken", "ham", "lachs",
            "salmon", "thunfisch", "tuna", "garnele", "prawn", "shrimp", "fisch", "fish",
            "lamm", "lamb", "pute", "turkey");

        Add(ShoppingSection.Bakery,
            "brot", "bread", "broetchen", "roll", "baguette", "toast", "brioche",
            "croissant", "sauerteig", "sourdough", "fladenbrot", "pita", "tortilla");

        Add(ShoppingSection.DryGoods,
            "mehl", "flour", "reis", "rice", "nudel", "pasta", "spaghetti", "orzo",
            "penne", "linse", "lentil", "bohne", "bean", "kichererbse", "chickpea",
            "hafer", "oat", "muesli", "granola", "couscous", "quinoa", "bulgur",
            "polenta", "nuss", "nut", "mandel", "almond", "walnuss", "cashew",
            "sonnenblumenkern", "seed");

        Add(ShoppingSection.CannedJars,
            "dose", "tin", "konserve", "passierte tomaten", "passata", "tomatenmark",
            "tomato paste", "kokosmilch dose", "oliven", "olive", "kapern", "caper",
            "senf", "mustard", "ketchup", "mayonnaise", "honig", "honey", "marmelade",
            "jam", "erdnussbutter", "peanut butter", "bruehe", "broth", "stock");

        Add(ShoppingSection.Frozen,
            "tiefkuehl", "frozen", "eiscreme", "ice cream", "tk ");

        Add(ShoppingSection.SpicesBaking,
            "salz", "salt", "pfeffer", "zucker", "sugar", "vanille", "vanilla",
            "zimt", "cinnamon", "kreuzkuemmel", "cumin", "curry", "paprikapulver",
            "chili", "muskat", "nutmeg", "backpulver", "baking powder", "natron",
            "baking soda", "hefe", "yeast", "kakao", "cocoa", "schokolade", "chocolate",
            "essig", "vinegar", "oel", "oil", "olivenoel", "sojasauce", "soy sauce",
            "miso", "lorbeer", "bay leaf", "oregano", "kurkuma", "turmeric");

        Add(ShoppingSection.Drinks,
            "wasser", "water", "saft", "juice", "wein", "wine", "bier", "beer",
            "kaffee", "coffee", "tee", "tea", "limonade", "cola");

        Add(ShoppingSection.Household,
            "spuelmittel", "washing up", "seife", "soap", "papier", "paper",
            "muellbeutel", "bin bag", "folie", "foil", "backpapier", "parchment",
            "schwamm", "sponge");

        // Longest first, so a specific keyword beats the general one it contains.
        return [.. entries.OrderByDescending(entry => entry.Item1.Length)];
    }
}
