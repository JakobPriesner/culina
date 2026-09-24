using System.Collections.Frozen;

namespace Domain.Search;

/// <summary>
/// What else a word means, to a cook: a curated, bilingual table of culinary
/// concepts.
/// </summary>
/// <remarks>
/// <para>
/// The third of a family. <c>CommonIngredients</c> answers "what might they be
/// typing?" and <c>SectionKeywords</c> answers "which aisle?"; this answers
/// "what else means this?" — that <em>Hähnchen</em> is <em>chicken</em>, that
/// chicken is poultry and poultry is meat, that <em>Waffeln</em> are a dessert
/// somebody searching for <em>Nachtisch</em> would want to see. Three
/// questions, three tables, because merging them would make each answer worse
/// at its own job.
/// </para>
/// <para>
/// Not an ontology. A flat list of concepts, each a handful of surface forms in
/// each language and a parent or two, closed into full ancestor lists once at
/// startup. No reasoner and nothing walked at query time.
/// </para>
/// <para>
/// Bounded on purpose, for the reason <c>CommonIngredients</c> gives for
/// staying short: a table of three hundred entries can be read and argued
/// with; three thousand would be a liability whose long tail is exactly where
/// it is most likely to be wrong. When it is wrong, the damage is contained by
/// where its matches land — the concept lane is the bottom tier, below
/// everything a query actually names.
/// </para>
/// <para>
/// One rule for writing an entry, and it is what keeps compounds honest: a
/// compound earns a form of its own only when it means something its parts do
/// not. <em>Kokosmilch</em> is not milk and <em>Zwiebelkuchen</em> is not a
/// cake, so both are listed and win over their parts. <em>Schweinebraten</em>
/// is pork and a roast, and is left for its parts to find.
/// </para>
/// <para>
/// Changing anything here changes what is indexed, so it changes
/// <see cref="Version"/> too: every document carries the version it was built
/// with, and the ones left behind are rebuilt when the container starts.
/// </para>
/// </remarks>
public static class CulinaryLexicon
{
    /// <summary>
    /// The version of this table. Raise it with any change to an entry or to
    /// how text is matched against them.
    /// </summary>
    public const int Version = 3;

    private static readonly Compiled Index = new(Entries());

    /// <summary>Every concept, in the order they are written below.</summary>
    public static IReadOnlyList<Concept> All => Index.Concepts;

    /// <summary>The concept with this key, or null.</summary>
    public static Concept? Find(string key) =>
        Index.ByKey.GetValueOrDefault(key);

    /// <summary>
    /// The concept and everything it is a kind of, nearest first.
    /// </summary>
    public static IReadOnlyList<string> Lineage(string key) =>
        Index.Closure.TryGetValue(key, out var lineage) ? lineage : [];

    /// <summary>
    /// The concepts a query names, and nothing they imply.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Somebody typing <em>Hähnchen</em> means chicken, not everything that is
    /// meat — so no ancestors here.
    /// </para>
    /// <para>
    /// And a query word names a concept only when one of its forms is the
    /// whole word, give or take a short ending: <em>italienische</em> is
    /// Italian, but <em>Ofengemüse</em> is not "baked" and "vegetable". A
    /// compound somebody types is almost always the name of the thing they
    /// want, and splitting it would answer a known-item search with half the
    /// library. A recipe is read the generous way instead (see
    /// <see cref="Describe"/>), because a recipe <em>is</em> everything it
    /// contains; the asymmetry is what lets a whole word in a query find a
    /// part of a compound in a recipe, and never the reverse.
    /// </para>
    /// </remarks>
    public static IReadOnlySet<string> Recognise(string query) => Read(query, whole: true);

    /// <summary>
    /// The concept this text is a name of, or null.
    /// </summary>
    /// <remarks>
    /// Stricter than <see cref="Recognise"/>: one form has to account for the
    /// whole of the text, give or take a short ending. "ohne Fleisch" is a name
    /// of vegetarian; "leckeres Abendessen" is not a name of dinner, however
    /// much it mentions one. It is what lets a parser consume exactly the words
    /// a concept was found in, and no others.
    /// </remarks>
    public static Concept? Name(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var key = Index.Whole(SearchText.FoldAe(text)) ?? Index.Whole(SearchText.FoldA(text));

        return key is null ? null : Index.ByKey[key];
    }

    private static HashSet<string> Read(string text, bool whole)
    {
        ArgumentNullException.ThrowIfNull(text);

        var found = new HashSet<string>(StringComparer.Ordinal);

        Index.Match(SearchText.FoldAe(text), found, whole);
        Index.Match(SearchText.FoldA(text), found, whole);

        return found;
    }

    /// <summary>
    /// Everything a recipe is, as concepts with their ancestors: what its
    /// document is indexed under.
    /// </summary>
    /// <remarks>
    /// A diet is taken from the title and the tags and never from an
    /// ingredient. "Vegane Lasagne" and a <c>vegan</c> tag are somebody saying
    /// so; "pflanzliche Sahne" in an ingredient list says only that one
    /// ingredient is.
    /// </remarks>
    /// <param name="title">The recipe's title.</param>
    /// <param name="tags">Its tag names.</param>
    /// <param name="ingredients">Its ingredient names.</param>
    public static IReadOnlyList<string> Describe(
        string title,
        IEnumerable<string> tags,
        IEnumerable<string> ingredients)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentNullException.ThrowIfNull(ingredients);

        var named = Read(title, whole: false);

        foreach (var tag in tags)
        {
            named.UnionWith(Read(tag, whole: false));
        }

        foreach (var ingredient in ingredients)
        {
            named.UnionWith(Read(ingredient, whole: false).Where(key => Index.ByKey[key].Kind != ConceptKind.Diet));
        }

