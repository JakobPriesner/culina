namespace Domain.Nutrition;

/// <summary>One food of a group of alternatives, with what a reader calls it.</summary>
/// <param name="Code">The BLS code.</param>
/// <param name="LabelDe">The German reader-facing name.</param>
/// <param name="LabelEn">The English reader-facing name.</param>
public sealed record FoodVariant(string Code, string LabelDe, string LabelEn);

/// <summary>
/// Which foods are the obvious alternatives to one another: Vollmilch, fettarme Milch, Magermilch;
/// Weizenmehl 405 and 550. Offered as one-tap choices beside a line, so the 1.5 % milk is not buried
/// among 261 foods that contain "Milch".
/// </summary>
/// <remarks>
/// A group holds raw or as-bought foods that can stand in for one another at the same weight: dry
/// never mixes with canned or cooked, fresh herbs never with dried ones, and a concentrate (Tomatenmark)
/// is in no group. A food is in at most one group. Choosing a variant is the household's food
/// correction, so nothing here is applied by itself. Generated from the reviewed table; any change
/// must raise <see cref="Version"/>.
/// </remarks>
public static class FoodVariants
{
    /// <summary>The table's version; raise it with any change to a group.</summary>
    public const int Version = 1;

    private static readonly IReadOnlyList<IReadOnlyList<FoodVariant>> Groups = Entries();

    private static readonly Dictionary<string, IReadOnlyList<FoodVariant>> ByCode =
        Groups.SelectMany(group => group.Select(member => (member.Code, group)))
            .ToDictionary(pair => pair.Code, pair => pair.group);

    /// <summary>Every group, each in the order its alternatives are offered.</summary>
    public static IReadOnlyList<IReadOnlyList<FoodVariant>> All => Groups;

    /// <summary>The group a food is in, the food itself included, or an empty list when it has none.</summary>
    /// <param name="code">The BLS code of the food.</param>
    public static IReadOnlyList<FoodVariant> For(string code) =>
        ByCode.GetValueOrDefault(code) ?? [];

