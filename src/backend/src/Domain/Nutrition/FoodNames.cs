using System.Collections.Frozen;
using System.Text.RegularExpressions;
using Domain.Search;

namespace Domain.Nutrition;

/// <summary>
/// The fourth table of food words: which BLS food, exactly, an ingredient name is.
/// </summary>
/// <remarks>
/// <para>
/// Three tables already answer other questions (<see cref="CulinaryLexicon"/>: what else a word
/// means; common ingredients: what might be typed; section keywords: which aisle), and each is
/// coarse where this one must be exact: a wrong food is a wrong number. So matching is the
/// lexicon's strict reading of a name (<see cref="CulinaryLexicon.Name"/>): one form must account
/// for the whole name, give or take a short ending on its last word. There is no compound
/// splitting (<em>Kokosmilch</em> is not milk, <em>Mangold</em> is not mango), both languages are
/// always read, and a note is never read: only the name is passed in.
/// </para>
/// <para>
/// Every entry is the raw, as-bought state, because a recipe weighs what goes in. An unqualified
/// word takes the food it usually means in a German shop (<em>Milch</em> is whole milk); the
/// caller shows that choice and lets it be corrected. Any change to an entry or to how a name is
/// matched must raise <see cref="Version"/>. Densities (grams per millilitre, only for foods that
/// pour) come from USDA FoodData Central portion rows, named beside each.
/// </para>
/// </remarks>
public static partial class FoodNames
{
    /// <summary>The table's version; raise it with any change to an entry or to how a name is matched.</summary>
    public const int Version = 3;

    /// <summary>How many letters the last word of a name may carry past a form and still be that form.</summary>
    private const int Ending = 2;

    /// <summary>A last word this short, or shorter, only matches exactly ("Eis" is not egg).</summary>
    private const int ShortestWordWithEnding = 4;

    private static readonly Compiled Index = new(Entries());

    /// <summary>Every entry, in the order they are written below.</summary>
    public static IReadOnlyList<FoodName> All => Index.Names;

    /// <summary>The food a name is the name of, or null when it is not one of ours.</summary>
    /// <remarks>
    /// A plural marker glued to a word is dropped first (<c>Tomate(n)</c>); any other parenthesis
    /// stays part of the name, so <c>Balsamicoessig (bianco)</c> matches nothing.
    /// </remarks>
    /// <param name="ingredientName">The ingredient's name, without its note.</param>
    public static FoodName? Match(string ingredientName)
    {
        ArgumentNullException.ThrowIfNull(ingredientName);

        var name = PluralMarker().Replace(ingredientName, string.Empty);

        return Compiled.Whole(SearchText.FoldAe(name), Index.ByAe) ?? Compiled.Whole(SearchText.FoldA(name), Index.ByA);
    }

    /// <summary>A one or two letter plural glued to a word: <c>Tomate(n)</c>, <c>Hähnchenbrustfilet(s)</c>.</summary>
    [GeneratedRegex(@"(?<=\p{L})\(\p{L}{1,2}\)")]
    private static partial Regex PluralMarker();

    private sealed class Compiled
    {
        internal Compiled(IReadOnlyList<FoodName> names)
        {
            Names = names;
            ByAe = Read(names, SearchText.FoldAe);
            ByA = Read(names, SearchText.FoldA);
        }

        internal IReadOnlyList<FoodName> Names { get; }

        /// <summary>Every form folded the ä to ae way, to its entry.</summary>
        internal FrozenDictionary<string, FoodName> ByAe { get; }

        /// <summary>Every form folded the ä to a way, to its entry.</summary>
        internal FrozenDictionary<string, FoodName> ByA { get; }

        /// <summary>The entry whose form is the whole folded name, trying the shortest ending first.</summary>
        internal static FoodName? Whole(string folded, FrozenDictionary<string, FoodName> forms)
        {
            if (folded.Length == 0)
            {
                return null;
            }

            // Words before the last must match exactly, so only the last word is shortened.
            var lastSpace = folded.LastIndexOf(' ');
            var front = folded[..(lastSpace + 1)];
            var last = folded[(lastSpace + 1)..];

            for (var ending = 0; ending <= Ending; ending++)
            {
                var stem = last[..^ending];

                if (stem.Length == 0 || (ending > 0 && stem.Length < ShortestWordWithEnding))
                {
                    break;
                }

                if (forms.TryGetValue(front + stem, out var found))
                {
                    return found;
                }
            }

            return null;
        }

        private static FrozenDictionary<string, FoodName> Read(IReadOnlyList<FoodName> names, Func<string, string> fold)
        {
            var forms = new Dictionary<string, FoodName>(StringComparer.Ordinal);

            foreach (var name in names)
            {
                foreach (var form in name.De.Concat(name.En))
                {
                    forms.TryAdd(fold(form), name);
                }
            }

            return forms.ToFrozenDictionary(StringComparer.Ordinal);
        }
    }

    private static FoodName Name(
        string code,
        string[] de,
        string[] en,
        string labelDe,
        string labelEn,
        decimal? density = null,
        EggPart egg = EggPart.None,
        LiquidFood? liquid = null) =>
        new(code, de, en, density, egg, labelDe, labelEn, liquid);