        return [.. named.SelectMany(Lineage).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// The table, compiled into what matching needs.
    /// </summary>
    private sealed class Compiled
    {
        internal Compiled(IReadOnlyList<Concept> concepts)
        {
            Concepts = concepts;
            ByKey = concepts.ToFrozenDictionary(concept => concept.Key, StringComparer.Ordinal);
            Closure = concepts.ToFrozenDictionary(
                concept => concept.Key,
                concept => (IReadOnlyList<string>)Ancestry(concept.Key),
                StringComparer.Ordinal);

            var words = new Dictionary<string, string>(StringComparer.Ordinal);
            var phrases = new Dictionary<string, List<(string[] Words, string Key)>>(StringComparer.Ordinal);

            foreach (var concept in concepts)
            {
                foreach (var form in concept.De.Concat(concept.En))
                {
                    foreach (var folded in new[] { SearchText.FoldAe(form), SearchText.FoldA(form) })
                    {
                        var parts = folded.Split(' ');

                        if (parts.Length == 1)
                        {
                            words.TryAdd(folded, concept.Key);
                        }
                        else
                        {
                            if (!phrases.TryGetValue(parts[0], out var starting))
                            {
                                phrases[parts[0]] = starting = [];
                            }

                            if (!starting.Exists(one => one.Words.SequenceEqual(parts)))
                            {
                                starting.Add((parts, concept.Key));
                            }
                        }
                    }
                }
            }

            WordLookup = words.GetAlternateLookup<ReadOnlySpan<char>>();
            Phrases = phrases.ToFrozenDictionary(
                pair => pair.Key,
                pair => pair.Value.OrderByDescending(one => one.Words.Length).ToArray(),
                StringComparer.Ordinal);
            Lengths = [.. words.Keys.Select(word => word.Length).Distinct().OrderDescending()];
        }

        internal IReadOnlyList<Concept> Concepts { get; }

        internal FrozenDictionary<string, Concept> ByKey { get; }

        internal FrozenDictionary<string, IReadOnlyList<string>> Closure { get; }

        private Dictionary<string, string>.AlternateLookup<ReadOnlySpan<char>> WordLookup { get; }

        /// <summary>Every multi-word form, by its first word, longest first.</summary>
        private FrozenDictionary<string, (string[] Words, string Key)[]> Phrases { get; }

        private int[] Lengths { get; }

        /// <summary>
        /// Finds the concepts in one folded text.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Phrases first, then words, and within a word the longest form
        /// first: the form that is found consumes its letters, so
        /// <em>Kokosmilch</em> is coconut milk and never also milk.
        /// </para>
        /// <para>
        /// Where in a word a form may be found depends on how long it is,
        /// because a short form found anywhere is found everywhere. Five
        /// letters or more may sit anywhere in a compound — <em>Hähnchen</em>
        /// in <em>Hähnchenbrustfilet</em>, <em>Tomate</em> in
        /// <em>Kirschtomaten</em>. Four may begin or end one — <em>Reis</em> in
        /// <em>Basmatireis</em>, <em>Rind</em> in <em>Rinderhack</em> — but not
        /// sit inside it, which is what stops <em>Ente</em> being found in
        /// <em>Studentenfutter</em>. Three or fewer must be the whole word:
        /// <em>Eis</em> is ice cream, <em>Eisbein</em> is not.
        /// </para>
        /// <para>
        /// The last word of a phrase may carry an ending, so
        /// <em>sweet potatoes</em> is still a sweet potato.
        /// </para>
        /// <para>
        /// <paramref name="whole"/> is how a query is read: a word names a
        /// concept only when a form begins it and leaves at most
        /// <see cref="Ending"/> letters over.
        /// </para>
        /// </remarks>
        internal void Match(string folded, HashSet<string> found, bool whole)
        {
            if (folded.Length == 0)
            {
                return;
            }

            var words = folded.Split(' ');
            var consumed = new bool[words.Length];

            for (var at = 0; at < words.Length; at++)
            {
                if (!Phrases.TryGetValue(words[at], out var candidates))
                {
                    continue;
                }

                foreach (var (phrase, key) in candidates)
                {
                    if (PhraseAt(words, consumed, at, phrase))
                    {
                        found.Add(key);
                        Array.Fill(consumed, true, at, phrase.Length);

                        break;
                    }
                }
            }

            for (var at = 0; at < words.Length; at++)
            {
                if (!consumed[at])
                {
                    MatchWord(words[at], found, whole);
                }
            }
        }

        internal string? Whole(string folded)
        {
            if (folded.Length == 0)
            {
                return null;
            }

            var words = folded.Split(' ');

            if (words.Length == 1)
            {
                var found = new HashSet<string>(StringComparer.Ordinal);
                MatchWord(words[0], found, whole: true);

                return found.Count == 1 ? found.First() : null;
            }

            return Phrases.TryGetValue(words[0], out var candidates)
                ? candidates.FirstOrDefault(one =>
                    one.Words.Length == words.Length
                    && PhraseAt(words, new bool[words.Length], 0, one.Words)
                    && words[^1].Length - one.Words[^1].Length <= Ending).Key
                : null;
        }

        private static bool PhraseAt(string[] words, bool[] consumed, int at, string[] phrase)
        {
            if (at + phrase.Length > words.Length)
            {
                return false;
            }

            for (var offset = 0; offset < phrase.Length; offset++)
            {
                var word = words[at + offset];
                var last = offset == phrase.Length - 1;

                if (consumed[at + offset]
                    || (last ? !word.StartsWith(phrase[offset], StringComparison.Ordinal) : word != phrase[offset]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// How many letters a query word may carry past a form and still be
        /// that form: <em>italienisch</em>+<em>en</em>, <em>Tomate</em>+<em>n</em>.
        /// </summary>
        private const int Ending = 2;

        private void MatchWord(string word, HashSet<string> found, bool whole)
        {
            var taken = new bool[word.Length];

            foreach (var length in Lengths)
            {
                for (var start = 0; start + length <= word.Length; start++)
                {
                    if (whole && (start > 0 || word.Length - length > Ending || taken.Contains(true)))
                    {
                        break;
                    }

                    if (!Allowed(word.Length, start, length)
                        || Array.IndexOf(taken, true, start, length) >= 0
                        || !WordLookup.TryGetValue(word.AsSpan(start, length), out var key))
                    {
                        continue;
                    }

                    found.Add(key);
                    Array.Fill(taken, true, start, length);
                }
            }
        }

        private static bool Allowed(int wordLength, int start, int length) => length switch
        {
            >= 5 => true,
            4 => start == 0 || start + length == wordLength,
            _ => start == 0 && length == wordLength
        };

        private List<string> Ancestry(string key)
        {
            var lineage = new List<string>();
            var pending = new Queue<string>([key]);

            while (pending.TryDequeue(out var next))
            {
                if (lineage.Contains(next, StringComparer.Ordinal))
                {
                    continue;
                }

                lineage.Add(next);

                foreach (var parent in ByKey.TryGetValue(next, out var concept) ? concept.Parents : [])
                {
                    pending.Enqueue(parent);
                }
            }

            return lineage;
        }
    }

    private static Concept Ingredient(string key, string[] de, string[] en, params string[] parents) =>
        new(key, ConceptKind.Ingredient, de, en, parents);

    private static Concept Dish(string key, string[] de, string[] en, params string[] parents) =>
        new(key, ConceptKind.Dish, de, en, parents);

    private static Concept Cuisine(string key, string[] de, string[] en, params string[] parents) =>
        new(key, ConceptKind.Cuisine, de, en, parents);

    private static Concept Meal(string key, string[] de, string[] en, params string[] parents) =>
        new(key, ConceptKind.Meal, de, en, parents);

    private static Concept Method(string key, string[] de, string[] en) =>
        new(key, ConceptKind.Method, de, en, []);

    private static Concept Diet(string key, string[] de, string[] en, params string[] parents) =>
        new(key, ConceptKind.Diet, de, en, parents);

    private static Concept Character(string key, string[] de, string[] en, params string[] parents) =>
        new(key, ConceptKind.Character, de, en, parents);

    private static Concept[] Entries() =>
    [
        // ── Families: what the rest are kinds of ─────────────────────────────
        Ingredient("meat", ["Fleisch"], ["meat"]),
        Ingredient("poultry", ["Geflügel"], ["poultry"], "meat"),
        Ingredient("fish", ["Fisch", "Fische"], ["fish"]),
        Ingredient("seafood", ["Meeresfrüchte"], ["seafood", "shellfish"]),
        Ingredient("animal_product", ["tierisches Produkt"], ["animal product"]),
        Ingredient("dairy", ["Milchprodukte", "Molkereiprodukte"], ["dairy"]),
        Ingredient("cheese", ["Käse", "Bergkäse", "Emmentaler", "Gouda", "Cheddar", "Gruyère", "Raclettekäse"],
            ["cheese", "cheddar", "gruyere"], "dairy"),
        Ingredient("egg", ["Ei", "Eier", "Eigelb", "Eiweiß", "Eidotter"], ["egg", "eggs", "egg yolk", "egg white"]),
        Ingredient("vegetable", ["Gemüse", "Suppengrün"], ["vegetable", "vegetables", "veg", "veggies"]),
        Ingredient("fruit", ["Obst", "Früchte", "Frucht"], ["fruit", "fruits"]),
        Ingredient("berry", ["Beeren", "Beere", "Beerenmix"], ["berry", "berries"], "fruit"),
        Ingredient("herb", ["Kräuter", "Wildkräuter"], ["herb", "herbs"]),
        Ingredient("spice", ["Gewürz", "Gewürze"], ["spice", "spices"]),
        Ingredient("grain", ["Getreide"], ["grain", "grains"]),
        Ingredient("legume", ["Hülsenfrüchte"], ["legume", "legumes", "pulses"]),
        Ingredient("nut", ["Nuss", "Nüsse", "Nusskerne", "Studentenfutter"], ["nut", "nuts"]),
        Ingredient("seed", ["Sonnenblumenkerne", "Kürbiskerne", "Sesam", "Leinsamen", "Chiasamen"],
            ["seed", "seeds", "sesame", "chia", "flaxseed"]),
        Ingredient("mushroom", ["Pilz", "Pilze", "Champignon", "Champignons", "Steinpilze", "Pfifferlinge", "Shiitake"],
            ["mushroom", "mushrooms", "chanterelles", "porcini"], "vegetable"),

        // ── Meat ─────────────────────────────────────────────────────────────
        Ingredient("beef", ["Rind", "Rindfleisch", "Rinder", "Rumpsteak", "Entrecôte", "Tafelspitz"],
            ["beef", "sirloin", "brisket"], "meat"),
        Ingredient("pork", ["Schwein", "Schweinefleisch", "Schweine", "Kotelett", "Schäufele", "Eisbein", "Haxe"],
            ["pork", "pork belly", "pork chop"], "meat"),
        Ingredient("mince", ["Hackfleisch", "Hack", "Gehacktes", "Mett", "Hackepeter"],
            ["mince", "minced meat", "ground beef", "ground meat", "ground pork"], "meat"),
        Ingredient("lamb", ["Lamm", "Lammfleisch"], ["lamb", "mutton"], "meat"),
        Ingredient("veal", ["Kalb", "Kalbfleisch"], ["veal"], "meat"),
        Ingredient("game", ["Wild", "Wildschwein", "Hirsch", "Rehrücken", "Rehkeule", "Wildbret"],
            ["venison", "game"], "meat"),
        // "Gockel" and "Hahn" are what some people call it, not a kind of it:
        // as a concept beneath chicken, nothing ever answered to them.
        Ingredient("chicken",
            ["Hähnchen", "Hühnchen", "Huhn", "Hühner", "Hendl", "Poulet", "Poularde", "Suppenhuhn", "Gockel", "Hahn"],
            ["chicken", "drumsticks", "rooster", "cockerel"], "poultry"),
        Ingredient("turkey", ["Pute", "Puten", "Puter", "Truthahn"], ["turkey"], "poultry"),
        Ingredient("duck", ["Ente"], ["duck"], "poultry"),
        Ingredient("goose", ["Gans", "Gänse"], ["goose"], "poultry"),
        Ingredient("bacon", ["Speck", "Bacon", "Pancetta", "Frühstücksspeck", "Guanciale"],
            ["bacon", "pancetta", "lardons"], "pork"),
        Ingredient("ham", ["Schinken", "Prosciutto", "Serrano"], ["ham", "prosciutto"], "pork"),
        Ingredient("sausage",
            ["Wurst", "Würstchen", "Würste", "Chorizo", "Salami", "Leberkäse", "Fleischkäse", "Cabanossi"],
            ["sausage", "sausages", "chorizo", "salami", "hot dog", "pepperoni"], "meat"),

        // ── Fish and seafood ─────────────────────────────────────────────────
        Ingredient("salmon", ["Lachs", "Wildlachs"], ["salmon"], "fish"),
        Ingredient("tuna", ["Thunfisch"], ["tuna"], "fish"),
        Ingredient("cod", ["Kabeljau", "Dorsch", "Skrei"], ["cod"], "fish"),
        Ingredient("trout", ["Forelle", "Saibling"], ["trout"], "fish"),
        Ingredient("white_fish", ["Seelachs", "Pangasius", "Zander", "Scholle", "Rotbarsch", "Dorade", "Wolfsbarsch"],
            ["pollock", "hake", "haddock", "plaice", "sea bass", "sea bream"], "fish"),
        Ingredient("herring", ["Hering", "Matjes", "Rollmops"], ["herring"], "fish"),
        Ingredient("anchovy", ["Sardelle", "Sardellen", "Anchovis"], ["anchovy", "anchovies"], "fish"),
        Ingredient("sardine", ["Sardine", "Sardinen"], ["sardine", "sardines"], "fish"),
        Ingredient("prawn", ["Garnele", "Garnelen", "Shrimps", "Krabben", "Scampi", "Gambas", "Krebs", "Hummer"],
            ["prawn", "prawns", "shrimp", "scampi", "crab", "lobster"], "seafood"),
        Ingredient("mussel", ["Muschel", "Muscheln", "Miesmuscheln", "Venusmuscheln", "Jakobsmuscheln"],
            ["mussel", "mussels", "clam", "clams", "scallops", "oysters"], "seafood"),
        Ingredient("squid", ["Tintenfisch", "Calamari", "Oktopus", "Pulpo"], ["squid", "calamari", "octopus"], "seafood"),

        // ── Vegetables ───────────────────────────────────────────────────────
        Ingredient("tomato",
            ["Tomate", "Tomaten", "Cherrytomaten", "Cocktailtomaten", "passierte Tomaten", "Passata", "Tomatenmark", "Pelati"],
            ["tomato", "tomatoes", "cherry tomatoes", "passata", "tomato paste", "tinned tomatoes", "canned tomatoes"],
            "vegetable"),
        Ingredient("potato", ["Kartoffel", "Kartoffeln", "Erdäpfel", "Erdapfel", "Drillinge"],
            ["potato", "potatoes", "spud"], "vegetable"),
        Ingredient("sweet_potato", ["Süßkartoffel", "Süßkartoffeln", "Batate"], ["sweet potato", "yam"], "vegetable"),
        Ingredient("onion", ["Zwiebel", "Zwiebeln", "Schalotte", "Schalotten", "Lauchzwiebel", "Lauchzwiebeln"],
            ["onion", "onions", "shallot", "shallots", "spring onion", "scallion", "green onion"], "vegetable"),
        Ingredient("garlic", ["Knoblauch", "Knoblauchzehe", "Knofi"], ["garlic"], "vegetable"),
        Ingredient("carrot", ["Karotte", "Karotten", "Möhre", "Möhren", "Mohrrübe", "Rüebli"],
            ["carrot", "carrots"], "vegetable"),
        Ingredient("bell_pepper", ["Paprika", "Paprikaschote", "Spitzpaprika"],
            ["bell pepper", "bell peppers", "red pepper", "capsicum"], "vegetable"),
        Ingredient("chili", ["Chili", "Chilischote", "Peperoni", "Peperoncino", "Jalapeño", "Chiliflocken"],
            ["chili", "chilli", "chillies", "jalapeno", "chili flakes"], "vegetable", "spicy"),
        Ingredient("cucumber", ["Gurke", "Gurken", "Salatgurke", "Gewürzgurke", "Gewürzgurken"],
            ["cucumber", "gherkin", "gherkins", "pickles"], "vegetable"),
        Ingredient("zucchini", ["Zucchini", "Zucchetti"], ["zucchini", "courgette", "courgettes"], "vegetable"),
        Ingredient("aubergine", ["Aubergine", "Auberginen", "Melanzani"], ["aubergine", "eggplant"], "vegetable"),
        Ingredient("chard", ["Mangold"], ["chard", "swiss chard"], "vegetable"),
        Ingredient("spinach", ["Spinat", "Blattspinat", "Babyspinat"], ["spinach"], "vegetable"),
        Ingredient("lettuce", ["Kopfsalat", "Eisbergsalat", "Rucola", "Feldsalat", "Römersalat", "Blattsalat", "Endivie"],
            ["lettuce", "rocket", "arugula", "romaine", "iceberg"], "vegetable"),
        Ingredient("cabbage",
            ["Kohl", "Kraut", "Sauerkraut", "Wirsing", "Chinakohl", "Pak Choi"],
            ["cabbage", "sauerkraut", "savoy", "bok choy", "pak choi"], "vegetable"),
        Ingredient("cauliflower", ["Blumenkohl", "Karfiol"], ["cauliflower"], "vegetable"),
        Ingredient("broccoli", ["Brokkoli", "Broccoli"], ["broccoli", "broccolini"], "vegetable"),
        Ingredient("brussels_sprouts", ["Rosenkohl"], ["brussels sprouts", "sprouts"], "vegetable"),
        Ingredient("kale", ["Grünkohl"], ["kale"], "vegetable"),
        Ingredient("kohlrabi", ["Kohlrabi"], ["kohlrabi"], "vegetable"),
        Ingredient("leek", ["Lauch", "Porree"], ["leek", "leeks"], "vegetable"),
        Ingredient("celery", ["Sellerie", "Staudensellerie", "Knollensellerie"], ["celery", "celeriac"], "vegetable"),
        Ingredient("pumpkin", ["Kürbis", "Hokkaido", "Butternut"], ["pumpkin", "squash", "butternut"], "vegetable"),
        Ingredient("asparagus", ["Spargel"], ["asparagus"], "vegetable"),
        Ingredient("pea", ["Erbse", "Erbsen", "Zuckerschoten", "Kaiserschoten"],
            ["pea", "peas", "snow peas", "sugar snaps", "mangetout"], "vegetable"),
        Ingredient("green_beans", ["grüne Bohnen", "Brechbohnen", "Buschbohnen", "Stangenbohnen", "Keniabohnen"],
            ["green beans", "french beans", "runner beans"], "vegetable"),
        Ingredient("corn", ["Mais", "Maiskolben", "Zuckermais"], ["corn", "sweetcorn", "maize", "corn on the cob"],
            "vegetable"),
        Ingredient("beetroot", ["Rote Bete", "Rote Beete", "Randen"], ["beetroot", "beet", "beets"], "vegetable"),
        Ingredient("radish", ["Radieschen", "Rettich"], ["radish", "radishes"], "vegetable"),
        Ingredient("fennel", ["Fenchel"], ["fennel"], "vegetable"),
        Ingredient("parsnip", ["Pastinake", "Pastinaken", "Petersilienwurzel"], ["parsnip", "parsnips"], "vegetable"),
        Ingredient("artichoke", ["Artischocke", "Artischocken"], ["artichoke", "artichokes"], "vegetable"),
        Ingredient("olive", ["Olive", "Oliven"], ["olive", "olives"], "vegetable"),
        Ingredient("avocado", ["Avocado", "Avocados"], ["avocado", "avocados"], "fruit"),

        // ── Fruit ────────────────────────────────────────────────────────────
        Ingredient("apple", ["Apfel", "Äpfel"], ["apple", "apples"], "fruit"),
        Ingredient("pear", ["Birne", "Birnen"], ["pear", "pears"], "fruit"),
        Ingredient("banana", ["Banane", "Bananen"], ["banana", "bananas"], "fruit"),
        Ingredient("lemon", ["Zitrone", "Zitronen"], ["lemon", "lemons"], "fruit"),
        Ingredient("lime", ["Limette", "Limetten"], ["lime", "limes"], "fruit"),
        Ingredient("orange", ["Orange", "Orangen", "Apfelsine", "Blutorange", "Mandarine", "Clementine"],
            ["orange", "oranges", "clementine", "tangerine"], "fruit"),
        Ingredient("strawberry", ["Erdbeere", "Erdbeeren"], ["strawberry", "strawberries"], "berry"),
        Ingredient("raspberry", ["Himbeere", "Himbeeren"], ["raspberry", "raspberries"], "berry"),
        Ingredient("blueberry", ["Heidelbeere", "Heidelbeeren", "Blaubeere", "Blaubeeren"],
            ["blueberry", "blueberries"], "berry"),
        Ingredient("currant", ["Johannisbeere", "Johannisbeeren", "Stachelbeere"], ["currant", "currants", "gooseberry"],
            "berry"),
        Ingredient("cherry", ["Kirsche", "Kirschen", "Sauerkirschen"], ["cherry", "cherries"], "fruit"),
        Ingredient("plum", ["Pflaume", "Pflaumen", "Zwetschge", "Zwetschgen", "Zwetschke"], ["plum", "plums"], "fruit"),
        Ingredient("apricot", ["Aprikose", "Aprikosen", "Marille", "Marillen"], ["apricot", "apricots"], "fruit"),
        Ingredient("peach", ["Pfirsich", "Pfirsiche", "Nektarine"], ["peach", "peaches", "nectarine"], "fruit"),
        Ingredient("mango", ["Mango", "Mangos"], ["mango", "mangoes"], "fruit"),
        Ingredient("pineapple", ["Ananas"], ["pineapple"], "fruit"),
        Ingredient("grape", ["Traube", "Trauben", "Weintrauben"], ["grape", "grapes"], "fruit"),
        Ingredient("rhubarb", ["Rhabarber"], ["rhubarb"], "fruit"),
        Ingredient("melon", ["Melone", "Wassermelone", "Honigmelone"], ["melon", "watermelon"], "fruit"),
        Ingredient("kiwi", ["Kiwi", "Kiwis"], ["kiwi", "kiwis"], "fruit"),
        Ingredient("pomegranate", ["Granatapfel", "Granatapfelkerne"], ["pomegranate"], "fruit"),
        Ingredient("fig", ["Feige", "Feigen"], ["fig", "figs"], "fruit"),
        Ingredient("date", ["Datteln", "Dattel"], ["dates"], "fruit"),
        Ingredient("raisin", ["Rosine", "Rosinen", "Sultaninen"], ["raisin", "raisins", "sultanas"], "fruit"),
        Ingredient("coconut", ["Kokos", "Kokosnuss", "Kokosraspeln"], ["coconut", "desiccated coconut"], "fruit"),
        Ingredient("coconut_milk", ["Kokosmilch", "Kokoscreme"], ["coconut milk", "coconut cream"], "coconut"),

        // ── Herbs and spices ─────────────────────────────────────────────────
        Ingredient("basil", ["Basilikum"], ["basil"], "herb"),
        Ingredient("parsley", ["Petersilie"], ["parsley"], "herb"),
        Ingredient("coriander", ["Koriander"], ["coriander", "cilantro"], "herb"),
        Ingredient("dill", ["Dill"], ["dill"], "herb"),
        Ingredient("chives", ["Schnittlauch"], ["chives"], "herb"),
        Ingredient("mint", ["Minze", "Pfefferminze"], ["mint", "peppermint"], "herb"),
        Ingredient("thyme", ["Thymian"], ["thyme"], "herb"),
        Ingredient("rosemary", ["Rosmarin"], ["rosemary"], "herb"),
        Ingredient("oregano", ["Oregano", "Majoran"], ["oregano", "marjoram"], "herb"),
        Ingredient("sage", ["Salbei"], ["sage"], "herb"),
        Ingredient("wild_garlic", ["Bärlauch"], ["wild garlic", "ramsons"], "herb"),
        Ingredient("lemongrass", ["Zitronengras"], ["lemongrass"], "herb"),
        Ingredient("ginger", ["Ingwer"], ["ginger"], "spice"),
        Ingredient("black_pepper", ["Pfeffer", "Pfefferkörner"], ["black pepper", "peppercorn", "peppercorns"], "spice"),
        Ingredient("cinnamon", ["Zimt", "Zimtstange"], ["cinnamon"], "spice"),
        Ingredient("cumin", ["Kreuzkümmel", "Cumin", "Kümmel"], ["cumin", "caraway"], "spice"),
        Ingredient("paprika_powder", ["Paprikapulver", "Rosenpaprika", "Paprika edelsüß", "geräuchertes Paprikapulver"],
            ["paprika powder", "smoked paprika", "sweet paprika"], "spice"),
        Ingredient("curry_powder", ["Currypulver", "Currypaste", "Garam Masala"],
            ["curry powder", "curry paste", "garam masala"], "spice"),
        Ingredient("nutmeg", ["Muskat", "Muskatnuss"], ["nutmeg"], "spice"),
        Ingredient("turmeric", ["Kurkuma"], ["turmeric"], "spice"),
        Ingredient("vanilla", ["Vanille", "Vanilleschote", "Vanillezucker"], ["vanilla"], "spice"),
        Ingredient("saffron", ["Safran"], ["saffron"], "spice"),

        // ── Dairy and eggs ───────────────────────────────────────────────────
        Ingredient("milk", ["Milch", "Vollmilch", "Buttermilch"], ["milk", "buttermilk"], "dairy"),
        Ingredient("plant_milk", ["Mandelmilch", "Hafermilch", "Sojamilch", "Pflanzenmilch", "Haferdrink"],
            ["almond milk", "oat milk", "soy milk", "plant milk"]),
        Ingredient("cream",
            ["Sahne", "Schlagsahne", "Schlagobers", "Rahm", "Crème fraîche", "Schmand", "saure Sahne", "Sauerrahm"],
            ["cream", "double cream", "heavy cream", "whipping cream", "sour cream", "creme fraiche"], "dairy"),
        Ingredient("butter", ["Butter", "Butterschmalz"], ["butter", "ghee"], "dairy"),
        Ingredient("yoghurt", ["Joghurt", "Jogurt", "Naturjoghurt"], ["yoghurt", "yogurt"], "dairy"),
        Ingredient("quark", ["Quark", "Topfen", "Skyr", "Hüttenkäse"], ["quark", "cottage cheese", "curd"], "dairy"),
        Ingredient("parmesan", ["Parmesan", "Parmigiano", "Grana Padano", "Pecorino"], ["parmesan", "pecorino"],
            "cheese", "animal_product"),
        Ingredient("mozzarella", ["Mozzarella", "Burrata"], ["mozzarella", "burrata"], "cheese"),
        Ingredient("feta", ["Feta", "Hirtenkäse", "Schafskäse"], ["feta"], "cheese"),
        Ingredient("cream_cheese", ["Frischkäse", "Mascarpone", "Ricotta"], ["cream cheese", "mascarpone", "ricotta"],
            "cheese"),
        Ingredient("halloumi", ["Halloumi", "Grillkäse"], ["halloumi"], "cheese"),

        // ── Grains, pasta and bread ──────────────────────────────────────────
        Ingredient("rice",
            ["Reis", "Basmati", "Basmatireis", "Jasminreis", "Risottoreis", "Langkornreis", "Wildreis", "Arborio"],
            ["rice", "basmati", "jasmine rice", "arborio", "brown rice", "wild rice"], "grain"),
        Ingredient("pasta",
            ["Pasta", "Nudel", "Nudeln", "Spaghetti", "Penne", "Fusilli", "Tagliatelle", "Makkaroni", "Maccaroni",
             "Rigatoni", "Farfalle", "Linguine", "Orzo", "Tortellini", "Tortelloni", "Ravioli", "Lasagneplatten",
             "Bandnudeln", "Spirelli", "Hörnchennudeln", "Muschelnudeln"],
            ["pasta", "spaghetti", "penne", "macaroni", "tagliatelle", "ravioli", "tortellini"]),
        Ingredient("noodles", ["Mie-Nudeln", "Mienudeln", "Glasnudeln", "Reisnudeln", "Udon", "Ramen", "Soba", "Eiernudeln"],
            ["noodles", "rice noodles", "egg noodles", "udon", "ramen", "soba"], "pasta", "asian"),
        Ingredient("spaetzle", ["Spätzle", "Knöpfle"], ["spaetzle"], "pasta"),
        Ingredient("bread",
            ["Brot", "Brötchen", "Baguette", "Ciabatta", "Toast", "Toastbrot", "Fladenbrot", "Pita", "Sauerteig",
             "Semmel", "Semmeln", "Schrippe", "Brezel", "Laugenbrezel", "Knäckebrot"],
            ["bread", "baguette", "toast", "sourdough", "pita", "flatbread", "bun", "buns", "bagel"], "grain"),
        Ingredient("flour", ["Mehl", "Weizenmehl", "Dinkelmehl", "Roggenmehl", "Vollkornmehl"],
            ["flour", "plain flour", "wholemeal flour"], "grain"),
        Ingredient("oats", ["Haferflocken", "Hafer"], ["oats", "rolled oats", "oatmeal"], "grain"),
        Ingredient("couscous", ["Couscous", "Bulgur", "Quinoa", "Graupen", "Hirse"],
            ["couscous", "bulgur", "quinoa", "pearl barley", "millet"], "grain"),
        Ingredient("polenta", ["Polenta", "Maisgrieß", "Grieß"], ["polenta", "semolina", "grits"], "grain"),
        Ingredient("dough", ["Teig", "Blätterteig", "Mürbeteig", "Hefeteig", "Pizzateig", "Strudelteig", "Filoteig"],
            ["dough", "pastry", "puff pastry", "shortcrust", "filo"]),

        // ── Legumes, nuts, soy ───────────────────────────────────────────────
        Ingredient("lentil", ["Linse", "Linsen", "Belugalinsen"], ["lentil", "lentils"], "legume"),
        Ingredient("chickpea", ["Kichererbse", "Kichererbsen"], ["chickpea", "chickpeas", "garbanzo"], "legume"),
        Ingredient("bean", ["Bohne", "Bohnen", "Kidneybohnen", "Kidney", "weiße Bohnen", "schwarze Bohnen", "Edamame"],
            ["bean", "beans", "kidney beans", "black beans", "cannellini", "edamame"], "legume"),
        Ingredient("tofu", ["Tofu", "Räuchertofu", "Seidentofu", "Tempeh"], ["tofu", "tempeh"], "soy"),
        Ingredient("soy", ["Soja", "Sojabohnen", "Miso"], ["soy", "soya", "miso"], "legume"),
        Ingredient("almond", ["Mandel", "Mandeln"], ["almond", "almonds"], "nut"),
        Ingredient("hazelnut", ["Haselnuss", "Haselnüsse"], ["hazelnut", "hazelnuts"], "nut"),
        Ingredient("walnut", ["Walnuss", "Walnüsse"], ["walnut", "walnuts", "pecan", "pecans"], "nut"),
        Ingredient("cashew", ["Cashew", "Cashewkerne", "Pistazie", "Pistazien"], ["cashew", "cashews", "pistachio"], "nut"),
        Ingredient("peanut", ["Erdnuss", "Erdnüsse"], ["peanut", "peanuts"], "nut"),
        Ingredient("peanut_butter", ["Erdnussbutter", "Erdnussmus"], ["peanut butter"], "peanut"),
        Ingredient("pine_nut", ["Pinienkerne"], ["pine nuts"], "nut"),

        // ── The rest of a cupboard ───────────────────────────────────────────
        Ingredient("chocolate", ["Schokolade", "Schoko", "Kuvertüre", "Zartbitter", "Nougat"],
            ["chocolate", "dark chocolate", "choc"], "sweet"),
        Ingredient("cocoa", ["Kakao", "Kakaopulver"], ["cocoa"]),
        Ingredient("sugar", ["Zucker", "Puderzucker", "Rohrzucker", "Ahornsirup", "Sirup"],
            ["sugar", "icing sugar", "brown sugar", "maple syrup", "syrup"]),
        Ingredient("honey", ["Honig"], ["honey"], "animal_product"),
        Ingredient("yeast", ["Hefe", "Trockenhefe"], ["yeast"]),
        Ingredient("gelatine", ["Gelatine", "Blattgelatine"], ["gelatine", "gelatin"], "animal_product"),
        Ingredient("stock", ["Brühe", "Fond", "Bouillon"], ["stock", "broth", "bouillon"]),
        Ingredient("soy_sauce", ["Sojasauce", "Sojasoße", "Shoyu", "Tamari"], ["soy sauce", "tamari"], "soy"),
        Ingredient("vinegar", ["Essig", "Balsamico"], ["vinegar", "balsamic"]),
        Ingredient("mustard", ["Senf", "Dijonsenf"], ["mustard", "dijon"]),
        Ingredient("mayonnaise", ["Mayonnaise", "Mayo", "Aioli", "Remoulade"], ["mayonnaise", "mayo", "aioli"], "egg"),
        Ingredient("capers", ["Kapern"], ["capers"]),
        Ingredient("wine", ["Wein", "Rotwein", "Weißwein", "Sekt"], ["wine", "red wine", "white wine", "prosecco"]),
        Ingredient("beer", ["Bier", "Weißbier", "Pils"], ["beer", "ale", "lager", "stout"]),

        // ── Dishes ───────────────────────────────────────────────────────────
        Dish("sauce", ["Soße", "Sauce", "Soßen", "Dip", "Dressing"], ["sauce", "dip", "gravy", "dressing"]),
        Dish("pasta_sauce", ["Nudelsoße", "Nudelsauce", "Pastasauce", "Pastasoße"], ["pasta sauce"], "sauce"),
        Dish("tomato_sauce", ["Tomatensoße", "Tomatensauce", "Sugo"], ["tomato sauce", "marinara"], "tomato", "sauce"),
        Dish("bolognese", ["Bolognese", "Bolognaise"], ["bolognese"], "pasta_sauce", "italian"),
        Dish("pesto", ["Pesto"], ["pesto"], "sauce", "italian"),
        Dish("carbonara", ["Carbonara"], ["carbonara"], "pasta", "italian"),
        Dish("lasagne", ["Lasagne", "Lasagna"], ["lasagne", "lasagna"], "pasta", "casserole", "italian"),
        Dish("pizza", ["Pizza", "Calzone"], ["pizza", "calzone"], "italian", "baked"),
        Dish("risotto", ["Risotto"], ["risotto"], "rice", "italian"),
        Dish("gnocchi", ["Gnocchi"], ["gnocchi"], "potato", "italian"),
        Dish("tiramisu", ["Tiramisu"], ["tiramisu"], "dessert", "italian"),
        Dish("minestrone", ["Minestrone"], ["minestrone"], "soup", "italian"),
        Dish("soup", ["Suppe", "Suppen", "Cremesuppe", "Brühe mit Einlage"], ["soup", "soups", "chowder"], "warm"),
        Dish("stew", ["Eintopf", "Ragout", "Ragù", "Ragu", "Schmortopf"], ["stew", "ragout", "hotpot"], "warm", "hearty"),
        Dish("goulash", ["Gulasch"], ["goulash"], "stew"),
        Dish("chili_con_carne", ["Chili con Carne", "Chili sin Carne"], ["chili con carne", "chilli con carne"],
            "stew", "mexican", "spicy"),
        Dish("curry", ["Curry", "Currys"], ["curry", "curries"], "asian", "warm"),
        Dish("dal", ["Dal", "Dhal", "Daal"], ["dal", "dhal"], "lentil", "indian"),
        Dish("casserole", ["Auflauf", "Aufläufe", "Gratin", "überbacken"], ["casserole", "gratin", "bake"],
            "baked", "warm"),
        Dish("roast", ["Braten", "Sonntagsbraten", "Krustenbraten"], ["roast", "sunday roast"], "warm", "hearty"),
        Dish("meatloaf", ["Hackbraten"], ["meatloaf"], "mince"),
        Dish("meatballs", ["Frikadelle", "Frikadellen", "Buletten", "Fleischpflanzerl", "Köttbullar", "Hackbällchen",
                "Fleischbällchen", "Klopse"],
            ["meatballs", "meatball", "patties"], "mince"),
        Dish("schnitzel", ["Schnitzel", "Cordon bleu"], ["schnitzel", "escalope"], "warm"),
        Dish("roulade", ["Rouladen", "Roulade"], ["roulade", "rouladen"], "german", "warm"),
        Dish("steak", ["Steak", "Steaks"], ["steak", "steaks"]),
        Dish("burger", ["Burger", "Hamburger", "Cheeseburger"], ["burger", "burgers", "hamburger"], "american"),
        Dish("tex_mex",
            ["Tacos", "Taco", "Burrito", "Burritos", "Enchiladas", "Quesadilla", "Quesadillas", "Fajitas", "Nachos"],
            ["taco", "tacos", "burrito", "enchilada", "enchiladas", "quesadilla", "fajita", "fajitas", "nachos"],
            "mexican"),
        Dish("guacamole", ["Guacamole"], ["guacamole"], "avocado", "mexican"),
        Dish("stir_fry", ["Wok", "Wokgemüse", "gebratene Nudeln", "Pfannengemüse"], ["stir fry", "stir fried"], "asian"),
        Dish("pan_dish", ["Pfanne", "Pfannengericht"], ["skillet", "traybake"], "warm"),
        Dish("fried_rice", ["gebratener Reis", "Bratreis", "Nasi Goreng"], ["fried rice", "nasi goreng"], "rice", "asian"),
        Dish("pad_thai", ["Pad Thai"], ["pad thai"], "noodles", "thai"),
        Dish("pho", ["Pho"], ["pho"], "soup", "vietnamese"),
        Dish("sushi", ["Sushi", "Maki", "Onigiri"], ["sushi", "maki", "onigiri"], "rice", "japanese"),
        Dish("spring_rolls", ["Frühlingsrollen", "Sommerrollen"], ["spring rolls", "summer rolls"], "asian"),
        Dish("asian_dumplings", ["Gyoza", "Dim Sum", "Wan Tan", "Wantan", "Jiaozi"], ["gyoza", "dim sum", "wontons",
            "dumplings"], "asian"),
        Dish("kimchi", ["Kimchi", "Bibimbap"], ["kimchi", "bibimbap"], "korean"),
        Dish("indian_dish", ["Tikka", "Masala", "Biryani", "Korma", "Tandoori", "Vindaloo", "Naan", "Chutney"],
            ["tikka", "masala", "biryani", "korma", "tandoori", "vindaloo", "naan", "chutney"], "indian"),
        Dish("paella", ["Paella"], ["paella"], "rice", "spanish"),
        Dish("tapas", ["Tapas", "Gazpacho"], ["tapas", "gazpacho"], "spanish"),
        Dish("greek_dish", ["Gyros", "Souvlaki", "Moussaka", "Tzatziki", "Bifteki"],
            ["gyros", "souvlaki", "moussaka", "tzatziki"], "greek"),
        Dish("mezze", ["Falafel", "Hummus", "Humus", "Tabouleh", "Taboulé", "Baba Ganoush", "Mezze"],
            ["falafel", "hummus", "tabbouleh", "baba ganoush", "mezze"], "middle_eastern"),
        Dish("shakshuka", ["Shakshuka", "Schakschuka"], ["shakshuka"], "egg", "breakfast", "middle_eastern"),
        Dish("ratatouille", ["Ratatouille"], ["ratatouille"], "vegetable", "french"),
        Dish("quiche", ["Quiche", "Tarte", "Tartes"], ["quiche", "tart", "tarts"], "baked"),
        Dish("flammkuchen", ["Flammkuchen", "Tarte flambée"], ["tarte flambee"], "baked", "german"),
        Dish("onion_tart", ["Zwiebelkuchen"], ["onion tart"], "onion", "baked", "german"),
        Dish("pie", ["Pastete", "Pie"], ["pie", "pies"], "baked"),
        Dish("fondue", ["Fondue", "Käsefondue", "Raclette"], ["fondue", "raclette"], "cheese"),
        Dish("salad", ["Salat", "Salate", "Salatbowl"], ["salad", "salads", "slaw", "coleslaw"], "light"),
        Dish("sandwich", ["Sandwich", "Stulle", "belegtes Brot", "Wrap", "Wraps", "Panini"],
            ["sandwich", "sandwiches", "wrap", "wraps", "panini", "toastie"], "bread"),
        Dish("mash", ["Kartoffelpüree", "Kartoffelbrei", "Kartoffelstampf", "Stampf"],
            ["mash", "mashed potatoes"], "potato", "side"),
        Dish("fries", ["Pommes", "Pommes frites", "Fritten", "Wedges", "Kartoffelspalten", "Ofenkartoffeln"],
            ["fries", "french fries", "wedges", "chips"], "potato", "side"),
        Dish("roesti", ["Rösti", "Kartoffelpuffer", "Reibekuchen", "Reiberdatschi", "Bratkartoffeln"],
            ["rosti", "hash browns", "potato pancakes", "fried potatoes"], "potato"),
        Dish("dumplings", ["Knödel", "Klöße", "Kloß", "Semmelknödel", "Serviettenknödel", "Schupfnudeln"],
            ["german dumplings", "bread dumplings", "potato dumplings"], "german", "side"),
        Dish("maultaschen", ["Maultaschen"], ["maultaschen"], "german"),
        Dish("currywurst", ["Currywurst"], ["currywurst"], "sausage", "german"),
        Dish("omelette", ["Omelett", "Omelette", "Frittata", "Rührei", "Spiegelei", "Spiegeleier", "Eierspeise"],
            ["omelette", "omelet", "frittata", "scrambled eggs", "fried egg", "fried eggs"], "egg", "breakfast"),
        Dish("pancake", ["Pfannkuchen", "Eierkuchen", "Palatschinken", "Crêpes", "Crêpe", "Pancakes"],
            ["pancake", "pancakes", "crepe", "crepes"], "breakfast"),
        Dish("french_toast", ["Arme Ritter", "French Toast"], ["french toast"], "bread", "breakfast", "sweet"),
        Dish("porridge", ["Porridge", "Haferbrei", "Overnight Oats"], ["porridge", "overnight oats"], "oats", "breakfast"),
        Dish("muesli", ["Müsli", "Granola", "Knuspermüsli"], ["muesli", "granola"], "oats", "breakfast"),
        Dish("granola_bar", ["Müsliriegel", "Energieriegel", "Energiebällchen"], ["granola bar", "energy balls"],
            "snack"),
        Dish("smoothie", ["Smoothie", "Smoothies", "Shake"], ["smoothie", "smoothies", "milkshake"], "drink"),
        Dish("waffle", ["Waffel", "Waffeln"], ["waffle", "waffles"], "dessert"),
        Dish("cake", ["Kuchen", "Torte", "Torten", "Rührkuchen", "Blechkuchen", "Gugelhupf", "Napfkuchen", "Obstkuchen"],
            ["cake", "cakes", "gateau", "bundt"], "dessert", "baked"),
        Dish("cheesecake", ["Käsekuchen", "Quarkkuchen", "Cheesecake"], ["cheesecake"], "cake"),
        Dish("apple_cake", ["Apfelkuchen", "Apple Pie"], ["apple pie", "apple cake"], "apple", "cake"),
        Dish("strudel", ["Strudel", "Apfelstrudel"], ["strudel"], "dessert", "austrian"),
        Dish("kaiserschmarrn", ["Kaiserschmarrn", "Schmarrn"], ["kaiserschmarrn"], "dessert", "austrian"),
        Dish("brownie", ["Brownie", "Brownies", "Blondies"], ["brownie", "brownies", "blondies"], "dessert", "chocolate"),
        Dish("muffin", ["Muffin", "Muffins", "Cupcake", "Cupcakes", "Scones"], ["muffin", "muffins", "cupcake", "cupcakes",
            "scones"], "baked", "sweet"),
        Dish("cookies", ["Kekse", "Keks", "Plätzchen", "Cookies", "Spekulatius", "Lebkuchen", "Makronen"],
            ["cookie", "cookies", "biscuit", "biscuits", "shortbread", "macaroons"], "baked", "sweet"),
        Dish("pastry", ["Krapfen", "Berliner", "Donut", "Donuts", "Croissant", "Croissants", "Zimtschnecken",
                "Hefezopf", "Plunder"],
            ["doughnut", "doughnuts", "donut", "croissant", "cinnamon rolls", "danish"], "baked", "sweet"),
        Dish("pudding", ["Pudding", "Grießbrei", "Milchreis", "Mousse", "Mousse au Chocolat", "Panna Cotta",
                "Crème brûlée", "Creme"],
            ["pudding", "rice pudding", "mousse", "panna cotta", "creme brulee", "custard", "trifle"], "dessert"),
        Dish("ice_cream", ["Eis", "Speiseeis", "Eiscreme", "Sorbet", "Parfait", "Frozen Joghurt"],
            ["ice cream", "sorbet", "gelato", "frozen yoghurt"], "dessert", "cold"),
        Dish("fruit_salad", ["Obstsalat"], ["fruit salad"], "fruit", "dessert"),
        Dish("compote", ["Kompott", "Apfelmus", "Rote Grütze"], ["compote", "apple sauce", "applesauce"], "fruit",
            "dessert"),
        Dish("crumble", ["Crumble", "Streusel"], ["crumble", "cobbler"], "dessert", "baked"),
        Dish("jam", ["Marmelade", "Konfitüre", "Gelee"], ["jam", "marmalade", "jelly", "preserves"], "fruit", "sweet"),

        // ── Meals ────────────────────────────────────────────────────────────
        Meal("breakfast", ["Frühstück", "Brunch", "Frühstücksideen"], ["breakfast", "brunch"]),
        Meal("lunch", ["Mittagessen", "Mittag", "Mittagstisch"], ["lunch", "luncheon"]),
        Meal("dinner", ["Abendessen", "Abendbrot", "Abends"], ["dinner", "supper", "evening meal"]),
        Meal("dessert", ["Nachtisch", "Dessert", "Desserts", "Nachspeise", "Süßspeise", "Süßspeisen"],
            ["dessert", "desserts", "afters", "pudding course"], "sweet"),
        Meal("snack", ["Snack", "Snacks", "Zwischenmahlzeit", "Fingerfood", "Häppchen"],
            ["snack", "snacks", "finger food", "nibbles", "appetiser", "appetizer"]),
        Meal("side", ["Beilage", "Beilagen"], ["side", "side dish", "sides"]),
        Meal("drink", ["Getränk", "Getränke", "Drink", "Drinks", "Cocktail", "Limonade", "Bowle", "Punsch"],
            ["drink", "drinks", "beverage", "cocktail", "lemonade", "punch"]),

        // ── Cuisines ─────────────────────────────────────────────────────────
        Cuisine("italian", ["Italienisch", "Italien"], ["italian", "italy"], "mediterranean"),
        Cuisine("asian", ["Asiatisch", "Asien"], ["asian", "asia"]),
        Cuisine("chinese", ["Chinesisch", "China"], ["chinese", "china"], "asian"),
        Cuisine("japanese", ["Japanisch", "Japan"], ["japanese", "japan"], "asian"),
        Cuisine("thai", ["Thailändisch", "Thai"], ["thai", "thailand"], "asian"),
        Cuisine("indian", ["Indisch", "Indien"], ["indian", "india"], "asian"),
        Cuisine("vietnamese", ["Vietnamesisch", "Vietnam"], ["vietnamese", "vietnam"], "asian"),
        Cuisine("korean", ["Koreanisch", "Korea"], ["korean", "korea"], "asian"),
        Cuisine("mexican", ["Mexikanisch", "Mexiko", "Tex-Mex"], ["mexican", "mexico", "tex mex"]),
        Cuisine("american", ["Amerikanisch", "USA"], ["american", "usa"]),
        Cuisine("mediterranean", ["Mediterran", "Mittelmeer", "Mittelmeerküche"], ["mediterranean"]),
        Cuisine("greek", ["Griechisch", "Griechenland"], ["greek", "greece"], "mediterranean"),
        Cuisine("spanish", ["Spanisch", "Spanien"], ["spanish", "spain"], "mediterranean"),
        Cuisine("french", ["Französisch", "Frankreich"], ["french", "france"]),
        Cuisine("german", ["Deutsch", "Deutschland", "Hausmannskost"], ["german", "germany"]),
        Cuisine("austrian", ["Österreichisch", "Österreich"], ["austrian", "austria"]),
        Cuisine("middle_eastern", ["Orientalisch", "Arabisch", "Libanesisch", "Türkisch", "Persisch", "Israelisch"],
            ["middle eastern", "lebanese", "turkish", "persian", "israeli", "arabic"]),

        // ── Methods ──────────────────────────────────────────────────────────
        Method("baked", ["Ofen", "Ofengericht", "gebacken", "backen"], ["baked", "oven"]),
        Method("grilled", ["Grill", "gegrillt", "grillen", "Barbecue", "BBQ"], ["grilled", "grill", "barbecue", "bbq"]),
        Method("fried", ["frittiert", "Fritteuse", "Heißluftfritteuse", "Airfryer", "paniert"],
            ["fried", "deep fried", "air fryer", "breaded"]),
        Method("braised", ["geschmort", "schmoren", "Schmorgericht", "Slow Cooker", "Schongarer"],
            ["braised", "slow cooked", "slow cooker"]),
        Method("steamed", ["gedämpft", "gedünstet", "Dampfgarer"], ["steamed"]),
        Method("raw", ["roh", "Rohkost"], ["raw", "no cook"]),
        Method("one_pot", ["One Pot", "One-Pot", "Eintopfgericht"], ["one pot", "one pan"]),

        // ── Diets ────────────────────────────────────────────────────────────
        Diet("vegetarian", ["vegetarisch", "Vegetarier", "Veggie", "fleischlos", "ohne Fleisch"],
            ["vegetarian", "veggie", "meatless", "meat free"]),
        Diet("vegan", ["vegan", "pflanzlich", "pflanzenbasiert"], ["vegan", "plant based"], "vegetarian"),
        Diet("gluten_free", ["glutenfrei", "ohne Gluten"], ["gluten free", "coeliac"]),
        Diet("lactose_free", ["laktosefrei", "ohne Laktose", "milchfrei"], ["lactose free", "dairy free"]),
        Diet("low_carb", ["Low Carb", "kohlenhydratarm", "Keto"], ["low carb", "keto"]),

        // ── Character: what vague queries resolve to ─────────────────────────
        Character("warm", ["warm", "warme Mahlzeit", "warmes Essen"], ["hot meal", "warm meal"]),
        Character("cold", ["kalt", "kalte Küche"], ["cold", "chilled"]),
        Character("light", ["leicht", "leichte Küche"], ["light"]),
        Character("hearty", ["deftig", "herzhaft"], ["hearty", "savoury", "savory"]),
        Character("comfort", ["Soulfood", "Seelenfutter", "Comfort Food"], ["comfort food"], "warm", "hearty"),
        Character("sweet", ["süß", "süße", "Süßes", "Süßigkeiten"], ["sweet", "sweets"]),
        Character("spicy", ["scharf", "pikant", "feurig"], ["spicy", "fiery"]),
        Character("summer", ["Sommer", "sommerlich"], ["summer", "summery"]),
        Character("winter", ["Winter", "winterlich"], ["winter", "wintry"]),
        Character("christmas", ["Weihnacht", "weihnachtlich", "Advent"], ["christmas", "xmas"], "festive"),
        Character("easter", ["Ostern", "österlich"], ["easter"], "festive"),
        Character("festive", ["festlich", "Festessen", "Feiertag"], ["festive", "celebration"]),
        Character("kids", ["kinderfreundlich", "Kinder", "für Kinder"], ["kids", "kid friendly", "children"]),
        Character("party", ["Party", "Buffet", "Partyrezept"], ["party", "buffet", "potluck"]),
        Character("healthy", ["gesund", "gesunde", "gesundes"], ["healthy", "wholesome"]),
        Character("meal_prep", ["Meal Prep", "vorkochen", "Vorrat"], ["meal prep", "batch cooking", "make ahead"]),
        Character("quick", ["schnell", "Blitzrezept", "Fix", "einfach", "unkompliziert", "wenig Aufwand"],
            ["quick", "fast", "speedy", "weeknight", "easy", "simple"])
    ];
}