    private static IReadOnlyList<FoodVariant>[] Entries() =>
    [
        // milk
        [
            new("M111300", "Vollmilch 3,5 %", "whole milk 3.5 %"),
            new("M111200", "fettarme Milch 1,5 %", "low-fat milk 1.5 %"),
            new("M111100", "Magermilch", "skimmed milk"),
            new("M113300", "H-Milch 3,5 %", "UHT milk 3.5 %"),
            new("C660000", "Haferdrink (ungesüßt)", "oat drink (unsweetened)"),
            new("H841100", "Sojadrink (ungesüßt)", "soy drink (unsweetened)"),
            new("H800000", "Mandeldrink (ungesüßt)", "almond drink (unsweetened)")
        ],
        // soured_milk
        [
            new("M150000", "Buttermilch", "buttermilk"),
            new("M130300", "Kefir 3,5 %", "kefir 3.5 %"),
            new("M121300", "Dickmilch 3,5 %", "soured milk 3.5 %"),
            new("M130200", "Kefir 1,5 %", "kefir 1.5 %")
        ],
        // cream
        [
            new("M173800", "Schlagsahne 30 %", "whipping cream 30 %"),
            new("M173900", "Schlagsahne 36 %", "heavy cream 36 %"),
            new("M171500", "Kaffeesahne 10 %", "coffee cream 10 %"),
            new("M117700", "Sahneersatz", "cream substitute")
        ],
        // soured_cream
        [
            new("M172700", "Schmand 20 %", "sour cream 20 %"),
            new("M176800", "Crème fraîche 30 %", "crème fraîche 30 %"),
            new("M172500", "saure Sahne 10 %", "sour cream 10 %"),
            new("M172900", "Crème double 40 %", "crème double 40 %")
        ],
        // yoghurt
        [
            new("M141300", "Joghurt 3,5 %", "plain yogurt 3.5 %"),
            new("M141200", "Joghurt 1,5 %", "plain yogurt 1.5 %"),
            new("M141100", "Joghurt 0,5 % (fettfrei)", "plain yogurt 0.5 %"),
            new("M141500", "Sahnejoghurt 10 %", "cream yogurt 10 %"),
            new("H844000", "Sojajoghurt", "soy yogurt"),
            new("M149500", "Schafjoghurt", "sheep's yogurt")
        ],
        // quark
        [
            new("M713100", "Magerquark", "low-fat quark"),
            new("M713300", "Speisequark 20 %", "quark 20 %"),
            new("M713500", "Speisequark 40 %", "quark 40 %"),
            new("M710100", "Skyr", "skyr"),
            new("M713200", "Speisequark 10 %", "quark 10 %"),
            new("M713700", "Sahnequark 50 %", "cream quark 50 %")
        ],
        // cottage_cheese
        [
            new("M711300", "Hüttenkäse 20 %", "cottage cheese 20 %"),
            new("M711100", "Hüttenkäse mager", "cottage cheese, low-fat")
        ],
        // cream_cheese
        [
            new("M710800", "Doppelrahmfrischkäse", "full-fat cream cheese"),
            new("M820500", "Frischkäse 40 %", "cream cheese 40 %"),
            new("M820100", "Frischkäse mager", "low-fat cream cheese"),
            new("M7A6800", "Mascarpone", "mascarpone"),
            new("M741600", "Ricotta", "ricotta")
        ],
        // cheese_sliced
        [
            new("M402600", "Gouda", "gouda"),
            new("M402500", "Gouda 40 %", "gouda 40 %"),
            new("M402400", "Gouda 30 %", "gouda 30 %"),
            new("M401600", "Edamer 45 %", "edam 45 %"),
            new("M303700", "Cheddar", "cheddar"),
            new("M403600", "Tilsiter 45 %", "tilsiter 45 %"),
            new("M501600", "Butterkäse 45 %", "butterkäse 45 %")
        ],
        // cheese_hard
        [
            new("M304600", "Emmentaler", "emmental"),
            new("M306400", "Parmesan", "parmesan"),
            new("M300600", "Hartkäse 45 %", "hard cheese 45 %"),
            new("M302600", "Bergkäse 45 %", "mountain cheese 45 %"),
            new("M301700", "Appenzeller 50 %", "appenzeller 50 %"),
            new("M307700", "Raclettekäse 45 %", "raclette cheese 45 %")
        ],
        // cheese_soft
        [
            new("M602600", "Camembert", "camembert"),
            new("M601800", "Brie 60 %", "brie 60 %"),
            new("M602400", "Camembert 30 %", "camembert 30 %"),
            new("M601600", "Brie 45 %", "brie 45 %")
        ],
        // cheese_blue
        [
            new("M5B1600", "Gorgonzola", "gorgonzola"),
            new("M502700", "Edelpilzkäse 50 %", "blue cheese 50 %"),
            new("M502600", "Edelpilzkäse 45 %", "blue cheese 45 %")
        ],
        // cheese_brine
        [
            new("M012200", "Feta", "feta"),
            new("M052000", "Hirtenkäse 45 %", "brined cheese (cow) 45 %"),
            new("M6A6800", "Schafskäse (Salzlake) 45 %", "brined sheep's cheese 45 %"),
            new("M524500", "Halloumi", "halloumi")
        ],
        // mozzarella
        [
            new("M032100", "Mozzarella (45 %)", "mozzarella (45 %)"),
            new("M0A1000", "Mozzarella light (20 %)", "mozzarella light (20 %)")
        ],
        // butter
        [
            new("Q611000", "Butter", "butter"),
            new("Q6A4000", "gesalzene Butter", "salted butter"),
            new("Q630000", "Süßrahmbutter", "sweet cream butter"),
            new("Q620000", "Sauerrahmbutter", "cultured butter"),
            new("Q640000", "Halbfettbutter", "reduced-fat butter"),
            new("Q656000", "Kräuterbutter", "herb butter")
        ],
        // margarine
        [
            new("Q400000", "Pflanzenmargarine", "margarine"),
            new("Q440000", "Margarine halbfett", "margarine, half-fat"),
            new("Q4A1000", "Margarine dreiviertelfett", "margarine, three-quarter fat"),
            new("Q4B1000", "Pflanzencreme zum Braten", "plant cream for cooking")
        ],
        // cooking_fat
        [
            new("Q683000", "Butterschmalz", "ghee"),
            new("Q860000", "Schweineschmalz", "lard"),
            new("Q820000", "Gänseschmalz", "goose fat"),
            new("Q810000", "Entenfett", "duck fat"),
            new("Q960000", "Frittierfett (pflanzlich)", "frying fat (vegetable)"),
            new("Q902000", "Pflanzenschmalz (vegan)", "vegan plant lard")
        ],
        // oil
        [
            new("Q120000", "Olivenöl", "olive oil"),
            new("Q180000", "Rapsöl", "rapeseed oil"),
            new("Q320000", "Sonnenblumenöl", "sunflower oil"),
            new("Q940000", "Pflanzenöl", "vegetable oil"),
            new("Q550000", "Kokosöl", "coconut oil"),
            new("Q230000", "Sesamöl", "sesame oil"),
            new("Q210000", "Erdnussöl", "peanut oil")
        ],
        // sugar
        [
            new("S111000", "Zucker", "sugar"),
            new("S112000", "brauner Zucker", "brown sugar"),
            new("S111100", "Puderzucker", "icing sugar")
        ],
        // vanilla_sugar
        [
            new("S114000", "Vanillezucker", "vanilla sugar"),
            new("R452000", "Vanillinzucker", "vanillin sugar")
        ],
        // syrup
        [
            new("S120000", "Honig", "honey"),
            new("S151100", "Ahornsirup", "maple syrup"),
            new("S122000", "Waldhonig", "forest honey"),
            new("S152000", "Melasse", "molasses")
        ],
        // salt
        [
            new("R111000", "Speisesalz", "table salt"),
            new("R114000", "Jodsalz", "iodised salt"),
            new("R112000", "Meersalz", "sea salt"),
            new("R111100", "Steinsalz", "rock salt")
        ],
        // jam
        [
            new("S132000", "Konfitüre extra", "jam"),
            new("S131100", "Fruchtaufstrich", "fruit spread"),
            new("S135000", "Zitrusmarmelade", "citrus marmalade"),
            new("S134000", "Gelee extra", "jelly"),
            new("S132100", "Konfitüre zuckerreduziert", "reduced-sugar jam")
        ],
        // ice_cream
        [
            new("S241000", "Vanilleeis", "vanilla ice cream"),
            new("S242000", "Schokoladeneis", "chocolate ice cream"),
            new("S220000", "Fruchteis", "fruit ice cream"),
            new("S221700", "Joghurteis", "frozen yogurt")
        ],
        // chocolate
        [
            new("S560000", "Zartbitterschokolade", "dark chocolate"),
            new("S539900", "Vollmilchschokolade", "milk chocolate"),
            new("S580000", "weiße Schokolade", "white chocolate"),
            new("S570000", "Bitterschokolade", "bitter chocolate"),
            new("S500000", "Schokolade", "chocolate"),
            new("S671000", "Zartbitterkuvertüre", "dark couverture")
        ],
        // cocoa
        [
            new("S711000", "Kakaopulver (schwach entölt)", "cocoa powder (lightly de-oiled)"),
            new("S713000", "Kakaopulver (stark entölt)", "cocoa powder (heavily de-oiled)")
        ],
        // pudding_powder
        [
            new("R481100", "Vanillepuddingpulver", "vanilla pudding powder"),
            new("R481200", "Schokopuddingpulver", "chocolate pudding powder")
        ],
        // starch
        [
            new("C446000", "Speisestärke (Mais)", "cornflour (cornstarch)"),
            new("K230000", "Kartoffelstärke", "potato starch"),
            new("C216000", "Weizenstärke", "wheat starch"),
            new("C456000", "Reisstärke", "rice starch"),
            new("K512000", "Tapiokastärke", "tapioca starch")
        ],
        // flour_wheat
        [
            new("C214100", "Weizenmehl Type 405", "wheat flour (type 405)"),
            new("C214200", "Weizenmehl Type 550", "wheat flour (type 550)"),
            new("C213200", "Weizenmehl Type 1050", "wheat flour (type 1050)"),
            new("C211000", "Weizenvollkornmehl", "whole wheat flour"),
            new("C234000", "Dinkelmehl Type 630", "spelt flour (type 630)"),
            new("C234200", "Dinkelmehl Type 1050", "spelt flour (type 1050)"),
            new("C235000", "Dinkelvollkornmehl", "whole spelt flour")
        ],
        // flour_rye
        [
            new("C223300", "Roggenmehl Type 1150", "rye flour (type 1150)"),
            new("C223200", "Roggenmehl Type 997", "rye flour (type 997)"),
            new("C223400", "Roggenmehl Type 1370", "rye flour (type 1370)"),
            new("C223100", "Roggenmehl Type 815", "rye flour (type 815)"),
            new("C221000", "Roggenvollkornmehl", "whole rye flour")
        ],
        // flour_other
        [
            new("C453000", "Reismehl", "rice flour"),
            new("H720400", "Kichererbsenmehl", "chickpea flour"),
            new("C424000", "Buchweizenmehl", "buckwheat flour"),
            new("C233000", "Hafermehl", "oat flour"),
            new("C443000", "Maismehl", "corn flour"),
            new("C433000", "Hirsemehl", "millet flour")
        ],
        // flakes
        [
            new("C133000", "Haferflocken", "rolled oats"),
            new("C236100", "Dinkelflocken", "spelt flakes"),
            new("C113000", "Weizenflocken", "wheat flakes"),
            new("C123000", "Roggenflocken", "rye flakes"),
            new("C333100", "Hirseflocken", "millet flakes"),
            new("C143000", "Gerstenflocken", "barley flakes")
        ],
        // semolina
        [
            new("C218000", "Weichweizengrieß", "wheat semolina"),
            new("C219300", "Hartweizengrieß", "durum semolina"),
            new("C238000", "Dinkelgrieß", "spelt semolina"),
            new("C346000", "Maisgrieß (Polenta)", "cornmeal (polenta)"),
            new("C356000", "Reisgrieß", "rice semolina")
        ],
        // rice
        [
            new("C352000", "weißer Reis (roh)", "white rice (raw)"),
            new("C351000", "Naturreis (roh)", "brown rice (raw)"),
            new("C359000", "Parboiled-Reis (roh)", "parboiled rice (raw)"),
            new("C353100", "Wildreis", "wild rice"),
            new("C354100", "Wildreis-Mischung (roh)", "wild rice mix (raw)")
        ],
        // grains
        [
            new("C119200", "Couscous (trocken)", "couscous (dry)"),
            new("C119100", "Bulgur (trocken)", "bulgur (dry)"),
            new("C118000", "Quinoa (trocken)", "quinoa (dry)"),
            new("C332000", "Hirse", "millet"),
            new("C322000", "Buchweizen", "buckwheat")
        ],
        // pasta
        [
            new("E401000", "Nudeln ohne Ei (trocken)", "dried pasta without egg"),
            new("E510000", "Vollkornnudeln (trocken)", "wholewheat pasta (dried)"),
            new("E432000", "Eiernudeln (trocken)", "egg noodles (dried)"),
            new("E612000", "Nudeln glutenfrei (trocken)", "gluten-free pasta (dried)")
        ],
        // broth
        [
            new("R821000", "Gemüsebrühpulver", "vegetable stock powder"),
            new("R810000", "Brühpulver", "stock powder"),
            new("R822000", "Hühnerbrühpulver", "chicken stock powder"),
            new("R811000", "Fleischbrühpulver", "beef stock powder")
        ],
        // mustard
        [
            new("R132000", "mittelscharfer Senf", "medium mustard"),
            new("R133000", "scharfer Senf", "hot mustard"),
            new("R135000", "süßer Senf", "sweet mustard"),
            new("R134000", "extra scharfer Senf", "extra hot mustard")
        ],
        // vinegar
        [
            new("R121000", "Weinessig", "wine vinegar"),
            new("R122000", "Branntweinessig", "white vinegar"),
            new("R123100", "Apfelessig", "apple cider vinegar"),
            new("R125000", "Balsamicoessig", "balsamic vinegar"),
            new("R124000", "Kräuteressig", "herb vinegar")
        ],
        // mayo
        [
            new("Q991000", "Mayonnaise 80 %", "mayonnaise 80 %"),
            new("Q993000", "Salatmayonnaise", "salad mayonnaise"),
            new("Q999000", "Remoulade", "remoulade")
        ],
        // wine
        [
            new("P2A3000", "trockener Rotwein", "dry red wine"),
            new("P210000", "trockener Weißwein", "dry white wine"),
            new("P220000", "halbtrockener Weißwein", "medium-dry white wine"),
            new("P253000", "halbtrockener Rotwein", "medium-dry red wine"),
            new("P2A6000", "Sekt (trocken)", "sparkling wine (dry)"),
            new("P431000", "Portwein", "port wine"),
            new("P462000", "Sherry medium dry", "medium dry sherry")
        ],
        // spirits
        [
            new("P741000", "Rum", "rum"),
            new("P712000", "Wodka", "vodka"),
            new("P733000", "Whisky", "whisky"),
            new("P640000", "Calvados", "calvados"),
            new("P610000", "Cognac", "cognac"),
            new("P620000", "Weinbrand", "brandy"),
            new("P752100", "Obstbrand", "fruit brandy")
        ],
        // liqueur
        [
            new("P540100", "Mandellikör", "almond liqueur"),
            new("P546000", "Eierlikör", "egg liqueur"),
            new("P5A1000", "Sahnelikör", "cream liqueur"),
            new("P550000", "Kaffeelikör", "coffee liqueur")
        ],
        // juice
        [
            new("F603600", "Orangensaft", "orange juice"),
            new("F110600", "Apfelsaft", "apple juice"),
            new("F310600", "Traubensaft", "grape juice"),
            new("F501600", "Ananassaft", "pineapple juice"),
            new("F604600", "Grapefruitsaft", "grapefruit juice"),
            new("F516600", "Mangosaft", "mango juice"),
            new("F401600", "Preiselbeersaft", "lingonberry juice")
        ],
        // citrus_juice
        [
            new("F601600", "Zitronensaft", "lemon juice"),
            new("F602600", "Limettensaft", "lime juice")
        ],
        // citrus
        [
            new("F601100", "Zitrone", "lemon"),
            new("F602100", "Limette", "lime"),
            new("F603100", "Orange", "orange"),
            new("F604100", "Grapefruit", "grapefruit"),
            new("F606100", "Mandarine", "mandarin"),
            new("F607100", "Clementine", "clementine")
        ],
        // berries
        [
            new("F301100", "Erdbeere", "strawberry"),
            new("F302100", "Himbeere", "raspberry"),
            new("F304100", "Heidelbeere", "blueberry"),
            new("F303100", "Brombeere", "blackberry"),
            new("F321100", "rote Johannisbeere", "redcurrant"),
            new("F305100", "Stachelbeere", "gooseberry")
        ],
        // pome
        [
            new("F110100", "Apfel", "apple"),
            new("F130100", "Birne", "pear"),
            new("F140100", "Quitte", "quince")
        ],
        // stone_fruit
        [
            new("F223100", "Zwetschge", "plum"),
            new("F220100", "Pflaume", "plum (fresh)"),
            new("F211100", "Süßkirsche", "sweet cherry"),
            new("F212100", "Sauerkirsche", "sour cherry"),
            new("F203100", "Pfirsich", "peach"),
            new("F201100", "Aprikose", "apricot"),
            new("F202100", "Nektarine", "nectarine")
        ],
        // dried_fruit
        [
            new("F840100", "Rosinen", "raisins"),
            new("F414400", "Cranberries (getrocknet)", "dried cranberries"),
            new("F504400", "Datteln (getrocknet)", "dates (dried)"),
            new("F201400", "Aprikosen (getrocknet)", "apricots (dried)"),
            new("F505400", "Feigen (getrocknet)", "figs (dried)"),
            new("F220400", "Backpflaumen", "prunes")
        ],
        // nuts
        [
            new("H210100", "Mandeln", "almonds"),
            new("H130100", "Haselnüsse", "hazelnuts"),
            new("H120100", "Walnüsse", "walnuts"),
            new("H170100", "Cashewkerne", "cashew nuts"),
            new("H250100", "Pistazien", "pistachios"),
            new("H160100", "Pekannüsse", "pecans"),
            new("H320100", "Pinienkerne", "pine nuts")
        ],
        // nuts_ground
        [
            new("H210400", "gemahlene Mandeln", "ground almonds"),
            new("H130400", "gemahlene Haselnüsse", "ground hazelnuts"),
            new("H120400", "gemahlene Walnüsse", "ground walnuts")
        ],
        // seeds
        [
            new("H430100", "Sonnenblumenkerne", "sunflower seeds"),
            new("H310100", "Kürbiskerne", "pumpkin seeds"),
            new("H410100", "Leinsamen", "flaxseed"),
            new("H480100", "Chiasamen", "chia seeds"),
            new("H420100", "Sesamsamen", "sesame seeds"),
            new("H450100", "Mohn", "poppy seeds")
        ],
        // nut_butter
        [
            new("H880200", "Erdnussbutter", "peanut butter"),
            new("H212800", "Mandelmus", "almond butter"),
            new("Q901000", "Tahin", "tahini"),
            new("F960800", "Cashewmus", "cashew butter"),
            new("H130800", "Haselnussmus", "hazelnut butter")
        ],
        // peanuts
        [
            new("H110600", "Erdnüsse (geröstet)", "peanuts (roasted)"),
            new("H110700", "gesalzene Erdnüsse", "salted peanuts")
        ],
        // pulses_dry
        [
            new("H725100", "Linsen (trocken)", "lentils (dry)"),
            new("H730000", "rote Linsen (trocken)", "red lentils (dry)"),
            new("G770400", "Kichererbsen (trocken)", "chickpeas (dry)"),
            new("H742100", "Kidneybohnen (trocken)", "kidney beans (dry)"),
            new("H739400", "weiße Bohnen (trocken)", "white beans (dry)"),
            new("G760400", "Erbsen (getrocknet)", "split peas (dried)")
        ],
        // pulses_canned
        [
            new("H742902", "Kidneybohnen (gekocht)", "kidney beans (cooked)"),
            new("H720902", "Kichererbsen (gekocht)", "chickpeas (cooked)"),
            new("H730902", "Linsen (gekocht)", "lentils (cooked)"),
            new("H740902", "weiße Bohnen (gekocht)", "white beans (cooked)")
        ],
        // tofu
        [
            new("H861000", "Tofu", "tofu"),
            new("H861100", "Seidentofu", "silken tofu")
        ],
        // tomato
        [
            new("G561100", "frische Tomate", "fresh tomato"),
            new("G568900", "Tomaten aus der Dose", "canned tomatoes"),
            new("R161200", "passierte Tomaten", "passata")
        ],
        // onion
        [
            new("G480100", "Zwiebel", "onion"),
            new("G485100", "Schalotte", "shallot"),
            new("G482100", "Frühlingszwiebel", "spring onion"),
            new("G470100", "Lauch", "leek")
        ],
        // bell_pepper
        [
            new("G543100", "rote Paprika", "red bell pepper"),
            new("G542100", "gelbe Paprika", "yellow bell pepper"),
            new("G541100", "grüne Paprika", "green bell pepper")
        ],
        // chilli
        [
            new("G554100", "rote Chilischote", "red chilli"),
            new("G553100", "grüne Chilischote", "green chilli")
        ],
        // cabbage
        [
            new("G342100", "Weißkohl", "white cabbage"),
            new("G341100", "Rotkohl", "red cabbage"),
            new("G343100", "Wirsing", "savoy cabbage"),
            new("G344100", "Spitzkohl", "pointed cabbage"),
            new("G321100", "Chinakohl", "chinese cabbage"),
            new("G324100", "Pak Choi", "pak choi")
        ],
        // florets
        [
            new("G311100", "Blumenkohl", "cauliflower"),
            new("G312100", "Brokkoli", "broccoli"),
            new("G350100", "Romanesco", "romanesco"),
            new("G332100", "Rosenkohl", "brussels sprouts"),
            new("G331100", "Kohlrabi", "kohlrabi")
        ],
        // root_veg
        [
            new("G620100", "Karotte", "carrot"),
            new("G640100", "Pastinake", "parsnip"),
            new("G660100", "Knollensellerie", "celeriac"),
            new("G613100", "Rote Bete (roh)", "beetroot (raw)")
        ],
        // potato
        [
            new("K110100", "Kartoffel", "potato"),
            new("K120100", "Kartoffel (ungeschält)", "potato (unpeeled)"),
            new("K420100", "Süßkartoffel", "sweet potato")
        ],
        // salad
        [
            new("G105100", "Kopfsalat", "lettuce"),
            new("G103100", "Eisbergsalat", "iceberg lettuce"),
            new("G104100", "Feldsalat", "lamb's lettuce"),
            new("G130100", "Rucola", "rocket"),
            new("G106100", "Radicchio", "radicchio"),
            new("G107100", "Romanasalat", "romaine lettuce"),
            new("G102100", "Endivie", "endive")
        ],
        // spinach
        [
            new("G211100", "frischer Spinat", "fresh spinach"),
            new("G211200", "Tiefkühlspinat", "frozen spinach"),
            new("G230100", "Mangold", "chard")
        ],
        // green_veg
        [
            new("G760200", "Erbsen (tiefgekühlt)", "peas (frozen)"),
            new("G760100", "Erbsen (frisch)", "peas (fresh)"),
            new("G760902", "Erbsen (Dose)", "peas (canned)"),
            new("G761100", "Zuckerschoten", "sugar snap peas"),
            new("G710100", "grüne Bohnen", "green beans")
        ],
        // corn
        [
            new("G570902", "Zuckermais (Dose)", "sweetcorn (canned)"),
            new("G570100", "Zuckermais (frisch)", "sweetcorn (fresh)"),
            new("G570200", "Zuckermais (tiefgekühlt)", "sweetcorn (frozen)")
        ],
        // squash
        [
            new("G582100", "Zucchini", "courgette"),
            new("G510100", "Aubergine", "aubergine"),
            new("G581000", "Hokkaido-Kürbis", "hokkaido squash"),
            new("G581100", "Kürbis (Pumpkin)", "pumpkin")
        ],
        // pickles
        [
            new("G520802", "Gewürzgurke", "pickled gherkin"),
            new("G890702", "Salzgurke (Dillgurke)", "dill pickle (brined)"),
            new("G891202", "Senfgurke", "mustard pickle")
        ],
        // mushroom
        [
            new("K701100", "Champignons", "button mushrooms"),
            new("K713100", "Pfifferlinge", "chanterelles"),
            new("K718100", "Steinpilze", "porcini"),
            new("K740100", "Austernpilze", "oyster mushrooms"),
            new("K724100", "Shiitake", "shiitake")
        ],
        // herbs
        [
            new("G250100", "Petersilie", "parsley"),
            new("G081100", "Schnittlauch", "chives"),
            new("G061000", "Basilikum", "basil"),
            new("G492100", "Bärlauch", "wild garlic"),
            new("G280100", "Gartenkresse", "garden cress")
        ],
        // mince
        [
            new("U050100", "gemischtes Hackfleisch", "mixed minced meat"),
            new("U010100", "Rinderhackfleisch", "minced beef"),
            new("U020100", "Schweinehackfleisch", "minced pork"),
            new("U030100", "Kalbshackfleisch", "minced veal")
        ],
        // pork_cuts
        [
            new("U541100", "Schweineschnitzel", "pork escalope"),
            new("U611100", "Schweinefilet", "pork tenderloin"),
            new("U622100", "Schweinekotelett", "pork chop"),
            new("U632100", "Schweinenacken", "pork neck"),
            new("U642100", "Schweinebauch", "pork belly"),
            new("U665100", "Schweineschulter", "pork shoulder"),
            new("U554100", "Schweinegulasch", "pork goulash")
        ],
        // beef_cuts
        [
            new("U151100", "Rindergulasch (Bug)", "beef goulash (shoulder)"),
            new("U284100", "Rinderhüftsteak", "beef rump steak"),
            new("U211100", "Rinderfilet", "beef fillet"),
            new("U221100", "Roastbeef", "roast beef"),
            new("U163100", "Rinderroulade", "beef roulade"),
            new("U171100", "Rinderbraten (Bug)", "beef roast (shoulder)"),
            new("U255100", "Rinderbrust", "beef brisket")
        ],
        // veal_cuts
        [
            new("U342100", "Kalbsschnitzel", "veal escalope"),
            new("U411100", "Kalbsfilet", "veal fillet"),
            new("U351100", "Kalbsgulasch", "veal goulash"),
            new("U371100", "Kalbsbraten (Bug)", "veal roast (shoulder)"),
            new("U362100", "Kalbsroulade", "veal roulade"),
            new("U471100", "Kalbshaxe", "veal shank")
        ],
        // poultry
        [
            new("V416100", "Hähnchenbrustfilet", "chicken breast"),
            new("V486100", "Putenbrust", "turkey breast"),
            new("V4A5100", "Hähnchenoberschenkel", "chicken thigh"),
            new("V4B5100", "Hähnchenunterschenkel", "chicken drumstick"),
            new("V485100", "Putenkeule", "turkey leg")
        ],
        // ham_bacon
        [
            new("W424000", "Kochschinken", "cooked ham"),
            new("W442000", "Schwarzwälder Schinken", "black forest ham"),
            new("W441000", "Parmaschinken", "parma ham"),
            new("W415000", "Frühstücksspeck", "streaky bacon"),
            new("W411300", "Bauchspeck", "belly bacon"),
            new("W140000", "Salami", "salami"),
            new("W410400", "Schinkenspeck", "smoked ham bacon")
        ],
        // fish_oily
        [
            new("T410100", "Lachs (roh)", "salmon (raw)"),
            new("T422100", "Forelle", "trout"),
            new("T107100", "Makrele", "mackerel"),
            new("T102100", "Hering", "herring"),
            new("T121100", "Thunfisch", "tuna")
        ],
        // fish_white
        [
            new("T204100", "Kabeljau", "cod"),
            new("T207100", "Seelachs", "pollock"),
            new("T213100", "Alaska-Seelachs", "alaska pollock"),
            new("T305100", "Scholle", "plaice"),
            new("T603100", "Zander", "pike-perch"),
            new("T507100", "Pangasius", "pangasius"),
            new("T615100", "Rotbarsch", "redfish")
        ],
    ];
}