    private static FoodName[] Entries() =>
    [
        // Hühnerei roh; size M applied by the app, not here
        Name("E111100", // Hühnerei roh
            ["Ei", "Eier", "Hühnerei", "Hühnereier"],
            ["egg", "eggs"], "Hühnerei", "egg", egg: EggPart.Whole),
        Name("E112100", // Hühnerei Eigelb, roh
            ["Eigelb", "Eigelbe", "Eidotter", "Dotter"],
            ["egg yolk", "egg yolks", "yolk", "yolks"], "Eigelb", "egg yolk", egg: EggPart.Yolk),
        Name("E113100", // Hühnerei Eiklar, roh
            ["Eiweiß", "Eiklar", "Eiweiße"],
            ["egg white", "egg whites", "white of egg", "egg albumen"], "Eiweiß", "egg white", egg: EggPart.White),
        // Density: FDC 171265 (Milk, whole, 3.25% milkfat, with added vitamin D), 1 cup = 244 g
        // unqualified Milch = Vollmilch 3,5 % (62 kcal); H-Vollmilch has identical values
        Name("M111300", // Vollmilch frisch, 3,5 % Fett, pasteurisiert
            ["Milch", "Vollmilch", "Frischmilch", "H-Milch", "Kuhmilch", "Trinkmilch"],
            ["milk", "whole milk", "full-fat milk"], "Vollmilch 3,5 %", "whole milk 3.5 %", density: 1.031m),
        // Density: FDC 171265 (Milk, whole, 3.25% milkfat, with added vitamin D), 1 cup = 244 g
        // density proxy: FDC whole milk (no 1.5 % row; difference <0.5 %)
        Name("M111200", // Milch fettarm, frisch, 1,5 % Fett, pasteurisiert
            ["fettarme Milch", "Milch 1,5 %"],
            ["low-fat milk", "lowfat milk", "semi-skimmed milk", "1.5% milk"], "fettarme Milch 1,5 %", "low-fat milk 1.5 %", density: 1.031m),
        // Density: FDC 171265 (Milk, whole, 3.25% milkfat, with added vitamin D), 1 cup = 244 g
        // density proxy: FDC whole milk
        Name("M111100", // Milch entrahmt, frisch, höchstens 0,1 % Fett, pasteurisiert
            ["Magermilch", "entrahmte Milch", "fettfreie Milch"],
            ["skimmed milk", "skim milk", "fat-free milk"], "Magermilch", "skimmed milk", density: 1.031m),
        // Density: FDC 172225 (Milk, buttermilk, fluid, whole), 1 cup = 245 g
        Name("M150000", // Buttermilch
            ["Buttermilch"],
            ["buttermilk"], "Buttermilch", "buttermilk", density: 1.036m),
        // Density: FDC 170858 (Cream, fluid, light whipping), 1 tbsp = 15 g
        // unqualified Sahne = Schlagsahne 30 % (shop standard 30-32 %); Kochsahne/Cremefine are NOT this; density from 1 tbsp row (rounded to 0.1 g)
        Name("M173800", // Schlagsahne mind. 30 % Fett
            ["Sahne", "Schlagsahne", "Süße Sahne", "Schlagrahm", "Sahne 30 %"],
            ["whipping cream"], "Schlagsahne 30 %", "whipping cream 30 %", density: 1.014m),
        // Density: FDC 170859 (Cream, fluid, heavy whipping), 1 fl oz = 29.8 g
        Name("M173900", // Schlagsahne mind. 36 % Fett
            ["Schlagsahne 36 %"],
            ["heavy cream", "heavy whipping cream"], "Schlagsahne 36 %", "heavy cream 36 %", density: 1.008m),
        // Density: FDC 171255 (Cream, fluid, half and half), 1 cup = 242 g
        // half-and-half (12 %) as density proxy for 10 % Kaffeesahne
        Name("M171500", // Kaffeesahne mind. 10 % Fett
            ["Kaffeesahne", "Kaffeesahne 10 %"],
            ["half and half"], "Kaffeesahne 10 %", "coffee cream 10 %", density: 1.023m),
        // Sauerrahm/Saure Sahne mind. 10 % (119 kcal); NOT Schmand (20 %)
        Name("M172500", // Sauerrahm/Saure Sahne, mind. 10 % Fett
            ["saure Sahne", "Sauerrahm", "Saure Sahne 10 %"],
            [], "saure Sahne 10 %", "sour cream 10 %"),
        // Schmand mind. 20 %; English sour cream is 18-20 %
        Name("M172700", // Sauerrahm/Schmand, mind. 20 % Fett
            ["Schmand", "Schmant"],
            ["sour cream", "soured cream"], "Schmand 20 %", "sour cream 20 %"),
        // Crème fraîche 30 % is the standard; light (15 %) is a different product. Accents fold away, so one spelling covers Creme fraiche / Crème Fraîche
        Name("M176800", // Sauerrahm/Creme fraiche, 30 % Fett
            ["Crème fraîche", "Crème fraîche 30 %"],
            [], "Crème fraîche 30 %", "crème fraîche 30 %"),
        // Crème double (40 % fat, about 400 kcal) is BLS Creme fraiche 40 %. English "double cream" is left out:
        // UK double cream is 48 % fat (about 449 kcal), 12 % more
        Name("M172900", // Sauerrahm/Creme fraiche, 40 % Fett
            ["Crème double", "Crème fraîche 40 %"],
            [], "Crème double 40 %", "crème double 40 %"),
        // defaults to 3,5 % (67 kcal) vs 1,5 % (62 kcal): 8 % apart; Greek style (10 % fat) is left out
        Name("M141300", // Joghurt mild, mind. 3,5 % Fett
            ["Joghurt", "Yoghurt", "Naturjoghurt", "Joghurt natur", "Vollmilchjoghurt"],
            ["yogurt", "natural yogurt", "natural yoghurt", "plain yogurt", "plain yoghurt"], "Joghurt 3,5 %", "plain yogurt 3.5 %"),
        // Sahnejoghurt mind. 10 %
        Name("M141500", // Sahnejoghurt mind. 10 % Fett
            ["Sahnejoghurt"],
            [], "Sahnejoghurt 10 %", "cream yogurt 10 %"),
        // only the Mager stage is unambiguous; plain "Quark" can be 0/20/40 % and is rejected
        Name("M713100", // Speisequark Magerstufe, Magerquark < 10 % Fett i. Tr.
            ["Magerquark", "Quark mager"],
            ["low-fat quark", "lowfat quark"], "Magerquark", "low-fat quark"),
        // a stated percentage is read as Fett i. Tr., which is how packs label it; plain "Quark" stays unmapped
        Name("M713300", // Speisequark Halbfettstufe, 20 % Fett i. Tr.
            ["Speisequark 20 %", "Quark 20 %", "Halbfettquark", "Speisequark Halbfettstufe"],
            [], "Speisequark 20 %", "quark 20 %"),
        // "Sahnequark" is deliberately not listed: sold as 40 % but sometimes as the 50 % Rahmstufe
        Name("M713500", // Speisequark Fettstufe, 40 % Fett i. Tr.
            ["Speisequark 40 %", "Quark 40 %"],
            [], "Speisequark 40 %", "quark 40 %"),
        Name("M710100", // Skyr, Frischkäse < 10 % Fett i. Tr.
            ["Skyr"],
            [], "Skyr", "skyr"),
        // mind. 20 % F.i.Tr., the usual retail product
        Name("M711300", // Körniger Frischkäse mind. 20 % Fett i. Tr.
            ["Hüttenkäse", "körniger Frischkäse"],
            ["cottage cheese"], "Hüttenkäse 20 %", "cottage cheese 20 %"),
        Name("M7A6800", // Mascarpone mind. 80 % Fett i. Tr.
            ["Mascarpone"],
            [], "Mascarpone", "mascarpone"),
        // Doppelrahmstufe = 60 % F.i.Tr.; plain Frischkäse is rejected (Natur 10-60 %)
        Name("M710800", // Frischkäsezubereitung Natur, mind. 60 % Fett i. Tr.
            ["Doppelrahmfrischkäse", "Frischkäse Doppelrahmstufe"],
            [], "Doppelrahmfrischkäse", "full-fat cream cheese"),
        // 45 % F.i.Tr. (259 kcal), the standard ball; light mozzarella and Büffelmozzarella are other foods
        Name("M032100", // Mozzarella mind. 45 % Fett i. Tr.
            ["Mozzarella", "Mozzarellas"],
            [], "Mozzarella (45 %)", "mozzarella (45 %)"),
        // not for grated "Reibekäse"
        Name("M306400", // Parmesan mind. 30 % Fett i. Tr.
            ["Parmesan", "Parmesankäse", "Parmigiano", "Parmigiano Reggiano"],
            ["parmesan cheese"], "Parmesan", "parmesan"),
        Name("M012200", // Feta mind. 45 % Fett i. Tr.
            ["Feta", "Fetakäse"],
            ["feta cheese"], "Feta", "feta"),
        Name("M741600", // Ricotta, Molkeneiweißkäse, mind. 45 % Fett i. Tr.
            ["Ricotta"],
            [], "Ricotta", "ricotta"),
        Name("M602600", // Camembert mind. 45 % Fett i. Tr.
            ["Camembert"],
            [], "Camembert", "camembert"),
        Name("M402600", // Gouda 48 % Fett i. Tr.
            ["Gouda"],
            [], "Gouda", "gouda"),
        Name("M304600", // Emmentaler mind. 45 % Fett i. Tr.
            ["Emmentaler"],
            ["emmental"], "Emmentaler", "emmental"),
        Name("M303700", // Chester (Cheddar) mind. 50 % Fett i. Tr.
            ["Cheddar"],
            [], "Cheddar", "cheddar"),
        Name("M5B1600", // Gorgonzola mind. 48 % Fett i. Tr.
            ["Gorgonzola"],
            [], "Gorgonzola", "gorgonzola"),
        // Butter mild gesäuert (747 kcal); sweet-cream and cultured butter have identical label values
        Name("Q611000", // Butter mild gesäuert
            ["Butter", "Butterflöckchen", "Markenbutter", "Landbutter"],
            ["unsalted butter"], "Butter", "butter"),
        Name("Q6A4000", // Butter gesalzen
            ["gesalzene Butter"],
            ["salted butter"], "gesalzene Butter", "salted butter"),
        Name("Q683000", // Butterschmalz
            ["Butterschmalz"],
            ["ghee", "clarified butter"], "Butterschmalz", "ghee"),
        Name("Q860000", // Schweinefett/Schweineschmalz
            ["Schweineschmalz", "Schmalz"],
            ["lard"], "Schweineschmalz", "lard"),
        // Pflanzenmargarine Vollfett (718 kcal); Streichfette of lower fat content are not this
        Name("Q400000", // Pflanzenmargarine Vollfett, angereichert mit Vitaminen
            ["Margarine"],
            [], "Pflanzenmargarine", "margarine"),
        Name("Q656000", // Knoblauchbutter/Kräuterbutter
            ["Kräuterbutter", "Knoblauchbutter"],
            ["herb butter", "garlic butter"], "Kräuterbutter", "herb butter"),
        // Density: FDC 171413 (Oil, olive, salad or cooking), 1 cup = 216 g
        Name("Q120000", // Olivenöl
            ["Olivenöl", "natives Olivenöl", "Olivenöl nativ extra", "natives Olivenöl extra", "Olivenöl extra vergine", "Olivenöl extra"],
            ["olive oil", "extra virgin olive oil"], "Olivenöl", "olive oil", density: 0.913m),
        // Density: FDC 172336 (Oil, canola), 1 cup = 218 g
        // FDC canola: the 1 tbsp row (14 g) is rounded high, the 1 cup row is used
        Name("Q180000", // Rapsöl/Rüböl
            ["Rapsöl"],
            ["rapeseed oil", "canola oil"], "Rapsöl", "rapeseed oil", density: 0.921m),
        // Density: FDC 171017 (Oil, sunflower, linoleic (less than 60%)), 1 cup = 218 g
        Name("Q320000", // Sonnenblumenöl
            ["Sonnenblumenöl"],
            ["sunflower oil"], "Sonnenblumenöl", "sunflower oil", density: 0.921m),
        // Density: FDC 171016 (Oil, sesame, salad or cooking), 1 cup = 218 g
        // no "Sesam" form on purpose: Sesam+öl is within the 2-letter ending tolerance
        Name("Q230000", // Sesamöl
            ["Sesamöl"],
            ["sesame oil", "toasted sesame oil"], "Sesamöl", "sesame oil", density: 0.921m),
        // Density: FDC 172370 (Oil, vegetable, soybean, refined), 1 cup = 218 g
        // generic cooking oil -> BLS "Bratöl/Frittieröl, pflanzlich" (900 kcal); saturated fat differs a little between oils
        Name("Q940000", // Bratöl/Frittieröl, pflanzlich
            ["Öl", "Speiseöl", "Pflanzenöl", "Bratöl", "Salatöl", "neutrales Öl"],
            ["oil", "vegetable oil", "cooking oil", "neutral oil", "salad oil"], "Pflanzenöl", "vegetable oil", density: 0.921m),
        // Density: FDC 171412 (Oil, coconut), 1 cup = 218 g (liquid row; it pours only when warm)
        // native coconut oil; all BLS coconut fats are 900 kcal. Kokosfett/Palmin is hardened (Q550200), a different product
        Name("Q550000", // Kokosöl
            ["Kokosöl", "Kokosnussöl", "natives Kokosöl", "Kokosöl nativ"],
            ["coconut oil", "virgin coconut oil"], "Kokosöl", "coconut oil", density: 0.921m),
        Name("S111000", // Zucker weiß (Raffinadezucker/Weißzucker)
            ["Zucker", "Haushaltszucker", "Kristallzucker", "Feinzucker", "Streuzucker", "Raffinade", "weißer Zucker"],
            ["sugar", "white sugar", "granulated sugar", "caster sugar", "castor sugar", "superfine sugar"], "Zucker", "sugar"),
        Name("S111100", // Puderzucker
            ["Puderzucker", "Staubzucker", "Zuckerpulver"],
            ["icing sugar", "powdered sugar", "confectioners sugar"], "Puderzucker", "icing sugar"),
        Name("S112000", // Zucker braun (Kandisfarin/Rohzucker)
            ["brauner Zucker", "Rohzucker", "Farinzucker"],
            ["brown sugar", "light brown sugar", "dark brown sugar", "soft brown sugar"], "brauner Zucker", "brown sugar"),
        Name("S114000", // Vanillezucker
            ["Vanillezucker", "Vanille-Zucker"],
            ["vanilla sugar"], "Vanillezucker", "vanilla sugar"),
        Name("R452000", // Vanillinzucker
            ["Vanillinzucker", "Vanillin-Zucker"],
            [], "Vanillinzucker", "vanillin sugar"),
        // Density: FDC 169640 (Honey), 1 cup = 339 g
        // 1 cup row = 339 g; check 1.433
        Name("S120000", // Honig
            ["Honig"],
            ["honey"], "Honig", "honey", density: 1.433m),
        // Density: FDC 169661 (Syrups, maple), 1 cup = 315 g
        Name("S151100", // Ahornsirup
            ["Ahornsirup"],
            ["maple syrup"], "Ahornsirup", "maple syrup", density: 1.331m),
        // Salt is a food (salt value 99+ g/100 g); no density: it packs
        Name("R111000", // Speisesalz/Siedesalz/Tafelsalz
            ["Salz", "Speisesalz", "Kochsalz", "Tafelsalz", "Siedesalz"],
            ["salt", "table salt", "cooking salt", "kosher salt"], "Speisesalz", "table salt"),
        Name("R112000", // Meersalz
            ["Meersalz", "Meersalz fein", "Meersalz grob"],
            ["sea salt", "fine sea salt", "coarse sea salt"], "Meersalz", "sea salt"),
        Name("R114000", // Speisesalz jodiert/Jodsalz
            ["Jodsalz", "jodiertes Salz", "Speisesalz jodiert"],
            ["iodised salt", "iodized salt"], "Jodsalz", "iodised salt"),
        Name("R421100", // Backpulver
            ["Backpulver"],
            ["baking powder"], "Backpulver", "baking powder"),
        // plain "Hefe" is rejected: fresh (128 kcal) vs dry (334 kcal)
        Name("R459000", // Backhefe frisch (Frischbackhefe)
            ["Frischhefe", "Hefewürfel", "Backhefe frisch"],
            ["fresh yeast", "fresh baker's yeast"], "Frischhefe", "fresh yeast"),
        Name("R458000", // Backhefe getrocknet (Trockenbackhefe)
            ["Trockenhefe", "Trockenbackhefe", "Backhefe trocken"],
            ["dried yeast", "dry yeast", "active dry yeast", "instant yeast"], "Trockenhefe", "dried yeast"),
        // sheet and powder gelatine have the same protein content
        Name("R468000", // Gelatine/Speisegelatine
            ["Gelatine", "Speisegelatine", "Blattgelatine", "Gelatineblätter", "weiße Gelatine", "Gelatine weiß"],
            ["gelatin", "gelatine leaves", "gelatine sheets"], "Gelatine", "gelatine"),
        // German Speisestärke = Maisstärke (Mondamin); plain "Stärke" is rejected. UK "cornflour" = Maisstärke
        Name("C446000", // Mais Stärke
            ["Speisestärke", "Maisstärke"],
            ["cornflour", "corn flour", "cornstarch", "corn starch"], "Speisestärke (Mais)", "cornflour (cornstarch)"),
        Name("K230000", // Kartoffelstärke (Kartoffelmehl)
            ["Kartoffelstärke", "Kartoffelmehl"],
            ["potato starch", "potato flour"], "Kartoffelstärke", "potato starch"),
        Name("R434000", // Sahnestandmittel
            ["Sahnesteif", "Sahnestandmittel", "Sahnefestiger"],
            [], "Sahnesteif", "whipped cream stabiliser"),
        Name("R481100", // Puddingpulver Vanille, ungezuckert
            ["Vanillepuddingpulver", "Vanille-Puddingpulver", "Puddingpulver Vanille"],
            [], "Vanillepuddingpulver", "vanilla pudding powder"),
        // unqualified Mehl = Weizenmehl Type 405 (348 kcal; 550 is 349)
        Name("C214100", // Weizen Mehl, Type 405
            ["Mehl", "Weizenmehl", "Weizenmehl 405", "Weizenmehl Type 405", "Mehl 405", "Mehl Type 405", "Weizenmehl Typ 405"],
            ["flour", "plain flour", "all-purpose flour", "wheat flour", "white flour", "flour 405"], "Weizenmehl Type 405", "wheat flour (type 405)"),
        Name("C214200", // Weizen Mehl, Type 550
            ["Weizenmehl 550", "Weizenmehl Type 550", "Mehl 550", "Weizenmehl Typ 550"],
            [], "Weizenmehl Type 550", "wheat flour (type 550)"),
        // Vollkornmehl unqualified assumed wheat; spelt/rye wholemeal differ by <8 %; "Volkornmehl" is a typo seen in the library
        Name("C211000", // Weizen Vollkornmehl
            ["Weizenvollkornmehl", "Vollkornmehl", "Volkornmehl", "Vollkorn-Mehl"],
            ["wholemeal flour", "whole wheat flour", "wholewheat flour", "wholegrain flour"], "Weizenvollkornmehl", "whole wheat flour"),
        Name("C234000", // Dinkel Mehl, Type 630
            ["Dinkelmehl", "Dinkelmehl 630", "Dinkelmehl Type 630"],
            ["spelt flour"], "Dinkelmehl Type 630", "spelt flour (type 630)"),
        // types 815-1740 are all 312-323 kcal
        Name("C223300", // Roggen Mehl, Type 1150
            ["Roggenmehl", "Roggenmehl 1150", "Roggenmehl Type 1150"],
            ["rye flour"], "Roggenmehl Type 1150", "rye flour (type 1150)"),
        Name("C453000", // Reis Mehl
            ["Reismehl"],
            ["rice flour"], "Reismehl", "rice flour"),
        Name("H720400", // Kichererbsenmehl
            ["Kichererbsenmehl"],
            ["chickpea flour", "gram flour", "besan"], "Kichererbsenmehl", "chickpea flour"),
        Name("B821000", // Paniermehl/Semmelbrösel/Semmelmehl
            ["Semmelbrösel", "Paniermehl", "Semmelmehl", "Panierbrösel", "Brösel"],
            ["breadcrumbs", "bread crumbs", "dry breadcrumbs"], "Semmelbrösel", "breadcrumbs"),
        Name("C133000", // Hafer Flocken
            ["Haferflocken", "Haferflocken zart", "Haferflocken kernig", "Kleinblatt Haferflocken", "Köllnflocken"],
            ["rolled oats", "oats", "porridge oats", "oat flakes"], "Haferflocken", "rolled oats"),
        // Reis poliert, roh (351 kcal); basmati/jasmine are within ~2 %. Reis+öl = Reisöl is within the 2-letter tolerance (accepted risk)
        Name("C352000", // Reis poliert, roh
            ["Reis", "Langkornreis", "Rundkornreis", "Risottoreis", "Arborio-Reis", "Basmatireis", "Basmati-Reis", "Jasminreis", "Jasmin-Reis", "Milchreis", "weißer Reis"],
            ["rice", "white rice", "long grain rice", "basmati rice", "basmati", "jasmine rice", "arborio rice", "arborio", "risotto rice", "pudding rice"], "weißer Reis (roh)", "white rice (raw)"),
        Name("C351000", // Reis unpoliert, roh
            ["Naturreis", "Vollkornreis", "Reis unpoliert", "brauner Reis"],
            ["brown rice", "wholegrain rice", "whole grain rice"], "Naturreis (roh)", "brown rice (raw)"),
        Name("C359000", // Reis parboiled, poliert, roh
            ["Parboiled Reis", "Reis parboiled"],
            ["parboiled rice"], "Parboiled-Reis (roh)", "parboiled rice (raw)"),
        Name("C353100", // Wildreis roh
            ["Wildreis"],
            ["wild rice"], "Wildreis", "wild rice"),
        // Couscous (Hartweizen) roh
        Name("C119200", // Couscous (Hartweizen) roh
            ["Couscous", "Cous Cous", "Cuscus"],
            [], "Couscous (trocken)", "couscous (dry)"),
        Name("C119100", // Bulgur (Hartweizen) roh
            ["Bulgur"],
            ["bulgur wheat", "bulghur"], "Bulgur (trocken)", "bulgur (dry)"),
        Name("C118000", // Quinoa weiß, roh
            ["Quinoa"],
            [], "Quinoa (trocken)", "quinoa (dry)"),
        // Polenta as bought is dry Maisgrieß (330 kcal), not the cooked BLS Polenta
        Name("C346000", // Mais Grieß
            ["Polenta", "Maisgrieß"],
            ["cornmeal", "maize semolina"], "Maisgrieß (Polenta)", "cornmeal (polenta)"),
        // Grieß unqualified = Weichweizengrieß
        Name("C218000", // Weichweizen Grieß
            ["Grieß", "Weichweizengrieß", "Weizengrieß"],
            ["semolina", "wheat semolina"], "Weichweizengrieß", "wheat semolina"),
        Name("C219300", // Hartweizen Grieß
            ["Hartweizengrieß"],
            ["durum semolina", "durum wheat semolina"], "Hartweizengrieß", "durum semolina"),
        Name("C322000", // Buchweizen roh
            ["Buchweizen"],
            ["buckwheat"], "Buchweizen", "buckwheat"),
        Name("C332000", // Hirse roh
            ["Hirse"],
            ["millet"], "Hirse", "millet"),
        // dry pasta, eifrei (346 kcal); egg pasta E432000 is 342 kcal, within 1 %. NOT the cooked E401032 (146 kcal). Bare "Lasagne" German is a dish and is not listed
        Name("E401000", // Teigwaren eifrei, roh
            ["Nudeln", "Nudel", "Pasta", "Spaghetti", "Spaghettini", "Penne", "Rigatoni", "Fusilli", "Farfalle", "Makkaroni", "Maccheroni", "Spirelli", "Orzo", "Risoni", "Lasagneplatten", "Lasagneblätter", "Lasagneblatt", "Tagliatelle", "Linguine", "Bandnudeln", "Hörnchennudeln", "Suppennudeln", "Teigwaren"],
            ["macaroni", "lasagne sheets", "lasagna sheets", "lasagne", "dried pasta"], "Nudeln ohne Ei (trocken)", "dried pasta without egg"),
        Name("E510000", // Vollkornteigwaren eifrei, roh
            ["Vollkornnudeln", "Vollkornspaghetti", "Vollkornpasta", "Vollkornpenne"],
            ["wholewheat pasta", "whole wheat pasta", "wholemeal pasta", "wholegrain pasta"], "Vollkornnudeln (trocken)", "wholewheat pasta (dried)"),
        Name("E432000", // Eierteigwaren roh
            ["Eiernudeln", "Eierteigwaren", "Eierspaghetti"],
            ["egg noodles", "egg pasta"], "Eiernudeln (trocken)", "egg noodles (dried)"),
        // Blätterteig eifrei, roh (425 kcal); butter puff pastry is 465 kcal. Shop dough is mostly vegetable-fat based. Rolled out from the roll/pack, raw
        Name("D072000", // Blätterteig eifrei, roh
            ["Blätterteig"],
            ["puff pastry"], "Blätterteig", "puff pastry"),
        // Speisezwiebel roh (34 kcal); colours do not differ in BLS
        Name("G480100", // Speisezwiebel roh
            ["Zwiebel", "Zwiebeln", "Speisezwiebel", "Speisezwiebeln", "Küchenzwiebel", "Küchenzwiebeln", "Gemüsezwiebel", "Gemüsezwiebeln", "weiße Zwiebel", "weiße Zwiebeln", "rote Zwiebel", "rote Zwiebeln", "gelbe Zwiebel", "gelbe Zwiebeln"],
            ["onion", "onions", "red onion", "red onions", "white onion", "white onions", "yellow onion", "yellow onions", "brown onion", "brown onions"], "Zwiebel", "onion"),
        Name("G482100", // Frühlingszwiebel/Lauchzwiebel, roh
            ["Frühlingszwiebel", "Frühlingszwiebeln", "Lauchzwiebel", "Lauchzwiebeln"],
            ["spring onion", "spring onions", "scallion", "scallions", "green onion", "green onions"], "Frühlingszwiebel", "spring onion"),
        Name("G485100", // Schalotte roh
            ["Schalotte", "Schalotten"],
            ["shallot", "shallots"], "Schalotte", "shallot"),
        // Knoblauch+öl = Knoblauchöl within the 2-letter tolerance (accepted risk)
        Name("G490100", // Knoblauch roh
            ["Knoblauch", "Knoblauchzehe", "Knoblauchzehen"],
            ["garlic", "garlic clove", "garlic cloves", "clove of garlic", "cloves of garlic"], "Knoblauch", "garlic"),
        Name("G470100", // Porree/Lauch, roh
            ["Lauch", "Porree"],
            ["leek", "leeks"], "Lauch", "leek"),
        // Karotte/Möhre roh
        Name("G620100", // Karotte/Möhre, roh
            ["Karotte", "Karotten", "Möhre", "Möhren", "Mohrrübe", "Mohrrüben"],
            ["carrot", "carrots"], "Karotte", "carrot"),
        // Kartoffel geschält, roh (83 kcal); unpeeled is 86
        Name("K110100", // Kartoffel geschält, roh
            ["Kartoffel", "Kartoffeln", "mehlige Kartoffel", "mehlige Kartoffeln", "festkochende Kartoffel", "festkochende Kartoffeln", "vorwiegend festkochende Kartoffel", "vorwiegend festkochende Kartoffeln", "Speisekartoffel", "Speisekartoffeln"],
            ["potato", "potatoes"], "Kartoffel", "potato"),
        Name("K420100", // Batate/Süßkartoffel, roh
            ["Süßkartoffel", "Süßkartoffeln", "Batate", "Bataten"],
            ["sweet potato", "sweet potatoes"], "Süßkartoffel", "sweet potato"),
        // fresh Tomate roh (22 kcal); canned (19-24 kcal) differs by <15 %, which is why the library lines "Tomaten, can" are still fine
        Name("G561100", // Tomate roh
            ["Tomate", "Tomaten", "Strauchtomate", "Strauchtomaten", "reife Strauchtomate", "Flaschentomate", "Flaschentomaten", "Kirschtomate", "Kirschtomaten", "Cherrytomate", "Cherrytomaten", "Cocktailtomate", "Cocktailtomaten", "Rispentomate", "Rispentomaten", "Fleischtomate", "Fleischtomaten"],
            ["tomato", "tomatoes", "cherry tomato", "cherry tomatoes", "plum tomato", "plum tomatoes", "vine tomatoes"], "frische Tomate", "fresh tomato"),
        // Tomate geschält, Konserve
        Name("G568900", // Tomate geschält, Konserve
            ["gehackte Tomaten", "geschälte Tomaten", "Dose geschälte Tomaten", "Dosentomaten", "stückige Tomaten", "Tomaten aus der Dose", "Tomaten gehackt", "Tomaten geschält"],
            ["chopped tomatoes", "canned tomatoes", "tinned tomatoes", "peeled tomatoes", "canned chopped tomatoes", "diced tomatoes", "canned whole tomatoes"], "Tomaten aus der Dose", "canned tomatoes"),
        // "Tomaten passiert/Tomatenpüree". No density although it pours (not a recognised pouring food)
        Name("R161200", // Tomaten passiert/Tomatenpüree
            ["passierte Tomaten", "Passata", "Tomatenpassata", "Tomatenpüree", "passierte Tomate"],
            ["strained tomatoes", "sieved tomatoes"], "passierte Tomaten", "passata"),
        // "tomato purée" (UK = paste, US = passata-like) is deliberately NOT listed
        Name("R160000", // Tomatenmark
            ["Tomatenmark", "Tomatenmark 2-fach konzentriert", "Tomatenmark dreifach konzentriert"],
            ["tomato paste", "tomato concentrate"], "Tomatenmark", "tomato paste"),
        Name("G520100", // Gurke roh
            ["Gurke", "Gurken", "Salatgurke", "Salatgurken", "Schlangengurke", "Schlangengurken"],
            ["cucumber", "cucumbers"], "Gurke", "cucumber"),
        Name("G520802", // Gurke gesäuert (Gewürzgurke) abgetropft
            ["Gewürzgurke", "Gewürzgurken", "Essiggurke", "Essiggurken", "Gewürzgürkchen"],
            ["gherkin", "gherkins", "pickled gherkin", "pickled gherkins"], "Gewürzgurke", "pickled gherkin"),
        Name("G543100", // Gemüsepaprika rot, roh
            ["rote Paprika", "rote Paprikaschote", "rote Paprikaschoten", "roter Paprika", "Paprika rot", "Paprika rote", "rote Spitzpaprika"],
            ["red bell pepper", "red bell peppers"], "rote Paprika", "red bell pepper"),
        Name("G542100", // Gemüsepaprika gelb, roh
            ["gelbe Paprika", "gelbe Paprikaschote", "Paprika gelb"],
            ["yellow bell pepper", "yellow bell peppers"], "gelbe Paprika", "yellow bell pepper"),
        Name("G541100", // Gemüsepaprika grün, roh
            ["grüne Paprika", "grüne Paprikaschote", "Paprika grün"],
            ["green bell pepper", "green bell peppers"], "grüne Paprika", "green bell pepper"),
        // Pfefferschote rot roh (40 kcal) = fresh chilli; bare Chili/Chilli rejected (fresh vs powder vs flakes)
        Name("G554100", // Pfefferschote rot, roh
            ["Chilischote", "Chilischoten", "rote Chilischote", "rote Chilischoten", "Chilli-Schote", "Peperoni rot", "Pfefferschote", "Pfefferschoten"],
            ["chilli pepper", "chili pepper", "red chilli", "red chili", "red chillies", "red chilies"], "rote Chilischote", "red chilli"),
        Name("G582100", // Zucchini roh
            ["Zucchini", "Zucchinis", "Zuccini", "Zucchino", "Zucchine"],
            ["courgette", "courgettes"], "Zucchini", "courgette"),
        Name("G510100", // Aubergine roh
            ["Aubergine", "Auberginen"],
            ["aubergines", "eggplant", "eggplants"], "Aubergine", "aubergine"),
        Name("G312100", // Broccoli roh
            ["Brokkoli", "Broccoli", "Brokoli"],
            [], "Brokkoli", "broccoli"),
        Name("G311100", // Blumenkohl roh
            ["Blumenkohl"],
            ["cauliflower"], "Blumenkohl", "cauliflower"),
        Name("G211100", // Spinat roh
            ["Spinat", "frischen Spinat", "frischer Spinat", "Blattspinat", "Babyspinat"],
            ["spinach", "fresh spinach", "baby spinach", "leaf spinach"], "frischer Spinat", "fresh spinach"),
        Name("G211200", // Spinat tiefgefroren
            ["Tiefkühlspinat", "TK-Spinat", "Spinat tiefgekühlt", "Spinat TK"],
            ["frozen spinach"], "Tiefkühlspinat", "frozen spinach"),
        Name("G341100", // Rotkohl roh
            ["Rotkohl", "Rotkraut", "Blaukraut", "Blaukohl"],
            ["red cabbage"], "Rotkohl", "red cabbage"),
        Name("G342100", // Weißkohl roh
            ["Weißkohl", "Weißkraut"],
            ["white cabbage", "cabbage"], "Weißkohl", "white cabbage"),
        Name("G343100", // Wirsing roh
            ["Wirsing", "Wirsingkohl"],
            ["savoy cabbage"], "Wirsing", "savoy cabbage"),
        Name("G332100", // Rosenkohl roh
            ["Rosenkohl"],
            ["brussels sprouts", "brussel sprouts"], "Rosenkohl", "brussels sprouts"),
        Name("G431100", // Fenchelblatt/Bologneser Fenchel, roh
            ["Fenchel", "Fenchelknolle"],
            ["fennel", "fennel bulb"], "Fenchel", "fennel"),
        Name("G331100", // Kohlrabi roh
            ["Kohlrabi"],
            [], "Kohlrabi", "kohlrabi"),
        Name("G691100", // Radieschen roh
            ["Radieschen"],
            ["radish", "radishes"], "Radieschen", "radish"),
        Name("G640100", // Pastinake roh
            ["Pastinake", "Pastinaken"],
            ["parsnip", "parsnips"], "Pastinake", "parsnip"),
        Name("G660100", // Knollensellerie roh
            ["Knollensellerie", "Sellerieknolle", "Sellerieknollen"],
            ["celeriac"], "Knollensellerie", "celeriac"),
        // German "Sellerie" is rejected: Knollensellerie 30 kcal vs Staudensellerie 17 kcal
        Name("G220100", // Bleichsellerie roh
            ["Staudensellerie", "Stangensellerie", "Bleichsellerie", "Selleriestange", "Selleriestangen"],
            ["celery", "celery stalks", "celery sticks"], "Staudensellerie", "celery"),
        Name("G093100", // Suppengrün/Suppenkraut, roh
            ["Suppengrün", "Suppengemüse"],
            [], "Suppengrün", "soup vegetables"),
        // roh (43 kcal); the vacuum-packed cooked product is ~40 kcal, within 10 %
        Name("G613100", // Rote Rübe/Rote Bete, roh
            ["Rote Bete", "Rote Beete", "Rote Rübe", "Rote Rüben", "Rotebete"],
            ["beetroot", "beetroots", "beet", "red beet"], "Rote Bete (roh)", "beetroot (raw)"),
        Name("G230100", // Mangold roh
            ["Mangold"],
            ["chard", "swiss chard"], "Mangold", "chard"),
        Name("G450100", // Spargel roh
            ["Spargel", "weißer Spargel", "Grüner Spargel", "Spargelstangen"],
            ["asparagus", "green asparagus", "white asparagus"], "Spargel", "asparagus"),
        // Erbse grün tiefgefroren (77 kcal) chosen over roh (88) and canned (~60): the shop product is frozen or canned; all within 15-25 %
        Name("G760200", // Erbse grün, tiefgefroren
            ["Erbsen", "Erbse", "Tiefkühlerbsen", "TK-Erbsen", "grüne Erbsen"],
            ["peas", "green peas", "frozen peas"], "Erbsen (tiefgekühlt)", "peas (frozen)"),
        Name("G761100", // Zuckererbse roh
            ["Zuckerschoten", "Zuckererbsen", "Zuckerschote"],
            ["sugar snap peas", "snow peas", "mangetout"], "Zuckerschoten", "sugar snap peas"),
        Name("G710100", // Bohne grün, roh
            ["grüne Bohnen", "Brechbohnen", "Prinzessbohnen", "Prinzessbohne", "Stangenbohnen", "Buschbohnen"],
            ["green beans", "french beans", "runner beans", "string beans"], "grüne Bohnen", "green beans"),
        // Zuckermais Konserve abgetropft (79 kcal); raw 67, frozen 73. Plain "Mais" NOT listed: Mais+öl = Maisöl is within the 2-letter tolerance, and Mais can mean dry grain (336 kcal)
        Name("G570902", // Zuckermais Konserve, abgetropft
            ["Zuckermais", "Mais aus der Dose", "Dosenmais", "Dosen-Mais", "Mais Dose", "Mais Konserve", "Mais abgetropft", "abgetropfter Mais"],
            ["sweetcorn", "sweet corn", "canned corn", "canned sweetcorn", "canned sweet corn", "tinned sweetcorn", "tinned sweet corn", "tinned corn", "drained sweetcorn"], "Zuckermais (Dose)", "sweetcorn (canned)"),
        Name("F502100", // Avocado roh
            ["Avocado", "Avocados"],
            [], "Avocado", "avocado"),
        Name("G130100", // Rucola roh
            ["Rucola", "Rauke", "Ruccola"],
            ["rocket", "arugula"], "Rucola", "rocket"),
        Name("G106100", // Radicchio roh
            ["Radicchio"],
            [], "Radicchio", "radicchio"),
        Name("G104100", // Feldsalat/Rapunzel, roh
            ["Feldsalat", "Rapunzel"],
            ["lamb's lettuce", "corn salad", "mache"], "Feldsalat", "lamb's lettuce"),
        Name("G103100", // Eisbergsalat roh
            ["Eisbergsalat", "Eisberg"],
            ["iceberg lettuce", "iceberg"], "Eisbergsalat", "iceberg lettuce"),
        // German plain "Salat" is rejected (also a dish)
        Name("G105100", // Kopfsalat roh
            ["Kopfsalat", "Butterkopfsalat", "Buttersalat"],
            ["lettuce", "butterhead lettuce", "butter lettuce", "head lettuce"], "Kopfsalat", "lettuce"),
        Name("G581000", // Kürbis Hokkaido (C. maxima) roh
            ["Hokkaido", "Hokkaidokürbis", "Hokkaido-Kürbis"],
            ["hokkaido pumpkin", "hokkaido squash", "red kuri squash"], "Hokkaido-Kürbis", "hokkaido squash"),
        // Champignon roh; Pilze unqualified rejected
        Name("K701100", // Champignon roh
            ["Champignon", "Champignons", "Egerling", "Egerlinge", "Braune Champignons"],
            ["mushroom", "mushrooms", "button mushrooms", "button mushroom", "brown mushrooms", "cremini mushrooms", "chestnut mushrooms"], "Champignons", "button mushrooms"),
        Name("K713100", // Pfifferling roh
            ["Pfifferlinge", "Pfifferling", "Rehlinge"],
            ["chanterelles", "chanterelle", "girolles"], "Pfifferlinge", "chanterelles"),
        // BLS Petersilienblatt roh
        Name("G250100", // Petersilienblatt roh
            ["Petersilie", "glatte Petersilie", "krause Petersilie", "Blattpetersilie"],
            ["parsley", "flat-leaf parsley", "curly parsley"], "Petersilie", "parsley"),
        Name("G081100", // Schnittlauch roh
            ["Schnittlauch"],
            ["chives", "chive"], "Schnittlauch", "chives"),
        Name("G061000", // Basilikum roh
            ["Basilikum"],
            ["basil", "fresh basil"], "Basilikum", "basil"),
        Name("G492100", // Bärlauch roh
            ["Bärlauch"],
            ["wild garlic", "ramsons"], "Bärlauch", "wild garlic"),
        Name("G280100", // Gartenkresse roh
            ["Kresse", "Gartenkresse"],
            ["cress", "garden cress"], "Gartenkresse", "garden cress"),
        Name("R211200", // Ingwer/Ingwerwurzel, roh
            ["Ingwer", "Ingwerwurzel", "frischer Ingwer"],
            ["ginger", "fresh ginger", "root ginger"], "frischer Ingwer", "fresh ginger"),
        Name("F110100", // Apfel roh
            ["Apfel", "Kochapfel", "säuerliche Äpfel", "säuerlicher Apfel", "Tafelapfel"],
            ["apple", "apples", "cooking apple", "cooking apples"], "Apfel", "apple"),
        Name("F130100", // Birne roh
            ["Birne", "Birnen"],
            ["pear", "pears"], "Birne", "pear"),
        Name("F503100", // Banane roh
            ["Banane", "Bananen"],
            ["banana", "bananas"], "Banane", "banana"),
        Name("F603100", // Orange roh
            ["Orange", "Orangen"],
            ["oranges"], "Orange", "orange"),
        // whole lemon; Zitronenspalten/Zitronenschale are rejected
        Name("F601100", // Zitrone roh
            ["Zitrone", "Zitronen", "Zitone"],
            ["lemon", "lemons"], "Zitrone", "lemon"),
        Name("F602100", // Limette roh
            ["Limette", "Limetten"],
            ["lime", "limes"], "Limette", "lime"),
        Name("F516100", // Mango roh
            ["Mango", "Mangos", "Mangoes"],
            [], "Mango", "mango"),
        Name("F301100", // Erdbeere roh
            ["Erdbeere", "Erdbeeren"],
            ["strawberry", "strawberries"], "Erdbeere", "strawberry"),
        Name("F302100", // Himbeere roh
            ["Himbeere", "Himbeeren"],
            ["raspberry", "raspberries"], "Himbeere", "raspberry"),
        Name("F304100", // Heidelbeere roh
            ["Blaubeere", "Blaubeeren", "Heidelbeere", "Heidelbeeren"],
            ["blueberry", "blueberries"], "Heidelbeere", "blueberry"),
        Name("F303100", // Brombeere roh
            ["Brombeere", "Brombeeren"],
            ["blackberry", "blackberries"], "Brombeere", "blackberry"),
        Name("F310100", // Weintraube roh
            ["Weintrauben", "Weintraube", "Trauben", "Traube"],
            ["grapes", "grape"], "Weintraube", "grape"),
        Name("F223100", // Zwetschge roh
            ["Zwetschge", "Zwetschgen", "Zwetschke", "Zwetschken"],
            [], "Zwetschge", "plum"),
        Name("F506100", // Granatapfel roh
            ["Granatapfel"],
            ["pomegranate", "pomegranates"], "Granatapfel", "pomegranate"),
        Name("F081100", // Rhabarber roh
            ["Rhabarber", "Rhababer"],
            ["rhubarb"], "Rhabarber", "rhubarb"),
        Name("F840100", // Rosine/Sultanine (Weinbeere getrocknet)
            ["Rosinen", "Sultaninen", "Rosine"],
            ["raisins", "sultanas", "raisin"], "Rosinen", "raisins"),
        // BLS "Cranberries getrocknet, gezuckert"; fresh cranberries are a different food
        Name("F414400", // Cranberries getrocknet, gezuckert
            ["getrocknete Cranberries", "getrocknete Cranberry", "Cranberries getrocknet"],
            ["dried cranberries", "dried cranberry", "craisins"], "Cranberries (getrocknet)", "dried cranberries"),
        // no singular "Mandel": Mandel+öl = Mandelöl; roasted and blanched differ <2 %
        Name("H210100", // Mandel süß
            ["Mandeln", "Mandelkerne", "ganze Mandeln", "geschälte Mandeln", "Mandelblättchen", "Mandelstifte", "Mandelsplitter", "Mandelhobel"],
            ["almonds", "whole almonds", "flaked almonds", "sliced almonds", "slivered almonds"], "Mandeln", "almonds"),
        Name("H210400", // Mandel süß, gemahlen
            ["gemahlene Mandeln", "Mandelmehl", "geriebene Mandeln"],
            ["ground almonds", "almond flour", "almond meal", "almonds ground"], "gemahlene Mandeln", "ground almonds"),
        Name("H212800", // Mandelmus
            ["Mandelmus"],
            ["almond butter"], "Mandelmus", "almond butter"),
        // no singular "Haselnuss": +öl = Haselnussöl
        Name("H130100", // Haselnuss
            ["Haselnüsse", "Haselnusskerne", "Haselnusskern", "Haselnuss-Kerne", "Haselnüsse ganz"],
            ["hazelnuts", "hazelnut kernels"], "Haselnüsse", "hazelnuts"),
        Name("H130400", // Haselnuss gemahlen
            ["gemahlene Haselnüsse", "gemahlene Haselnusskerne", "geriebene Haselnüsse"],
            ["ground hazelnuts"], "gemahlene Haselnüsse", "ground hazelnuts"),
        // no singular "Walnuss": +öl = Walnussöl
        Name("H120100", // Walnuss
            ["Walnüsse", "Walnusskerne", "Walnusskern", "Walnüsse ganz"],
            ["walnuts", "walnut kernels", "walnut halves"], "Walnüsse", "walnuts"),
        Name("H170100", // Cashewkern
            ["Cashewkerne", "Cashews", "Cashewnüsse", "Cashewkern"],
            ["cashew nuts", "cashew"], "Cashewkerne", "cashew nuts"),
        Name("H320100", // Pinienkern
            ["Pinienkerne", "Pinienkern", "Pignoli"],
            ["pine nuts", "pine kernels", "pinenuts"], "Pinienkerne", "pine nuts"),
        Name("H310100", // Kürbiskern
            ["Kürbiskerne", "Kürbiskern"],
            ["pumpkin seeds", "pepitas"], "Kürbiskerne", "pumpkin seeds"),
        Name("H430100", // Sonnenblumenkern
            ["Sonnenblumenkerne", "Sonnenblumenkern"],
            ["sunflower seeds"], "Sonnenblumenkerne", "sunflower seeds"),
        Name("H410100", // Leinsamen
            ["Leinsamen", "Leinsaat"],
            ["flaxseed", "flax seeds", "flaxseeds"], "Leinsamen", "flaxseed"),
        Name("H480100", // Chia-Samen
            ["Chiasamen", "Chia-Samen", "Chia"],
            ["chia seeds"], "Chiasamen", "chia seeds"),
        // no German "Sesam": Sesam+öl = Sesamöl
        Name("H420100", // Sesam
            ["Sesamsamen", "Sesamkörner", "Sesamsaat"],
            ["sesame seeds", "sesame"], "Sesamsamen", "sesame seeds"),
        Name("Q901000", // Tahin (Sesammus)
            ["Tahin", "Tahini", "Sesampaste", "Sesammus"],
            ["sesame paste"], "Tahin", "tahini"),
        Name("H880200", // Erdnussbutter/Erdnusscreme
            ["Erdnussbutter", "Erdnusscreme"],
            ["peanut butter"], "Erdnussbutter", "peanut butter"),
        // BLS has only roasted peanuts, no raw (raw is about 570 kcal, so a bare "Erdnüsse" may be up to 9 % high).
        // No singular "Erdnuss" on purpose: Erdnuss + öl = Erdnussöl is within the ending tolerance
        Name("H110600", // Erdnuss geröstet
            ["Erdnüsse", "Erdnusskerne", "geröstete Erdnüsse", "Erdnüsse geröstet", "ungesalzene Erdnüsse", "Erdnüsse ungesalzen"],
            ["peanuts", "roasted peanuts", "unsalted peanuts", "dry roasted peanuts"], "Erdnüsse (geröstet)", "peanuts (roasted)"),
        // same energy as the unsalted (614 against 620 kcal) but 0.96 g salt, so the salted wording has its own food
        Name("H110700", // Erdnuss geröstet, gesalzen
            ["gesalzene Erdnüsse", "Erdnüsse gesalzen", "geröstete gesalzene Erdnüsse"],
            ["salted peanuts", "roasted salted peanuts", "peanuts salted"], "gesalzene Erdnüsse", "salted peanuts"),
        Name("H150400", // Kokos Fruchtfleisch, geraspelt, getrocknet
            ["Kokosraspel", "Kokosraspeln", "Kokosflocken", "geraspelte Kokosnuss"],
            ["desiccated coconut", "shredded coconut", "coconut flakes", "grated coconut"], "Kokosraspel", "desiccated coconut"),
        // Density: FDC 170173 (Nuts, coconut milk, canned (liquid expressed from grated mea), 1 cup = 226 g
        // canned coconut milk (BLS 227 kcal); light coconut milk differs; FDC canned row (1 cup = 226 g) used
        Name("H154000", // Kokosmilch/Kokosnussmilch
            ["Kokosmilch", "Kokosnussmilch"],
            ["coconut milk"], "Kokosmilch (Dose)", "coconut milk (canned)", density: 0.955m),
        // Linse reif (dry); canned lentils are a different food
        Name("H725100", // Linse reif
            ["Linsen", "Linse", "braune Linsen", "grüne Linsen", "Tellerlinsen", "Berglinsen"],
            ["lentils", "brown lentils", "green lentils", "puy lentils"], "Linsen (trocken)", "lentils (dry)"),
        // dry red lentils
        Name("H730000", // Linse rot reif
            ["rote Linsen", "Linsen rot"],
            ["red lentils"], "rote Linsen (trocken)", "red lentils (dry)"),
        // Canned legumes by wording only: a bare "Kidneybohnen" or "Linsen" is dry or cooked (about 3 times the
        // energy apart), so only wording that says canned or drained maps to the BLS "Konserve, abgetropft" food
        Name("H742902", // Kidneybohne reif, Konserve, abgetropft
            ["Dosen-Kidneybohnen", "Kidneybohnen aus der Dose", "Kidneybohnen Dose", "Kidneybohnen Konserve", "Kidneybohnen abgetropft", "abgetropfte Kidneybohnen", "rote Kidneybohnen aus der Dose"],
            ["canned kidney beans", "tinned kidney beans", "kidney beans canned", "drained kidney beans"], "Kidneybohnen (gekocht)", "kidney beans (cooked)"),
        Name("H720902", // Kichererbse reif, Konserve, abgetropft
            ["Dosen-Kichererbsen", "Kichererbsen aus der Dose", "Kichererbsen Dose", "Kichererbsen Konserve", "Kichererbsen abgetropft", "abgetropfte Kichererbsen"],
            ["canned chickpeas", "tinned chickpeas", "chickpeas canned", "drained chickpeas", "canned garbanzo beans"], "Kichererbsen (gekocht)", "chickpeas (cooked)"),
        Name("H730902", // Linse reif, Konserve, abgetropft
            ["Dosenlinsen", "Dosen-Linsen", "Linsen aus der Dose", "Linsen Dose", "Linsen Konserve", "abgetropfte Linsen"],
            ["canned lentils", "tinned lentils", "lentils canned", "drained lentils"], "Linsen (gekocht)", "lentils (cooked)"),
        Name("H740902", // Gartenbohne reif, Konserve, abgetropft
            ["weiße Bohnen aus der Dose", "weiße Bohnen Dose", "abgetropfte weiße Bohnen", "Cannellinibohnen aus der Dose", "Cannellini-Bohnen aus der Dose"],
            ["canned white beans", "tinned white beans", "canned cannellini beans", "canned haricot beans", "canned navy beans"], "weiße Bohnen (gekocht)", "white beans (cooked)"),
        Name("H861000", // Tofu
            ["Tofu", "Naturtofu"],
            [], "Tofu", "tofu"),
        Name("H861100", // Seidentofu
            ["Seidentofu"],
            ["silken tofu"], "Seidentofu", "silken tofu"),
        Name("H862200", // Miso/Sojabohnenpaste
            ["Miso", "Misopaste", "Miso-Paste"],
            [], "Miso", "miso"),
        // mittelscharf (111 kcal); Senf+öl = Senföl within the 2-letter tolerance (accepted risk). Dijon-Senf is rejected (no Dijon food in BLS)
        Name("R132000", // Senf mittelscharf
            ["Senf", "Senf mittelscharf", "mittelscharfer Senf"],
            ["mustard"], "mittelscharfer Senf", "medium mustard"),
        Name("R133000", // Senf scharf
            ["scharfer Senf", "Senf scharf", "Senf extra scharf"],
            ["hot mustard"], "scharfer Senf", "hot mustard"),
        Name("R135000", // Senf süß
            ["süßer Senf", "Senf süß", "bayerischer Senf", "Weißwurstsenf"],
            ["sweet mustard"], "süßer Senf", "sweet mustard"),
        Name("R141100", // Tomatenketchup
            ["Ketchup", "Tomatenketchup", "Ketschup"],
            ["tomato ketchup", "catsup"], "Tomatenketchup", "tomato ketchup"),
        // Mayonnaise (Fertigprodukt) 80 % fat, 750 kcal; Salatmayonnaise is a different product
        Name("Q991000", // Mayonnaise (Fertigprodukt)
            ["Mayonnaise", "Mayonaise", "Mayo"],
            [], "Mayonnaise 80 %", "mayonnaise 80 %"),
        Name("R146100", // Sambal Oelek, Würzpaste
            ["Sambal Oelek", "Sambal Olek"],
            [], "Sambal Oelek", "sambal oelek"),
        Name("G898400", // Ajvar Konserve
            ["Ajvar"],
            [], "Ajvar", "ajvar"),
        // Kapern gesäuert, abgetropft
        Name("G012902", // Kapern gesäuert, abgetropft
            ["Kapern", "Kapernbeeren"],
            ["capers", "caper berries"], "Kapern", "capers"),
        // Density: FDC 174277 (Soy sauce made from soy and wheat (shoyu)), 1 cup = 255 g
        Name("R143000", // Sojasauce/Sojasoße
            ["Sojasauce", "Sojasoße", "Soja-Sauce", "Shoyu"],
            ["soy sauce", "soya sauce"], "Sojasauce", "soy sauce", density: 1.078m),
        // Density: FDC 172240 (Vinegar, red wine), 1 cup = 239 g
        Name("R121000", // Weinessig
            ["Weinessig", "Weißweinessig", "Rotweinessig", "Weisswein-Essig", "Rotwein-Essig"],
            ["wine vinegar", "white wine vinegar", "red wine vinegar"], "Weinessig", "wine vinegar", density: 1.010m),
        // Density: FDC 172237 (Vinegar, distilled), 1 cup = 238 g
        // Essigessenz is 25 % acid, far stronger than table vinegar (BLS R554000)
        Name("R122000", // Branntweinessig
            ["Essig", "Haushaltsessig", "Branntweinessig", "Tafelessig"],
            ["vinegar", "white vinegar", "distilled vinegar"], "Branntweinessig", "white vinegar", density: 1.006m),
        // Density: FDC 173469 (Vinegar, cider), 1 cup = 239 g
        Name("R123100", // Apfelessig
            ["Apfelessig"],
            ["apple cider vinegar", "cider vinegar", "apple vinegar"], "Apfelessig", "apple cider vinegar", density: 1.010m),
        // Density: FDC 172241 (Vinegar, balsamic), 1 cup = 255 g
        // dark balsamic; "Balsamicoessig (bianco)" is rejected
        Name("R125000", // Balsamicoessig
            ["Balsamico", "Balsamicoessig", "Aceto balsamico", "Balsamessig"],
            ["balsamic vinegar", "balsamic"], "Balsamicoessig", "balsamic vinegar", density: 1.078m),
        // BLS generic "Schokolade" (520 kcal); dark/milk/white have own entries
        Name("S500000", // Schokolade
            ["Schokolade", "Tafelschokolade"],
            ["chocolate"], "Schokolade", "chocolate"),
        // "Zartbitter-/Halbbitterschokolade"; "dark chocolate" (70 %+) is NOT listed
        Name("S560000", // Zartbitter-/Halbbitterschokolade
            ["Zartbitterschokolade", "Zartbitter-Schokolade", "Halbbitterschokolade", "Halbbitter-Schokolade", "Zartbitter"],
            ["semisweet chocolate", "semi-sweet chocolate", "bittersweet chocolate"], "Zartbitterschokolade", "dark chocolate"),
        Name("S570000", // Bitterschokolade
            ["Bitterschokolade", "Bitter-Schokolade"],
            [], "Bitterschokolade", "bitter chocolate"),
        Name("S539900", // Vollmilchschokolade
            ["Vollmilchschokolade", "Vollmilch-Schokolade", "Milchschokolade"],
            ["milk chocolate"], "Vollmilchschokolade", "milk chocolate"),
        Name("S580000", // Schokolade weiß
            ["weiße Schokolade"],
            ["white chocolate"], "weiße Schokolade", "white chocolate"),
        // Kuvertüre unqualified assumed dark (534 kcal); milk couverture is ~555 (4 % apart)
        Name("S671000", // Zartbitter-/Halbbitter-Kuvertüre
            ["Kuvertüre", "Zartbitterkuvertüre", "Zartbitter-Kuvertüre", "Halbbitterkuvertüre"],
            [], "Zartbitterkuvertüre", "dark couverture"),
        Name("S241000", // Vanilleeis
            ["Vanilleeis", "Vanille-Eis"],
            ["vanilla ice cream"], "Vanilleeis", "vanilla ice cream"),
        Name("S132000", // Konfitüre extra
            ["Konfitüre", "Marmelade", "Konfitüre extra"],
            ["jam", "fruit jam", "strawberry jam"], "Konfitüre extra", "jam"),
        // Rind Hackfleisch roh (224 kcal); BLS has no "gemischtes Hackfleisch", so plain Hackfleisch is rejected
        Name("U010100", // Rind Hackfleisch, roh
            ["Rinderhack", "Rinderhackfleisch", "Hackfleisch vom Rind", "Rindshack", "Rindshackfleisch", "Rindfleisch gehackt"],
            ["ground beef", "minced beef", "beef mince", "beef mince meat"], "Rinderhackfleisch", "minced beef"),
        Name("U020100", // Schwein Hackfleisch, roh
            ["Schweinehack", "Schweinehackfleisch", "Hackfleisch vom Schwein", "Schweinefleisch gehackt"],
            ["ground pork", "minced pork", "pork mince"], "Schweinehackfleisch", "minced pork"),
        // Rind Gulasch (Bug) roh; bare Rindfleisch is rejected (cuts range 110-300 kcal)
        Name("U151100", // Rind Gulasch (Bug) roh
            ["Rindergulasch", "Gulasch vom Rind", "Rindfleisch Gulasch", "Rindfleisch für Gulasch"],
            ["beef goulash meat"], "Rindergulasch (Bug)", "beef goulash (shoulder)"),
        Name("U284100", // Rind Hüfte, roh
            ["Rinderhüftsteak", "Hüftsteak", "Rinderhüfte"],
            [], "Rinderhüftsteak", "beef rump steak"),
        Name("U211100", // Rind Filet/Lende, roh
            ["Rinderfilet", "Rinderlende", "Rinderfiletsteak", "Filetsteak"],
            ["beef fillet", "beef tenderloin", "fillet steak", "beef filet"], "Rinderfilet", "beef fillet"),
        Name("U342100", // Kalb Schnitzel (Keule) roh
            ["Kalbsschnitzel", "Schnitzel vom Kalb"],
            ["veal escalope", "veal schnitzel", "veal cutlet"], "Kalbsschnitzel", "veal escalope"),
        Name("U541100", // Schwein Schnitzel (Oberschale) roh
            ["Schweineschnitzel", "Schnitzel vom Schwein"],
            ["pork escalope", "pork schnitzel", "pork cutlet"], "Schweineschnitzel", "pork escalope"),
        Name("U611100", // Schwein Filet/Lende, roh
            ["Schweinefilet", "Schweinelende"],
            ["pork tenderloin", "pork fillet", "pork filet"], "Schweinefilet", "pork tenderloin"),
        // Hähnchen Brustfilet roh (109 kcal), not marinated
        Name("V416100", // Hähnchen Brustfilet, roh
            ["Hähnchenbrustfilet", "Hähnchenbrustfilets", "Hähnchenbrust", "Hähnchenfilet", "Hähnchenfilets", "Hühnerbrust", "Hühnerbrustfilet", "Hühnchenbrust", "Hühnchenbrustfilet", "Hühnerfilet", "Hühnchenfilet"],
            ["chicken breast", "chicken breasts", "chicken breast fillet", "chicken breast fillets", "chicken fillet", "chicken fillets", "chicken filet"], "Hähnchenbrustfilet", "chicken breast"),
        // raw turkey breast, ohne Haut
        Name("V486100", // Pute Brust, ohne Haut, roh
            ["Putenbrust", "Putenbrustfilet", "Putenbrustfilets", "Pute Brust", "Putenfilet", "Truthahnbrust"],
            ["turkey breast", "turkey breast fillet", "turkey fillet"], "Putenbrust", "turkey breast"),
        // plain "Schinken" rejected: Kochschinken 130 vs Rohschinken 270-310 kcal
        Name("W424000", // Schwein Kochschinken, Kochpökelware
            ["Kochschinken", "gekochter Schinken", "Schinken gekocht", "Prager Schinken"],
            ["cooked ham", "boiled ham"], "Kochschinken", "cooked ham"),
        Name("W441000", // Parmaschinken
            ["Parmaschinken", "Prosciutto", "Prosciutto di Parma", "Prosciutto crudo"],
            ["parma ham"], "Parmaschinken", "parma ham"),
        Name("W442000", // Schwarzwälder Schinken, Rohpökelware, geräuchert
            ["Schwarzwälder Schinken", "Schwarzwälderschinken"],
            ["black forest ham"], "Schwarzwälder Schinken", "black forest ham"),
        // Bacon = Frühstücksspeck (304 kcal); German bare "Speck" is rejected (Bauchspeck 304 vs Rückenspeck 746)
        Name("W415000", // Schwein Frühstücksspeck, Rohpökelware, geräuchert
            ["Bacon", "Frühstücksspeck", "Baconscheiben"],
            ["streaky bacon", "bacon rashers"], "Frühstücksspeck", "streaky bacon"),
        Name("W140000", // Salami
            ["Salami"],
            [], "Salami", "salami"),
        // Lachs roh (180 kcal); smoked salmon is a different food and has its own words (Räucherlachs)
        Name("T410100", // Lachs roh
            ["Lachs", "Lachsfilet", "Lachsfilets", "Lachssteak", "Lachssteaks"],
            ["salmon", "salmon fillet", "salmon fillets", "salmon steak", "fresh salmon"], "Lachs (roh)", "salmon (raw)"),
        Name("T422100", // Forelle roh
            ["Forelle", "Forellen", "Forellenfilet", "Forellenfilets"],
            ["trout", "trout fillet", "rainbow trout"], "Forelle", "trout"),
        // Garnele roh (87 kcal); frozen is identical in BLS
        Name("T753100", // Garnele/Granat/Krabbe, roh
            ["Garnele", "Garnelen", "Riesengarnele", "Riesengarnelen", "Shrimps", "Scampi", "Crevetten"],
            ["shrimp", "prawn", "prawns", "king prawn", "king prawns", "jumbo shrimp"], "Garnelen (roh)", "prawns (raw)"),
        // Density: FDC 174158 (Water, bottled, generic), 1 cup = 237 g
        Name("N110000", // Trinkwasser
            ["Wasser", "Leitungswasser", "Trinkwasser", "kaltes Wasser", "warmes Wasser", "heißes Wasser", "lauwarmes Wasser", "Wasser kalt", "Wasser warm", "Eiswasser", "Kochendes Wasser"],
            ["water", "tap water", "cold water", "warm water", "hot water", "boiling water", "lukewarm water", "drinking water"], "Wasser", "water", density: 1.002m),
        // Density: FDC 174158 (Water, bottled, generic), 1 cup = 237 g
        // density proxy: FDC bottled water
        Name("N120000", // Natürliches Mineralwasser
            ["Mineralwasser", "Mineralwasser still", "stilles Mineralwasser", "stilles Wasser"],
            ["mineral water", "still water", "still mineral water", "bottled water"], "stilles Mineralwasser", "still mineral water", density: 1.002m),
        // Density: FDC 174158 (Water, bottled, generic), 1 cup = 237 g
        // carbonated mineral water; density proxy: FDC bottled water
        Name("N127000", // Natürliches Mineralwasser mit Kohlensäure
            ["Sprudelwasser", "Sprudel", "Mineralwasser mit Kohlensäure", "Mineralwasser sprudelnd", "Wasser mit Kohlensäure", "Selters", "Selterswasser"],
            ["sparkling water", "sparkling mineral water", "soda water", "club soda", "carbonated water"], "Sprudelwasser", "sparkling water", density: 1.002m),
        // Density: FDC 171869 (Beverages, carbonated, tonic water), 1 fl oz = 30.5 g
        // BLS "Limonade chininhaltig" (41 kcal) = tonic water
        Name("N314000", // Limonade chininhaltig
            ["Tonic Water", "Tonic", "Tonicwater"],
            [], "Tonic Water", "tonic water", density: 1.031m),
        // Density: FDC 169098 (Orange juice, raw (Includes foods for USDA's Food Distributi), 1 cup = 248 g
        Name("F603600", // Orangensaft
            ["Orangensaft"],
            ["orange juice"], "Orangensaft", "orange juice", density: 1.048m),
        // Density: FDC 173933 (Apple juice, canned or bottled, unsweetened, without added a), 1 cup = 248 g
        Name("F110600", // Apfelsaft
            ["Apfelsaft"],
            ["apple juice"], "Apfelsaft", "apple juice", density: 1.048m),
        // Density: FDC 167747 (Lemon juice, raw), 1 cup = 244 g
        Name("F601600", // Zitronensaft
            ["Zitronensaft", "Saft einer Zitrone"],
            ["lemon juice"], "Zitronensaft", "lemon juice", density: 1.031m),
        // Density: FDC 168156 (Lime juice, raw), 1 cup = 242 g
        Name("F602600", // Limettensaft
            ["Limettensaft"],
            ["lime juice"], "Limettensaft", "lime juice", density: 1.023m),
        // Density: FDC 173042 (Grape juice, canned or bottled, unsweetened, without added a), 1 cup = 253 g
        Name("F310600", // Traubensaft
            ["Traubensaft", "Weintraubensaft"],
            ["grape juice"], "Traubensaft", "grape juice", density: 1.069m),
        // Density: FDC 169947 (Pineapple juice, canned or bottled, unsweetened, without add), 1 cup = 250 g
        Name("F501600", // Ananassaft
            ["Ananassaft"],
            ["pineapple juice"], "Ananassaft", "pineapple juice", density: 1.057m),
        // Density: FDC 173039 (Grapefruit juice, white, raw), 1 cup = 247 g
        Name("F604600", // Grapefruitsaft
            ["Grapefruitsaft"],
            ["grapefruit juice"], "Grapefruitsaft", "grapefruit juice", density: 1.044m),
        // Density: FDC 167785 (Mango nectar, canned), 1 cup = 251 g
        // density proxy: FDC mango NECTAR (no mango juice row)
        Name("F516600", // Mangosaft
            ["Mangosaft"],
            ["mango juice"], "Mangosaft", "mango juice", density: 1.061m),
        // Haferdrink ungesüßt; no density (no FDC row for oat drink; ~1.03 expected)
        Name("C660000", // Haferdrink ungesüßt
            ["Haferdrink", "Hafermilch", "Haferdrink ungesüßt"],
            ["oat drink", "oat milk", "oatly"], "Haferdrink (ungesüßt)", "oat drink (unsweetened)"),
        // Density: FDC 173190 (Alcoholic beverage, wine, table, red), 1 fl oz = 29.4 g
        // Rotwein trocken (70 kcal) is the cooking default
        Name("P2A3000", // Rotwein trocken
            ["Rotwein"],
            ["red wine", "red wine dry"], "trockener Rotwein", "dry red wine", density: 0.994m),
        // Density: FDC 174837 (Alcoholic beverage, wine, table, white), 1 fl oz = 29.4 g
        // Weißwein trocken (64 kcal)
        Name("P210000", // Weißwein trocken
            ["Weißwein"],
            ["white wine", "dry white wine"], "trockener Weißwein", "dry white wine", density: 0.994m),
        // Density: FDC 173176 (Alcoholic beverage, wine, dessert, sweet), 1 fl oz = 29.5 g
        // FDC dessert wine, sweet. bare "Port" not listed (Port+er = Porter beer)
        Name("P431000", // Portwein
            ["Portwein"],
            ["port wine"], "Portwein", "port wine", density: 0.997m),
        // Density: FDC 174817 (Alcoholic beverage, distilled, rum, 80 proof), 1 fl oz = 27.8 g
        // Rum 37,5/40 % vol
        Name("P741000", // Rum 37,5/40 % vol
            ["Rum"],
            [], "Rum", "rum", density: 0.940m),
        // Density: FDC 174818 (Alcoholic beverage, distilled, vodka, 80 proof), 1 fl oz = 27.8 g
        Name("P712000", // Wodka
            ["Wodka"],
            ["vodka"], "Wodka", "vodka", density: 0.940m),
        // Density: FDC 174819 (Alcoholic beverage, distilled, whiskey, 86 proof), 1 fl oz = 27.8 g
        Name("P733000", // Whisky/Whiskey
            ["Whisky", "Whiskey"],
            [], "Whisky", "whisky", density: 0.940m),
        // Density: FDC 171919 (Alcoholic beverage, distilled, all (gin, rum, vodka, whiskey), 1 fl oz = 27.8 g
        // FDC generic distilled spirit 86 proof
        Name("P640000", // Calvados
            ["Calvados"],
            [], "Calvados", "calvados", density: 0.940m),
        // Density: FDC 171919 (Alcoholic beverage, distilled, all (gin, rum, vodka, whiskey), 1 fl oz = 27.8 g
        // FDC generic distilled spirit 86 proof
        Name("P610000", // Cognac
            ["Cognac", "Kognak"],
            [], "Cognac", "cognac", density: 0.940m),
        // Density: FDC 171919 (Alcoholic beverage, distilled, all (gin, rum, vodka, whiskey), 1 fl oz = 27.8 g
        Name("P620000", // Weinbrand/Brandy
            ["Weinbrand", "Brandy"],
            [], "Weinbrand", "brandy", density: 0.940m),
        // Density: FDC 171919 (Alcoholic beverage, distilled, all (gin, rum, vodka, whiskey), 1 fl oz = 27.8 g
        // BLS "Obstbrand/Obstwasser" (274 kcal) is a generic fruit spirit; Kirschwasser is within the same range
        Name("P752100", // Obstbrand/Obstwasser
            ["Kirschwasser", "Obstbrand", "Obstwasser"],
            [], "Obstbrand", "fruit brandy", density: 0.940m),
        // Pfeffer schwarz, getrocknet (304 kcal); bare English "pepper" NOT listed: pepper+s = peppers (bell peppers)
        Name("R258100", // Pfeffer schwarz, getrocknet
            ["Pfeffer", "schwarzer Pfeffer", "Pfeffer schwarz", "gemahlener Pfeffer", "Pfefferkörner", "schwarze Pfefferkörner"],
            ["black pepper", "ground black pepper", "peppercorns", "black peppercorns", "ground pepper"], "schwarzer Pfeffer", "black pepper"),
        Name("Q630000", // Süßrahmbutter
            ["Süßrahmbutter"],
            ["sweet cream butter"], "Süßrahmbutter", "sweet cream butter"),
        Name("Q620000", // Sauerrahmbutter
            ["Sauerrahmbutter"],
            ["cultured butter"], "Sauerrahmbutter", "cultured butter"),
        // Unqualified Hackfleisch in a German shop is mixed beef and pork.
        Name("U050100", // Rind/Schwein, Hackfleisch gemischt, roh
            ["Hackfleisch", "gemischtes Hackfleisch", "Hackfleisch gemischt", "Gehacktes", "gemischtes Hack"],
            [], "gemischtes Hackfleisch", "mixed minced meat"),
        Name("M300600", // Hartkäse mind. 45 % Fett i. Tr.
            ["Hartkäse"],
            ["hard cheese"], "Hartkäse 45 %", "hard cheese 45 %"),
        Name("P540100", // Mandellikör
            ["Amaretto", "Mandellikör"],
            ["almond liqueur"], "Mandellikör", "almond liqueur"),
        Name("P546000", // Eierlikör
            ["Eierlikör"],
            ["advocaat", "egg liqueur"], "Eierlikör", "egg liqueur"),
        Name("F401600", // Preiselbeersaft
            ["Preiselbeersaft"],
            ["lingonberry juice"], "Preiselbeersaft", "lingonberry juice"),
        // Broth: one word covers a powder or cube and the liquid, so the entry is the powder and carries
        // the liquid as its alternative; NutritionGrams.Resolve picks one by the line's unit. The liquid
        // is counted by volume at the density of FDC stock rows; at 3 to 5 kcal/100 g the kind of broth
        // (beef, chicken, vegetable) does not matter, but each has its own BLS food where one exists.
        // Bare "Brühe", "Bouillon" or "stock" names no kind and no state, and both blind judges refused
        // it, so only words that say powder or cube are forms of the generic powder.
        Name("R810000", // Bouillon/Brühe/Suppe (Brühwürfel, Pulver)
            ["gekörnte Brühe", "Brühpulver", "Instantbrühe", "Brühwürfel"],
            ["stock cube", "stock cubes", "bouillon powder", "stock powder"],
            "Brühpulver", "stock powder"),
        // Density: FDC 171583 (Soup, vegetable broth, ready to serve), 1 cup = 221 g (0.934, lower than real broth, so it errs low)
        Name("R821000", // Gemüse Bouillon/Brühe/Suppe (Brühwürfel, Pulver)
            ["gekörnte Gemüsebrühe", "Gemüsebrühpulver", "Gemüsebrühwürfel", "Gemüsebrühe", "Gemüsebouillon", "Gemüsesuppenbrühe"],
            ["vegetable stock cube", "vegetable stock cubes", "vegetable bouillon powder", "vegetable stock", "vegetable broth", "vegetable bouillon"],
            "Gemüsebrühpulver", "vegetable stock powder",
            liquid: new LiquidFood("X416243", 0.934m, "Gemüsebrühe (flüssig)", "vegetable stock (liquid)")),
        // Density: FDC 172884 (Soup, stock, chicken, home-prepared), 1 cup = 240 g
        // Geflügelbrühe is the German shop word for chicken broth; BLS has no separate turkey broth
        Name("R822000", // Hühner Bouillon/Brühe/Suppe (Brühwürfel, Pulver)
            ["Hühnerbrühe", "Hühnerbouillon", "Hühnchenbrühe", "Hähnchenbrühe", "Geflügelbrühe", "Hühnerbrühwürfel", "Hühnerbrühpulver", "gekörnte Hühnerbrühe"],
            ["chicken stock", "chicken broth", "chicken bouillon", "chicken stock cube", "chicken stock cubes", "chicken bouillon powder", "chicken stock powder"],
            "Hühnerbrühpulver", "chicken stock powder",
            liquid: new LiquidFood("X411243", 1.014m, "Hühnerbrühe (flüssig)", "chicken stock (liquid)")),
        // Density: FDC 172883 (Soup, stock, beef, home-prepared), 1 cup = 240 g
        // beef and meat broth share one entry because BLS has one meat powder; veal and game broth are not covered
        Name("R811000", // Fleisch Bouillon/Brühe/Suppe (Brühwürfel, Pulver)
            ["Fleischbrühe", "Rinderbrühe", "Rindsbrühe", "Fleischbouillon", "Rinderbouillon", "Rinderbrühwürfel", "Fleischbrühwürfel", "Rinderbrühpulver", "Fleischbrühpulver", "gekörnte Fleischbrühe", "gekörnte Rinderbrühe"],
            ["beef stock", "beef broth", "beef bouillon", "beef stock cube", "beef stock cubes", "beef bouillon powder", "beef stock powder"],
            "Fleischbrühpulver", "beef stock powder",
            liquid: new LiquidFood("Y183013", 1.014m, "Fleischbrühe (flüssig)", "beef stock (liquid)")),
    ];
}
