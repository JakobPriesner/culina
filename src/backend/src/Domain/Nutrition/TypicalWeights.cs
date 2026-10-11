namespace Domain.Nutrition;

/// <summary>
/// What one unit of a food typically weighs: the grams of one onion, one clove, one tablespoon of
/// butter, one pack of Vanillezucker.
/// </summary>
/// <param name="Code">The BLS code of the food the unit is counted as the weight of.</param>
/// <param name="UnitKey">The canonical unit, see <see cref="UnitKeys"/>.</param>
/// <param name="Grams">Grams of one unit.</param>
/// <param name="Source">Where the value comes from; shown to the reader, so it is written as a citation.</param>
/// <param name="CountAs">
/// The BLS code of the food a line in this unit really is, when it is not the food named: a can of
/// "Tomaten" is canned tomatoes, not fresh ones. Null when the food is the one counted.
/// </param>
public sealed record TypicalWeight(string Code, string UnitKey, decimal Grams, string Source, string? CountAs = null);

/// <summary>
/// The fifth table of food words and numbers: a curated, cited typical weight per food and unit, so a
/// count ("2 Zwiebeln"), a spoon of a solid or a household unit can be counted as an estimate.
/// </summary>
/// <remarks>
/// Values come from USDA FoodData Central (public domain, CC0) portion rows and from pack sizes
/// printed on the pack or the can; none is copied from a share-alike database. A line counted from
/// here is an estimate and is shown as one. Generated from the reviewed table, one row per (food,
/// unit); the comment above a row is what the curator noted. Any change must raise <see cref="Version"/>.
/// </remarks>
public static class TypicalWeights
{
    /// <summary>The table's version; raise it with any change to a row.</summary>
    public const int Version = 1;

    private static readonly TypicalWeight[] Rows = Entries();

    private static readonly Dictionary<(string Code, string UnitKey), TypicalWeight> ByFood =
        Rows.ToDictionary(weight => (weight.Code, weight.UnitKey));

    /// <summary>Every row, in the order they are written below.</summary>
    public static IReadOnlyList<TypicalWeight> All => Rows;

    /// <summary>The typical weight of one unit of a food, or null when there is none.</summary>
    /// <param name="code">The BLS code of the food.</param>
    /// <param name="unitKey">The canonical unit, see <see cref="UnitKeys"/>.</param>
    public static TypicalWeight? Find(string code, string unitKey) =>
        ByFood.GetValueOrDefault((code, unitKey));

    private static TypicalWeight[] Entries() =>
    [
        // Zwiebel: a German Zwiebel is 70-150 g; medium of FDC's three sizes
        new("G480100", "piece", 110m, "FDC 170000 Onions, raw: 1 medium (2-1/2\" dia) = 110 g (small 70, large 150)"),
        // Frühlingszwiebel: German Lauchzwiebeln are often thicker (FDC large = 25 g); medium kept for consistency
        new("G482100", "piece", 15m, "FDC 170005 Onions, spring or scallions (includes tops and bulb), raw: 1 medium (4-1/8\" long) = 15 g"),
        // Frühlingszwiebel: a Stange = one plant
        new("G482100", "stick", 15m, "FDC 170005 Onions, spring or scallions, raw: 1 medium (4-1/8\" long) = 15 g"),
        // Staudensellerie: Stange Staudensellerie
        new("G220100", "stick", 40m, "FDC 169988 Celery, raw: 1 stalk, medium (7-1/2\" - 8\" long) = 40 g (small 17, large 64)"),
        // Karotte: German Karotten often run 80-120 g; this is FDC's medium and probably low
        new("G620100", "piece", 61m, "FDC 170393 Carrots, raw: 1 medium = 61 g (small 50, large 72)"),
        // frische Tomate
        new("G561100", "piece", 123m, "FDC 170457 Tomatoes, red, ripe, raw: 1 medium whole (2-3/5\" dia) = 123 g (small 91, large 182)"),
        // frische Tomate: A can of "Tomaten" is counted as canned tomatoes (G568900), not as raw tomatoes; net weight incl. juice.
        new("G561100", "can", 400m, "übliche Packungsgröße, auf der Dose angegeben: Dose Tomaten (gehackt oder geschält) 400 g Nettofüllmenge", "G568900"),
        // Tomaten aus der Dose: net weight incl. juice, which is used with the tomatoes
        new("G568900", "can", 400m, "übliche Packungsgröße, auf der Dose angegeben: Dose geschälte Tomaten 400 g Nettofüllmenge"),
        // rote Paprika: large chosen: a German Paprikaschote is about 200 g as bought
        new("G543100", "piece", 164m, "FDC 170108 Peppers, sweet, red, raw: 1 large (2-1/4 per pound, approx 3-3/4\" long, 3\" dia) = 164 g (medium 119)"),
        // grüne Paprika: as red
        new("G541100", "piece", 164m, "FDC 170427 Peppers, sweet, green, raw: 1 large (2-1/4 per lb) = 164 g (medium 119)"),
        // gelbe Paprika: only row FDC has for yellow
        new("G542100", "piece", 186m, "FDC 169383 Peppers, sweet, yellow, raw: 1 pepper, large (3-3/4\" long, 3\" dia) = 186 g"),
        // Zucchini: wide spread; medium
        new("G582100", "piece", 196m, "FDC 169291 Squash, summer, zucchini, includes skin, raw: 1 medium = 196 g (small 118, large 323)"),
        // Gurke: Salatgurke
        new("G520100", "piece", 301m, "FDC 168409 Cucumber, with peel, raw: 1 cucumber (8-1/4\") = 301 g"),
        // Apfel: edible portion
        new("F110100", "piece", 182m, "FDC 171688 Apples, raw, with skin: 1 medium (3\" dia) = 182 g (small 149, large 223)"),
        // Birne
        new("F130100", "piece", 178m, "FDC 169118 Pears, raw: 1 medium = 178 g (small 148, large 230)"),
        // Banane: peeled (edible) weight
        new("F503100", "piece", 118m, "FDC 173944 Bananas, raw: 1 medium (7\" to 7-7/8\" long) = 118 g (small 101, large 136)"),
        // Orange: edible portion, without peel
        new("F603100", "piece", 131m, "FDC 169097 Oranges, raw, all commercial varieties: 1 fruit (2-5/8\" dia) = 131 g"),
        // Zitrone: edible pulp without peel
        new("F601100", "piece", 58m, "FDC 167746 Lemons, raw, without peel: 1 fruit (2-1/8\" dia) = 58 g"),
        // Limette
        new("F602100", "piece", 67m, "FDC 168155 Limes, raw: 1 fruit (2\" dia) = 67 g"),
        // Avocado: Hass
        new("F502100", "piece", 136m, "FDC 171706 Avocados, raw, California: 1 fruit, without skin and seed = 136 g"),
        // Fenchel: Knollenfenchel
        new("G431100", "piece", 234m, "FDC 169385 Fennel, bulb, raw: 1 bulb = 234 g"),
        // Champignons
        new("K701100", "piece", 18m, "FDC 169251 Mushrooms, white, raw: 1 medium = 18 g (small 10, large 23)"),
        // Knoblauch
        new("G490100", "clove", 3m, "FDC 169230 Garlic, raw: 1 clove = 3 g"),
        // Basilikum: 1 Blatt = 0.5 g
        new("G061000", "leaf", 0.5m, "FDC 172232 Basil, fresh: 5 leaves = 2.5 g"),
        // frischer Ingwer: 11 g / 5
        new("R211200", "slice", 2.2m, "FDC 169231 Ginger root, raw: 5 slices (1\" dia) = 11 g"),
        // Gelatine: 10 g / 6; other brands print up to 2 g per Blatt; from memory of the pack, not re-checked online
        new("R468000", "leaf", 1.7m, "übliche Packungsgröße, auf der Packung angegeben: Gelatine weiß, 6 Blatt = 10 g (z. B. Dr. Oetker)"),
        // Vanillezucker: from memory of the pack, not re-checked online
        new("S114000", "pack", 8m, "übliche Packungsgröße, auf der Packung angegeben: Päckchen Vanillezucker (z. B. Dr. Oetker Bourbon-Vanille Zucker) 8 g"),
        // Vanillezucker
        new("S114000", "piece", 8m, "as pack: a bare '1 Vanillezucker' is a Päckchen"),
        // Vanillinzucker: from memory of the pack
        new("R452000", "pack", 8m, "übliche Packungsgröße, auf der Packung angegeben: Päckchen Vanillinzucker (z. B. Dr. Oetker Vanillin-Zucker) 8 g"),
        // Vanillinzucker
        new("R452000", "piece", 8m, "as pack"),
        // Backpulver: from memory of the pack
        new("R421100", "pack", 15m, "übliche Packungsgröße, auf der Packung angegeben: Päckchen Backpulver (z. B. Dr. Oetker Backin) 15 g, für 500 g Mehl"),
        // Backpulver
        new("R421100", "piece", 15m, "as pack: a bare '1 Backpulver' is a Päckchen"),
        // Sahnesteif: from memory of the pack
        new("R434000", "pack", 8m, "übliche Packungsgröße, auf der Packung angegeben: Päckchen Sahnesteif (z. B. Dr. Oetker) 8 g"),
        // Sahnesteif
        new("R434000", "piece", 8m, "as pack"),
        // Vanillepuddingpulver: from memory of the pack
        new("R481100", "pack", 37m, "übliche Packungsgröße, auf der Packung angegeben: Päckchen Puddingpulver Vanille 37 g, für 500 ml Milch (z. B. Dr. Oetker)"),
        // Frischhefe
        new("R459000", "cube", 42m, "übliche Packungsgröße, auf der Packung angegeben: Würfel Frischhefe 42 g"),
        // Frischhefe
        new("R459000", "piece", 42m, "as cube: a bare '1 Frischhefe' is a Würfel"),
        // Trockenhefe: FDC 175043 active dry: 1 packet = 7.2 g agrees
        new("R458000", "pack", 7m, "übliche Packungsgröße, auf der Packung angegeben: Päckchen Trockenhefe 7 g, für 500 g Mehl (z. B. Dr. Oetker Backhefe)"),
        // Blätterteig: from memory of the pack; other makes 270-300 g
        new("D072000", "pack", 275m, "übliche Packungsgröße, auf der Packung angegeben: frischer Blätterteig 270–280 g je nach Marke (Tante Fanny 270 g, Henglein 275 g, Tante Fanny Butter 280 g); 275 g als Mitte"),
        // Schlagsahne 30 %: common cup; larger sizes exist
        new("M173800", "tub", 200m, "übliche Packungsgröße, auf dem Becher angegeben: Becher Schlagsahne 200 g"),
        // Crème fraîche 30 %: common cup; 125-200 g exist
        new("M176800", "tub", 150m, "übliche Packungsgröße, auf dem Becher angegeben: Becher Crème fraîche 150 g"),
        // Butter
        new("Q611000", "pack", 250m, "übliche Packungsgröße, auf der Packung angegeben: Päckchen/Paket Butter 250 g"),
        // gesalzene Butter
        new("Q6A4000", "pack", 250m, "as Butter: 250 g"),
        // Süßrahmbutter
        new("Q630000", "pack", 250m, "as Butter: 250 g"),
        // Sauerrahmbutter
        new("Q620000", "pack", 250m, "as Butter: 250 g"),
        // Zuckermais (Dose): DRAINED weight, because the BLS entry is abgetropft; from memory of the can
        new("G570902", "can", 285m, "übliche Packungsgröße, auf der Dose angegeben: Dose Zuckermais, Abtropfgewicht 285 g (Nettofüllmenge 340 g)"),
        // Kidneybohnen (gekocht): DRAINED weight; from memory of the can
        new("H742902", "can", 240m, "übliche Packungsgröße, auf der Dose angegeben: Dose Kidneybohnen, Abtropfgewicht 240 g (Nettofüllmenge 400 g)"),
        // Butter
        new("Q611000", "tbsp", 14.2m, "FDC 173430 Butter, without salt: 1 tbsp = 14.2 g"),
        // Butter: derived by 1/3
        new("Q611000", "tsp", 4.7m, "FDC 173430 Butter, without salt: 1 tbsp = 14.2 g, tsp = tbsp / 3"),
        // gesalzene Butter
        new("Q6A4000", "tbsp", 14.2m, "FDC 173410 Butter, salted: 1 tbsp = 14.2 g"),
        // gesalzene Butter: derived
        new("Q6A4000", "tsp", 4.7m, "FDC 173410: tbsp / 3"),
        // Süßrahmbutter
        new("Q630000", "tbsp", 14.2m, "FDC 173430 Butter, without salt: 1 tbsp = 14.2 g"),
        // Süßrahmbutter: derived
        new("Q630000", "tsp", 4.7m, "FDC 173430: tbsp / 3"),
        // Sauerrahmbutter
        new("Q620000", "tbsp", 14.2m, "FDC 173430 Butter, without salt: 1 tbsp = 14.2 g"),
        // Sauerrahmbutter: derived
        new("Q620000", "tsp", 4.7m, "FDC 173430: tbsp / 3"),
        // Pflanzenmargarine
        new("Q400000", "tbsp", 14m, "FDC 172346 Margarine, regular, 80% fat, composite, stick, with salt: 1 tbsp = 14 g"),
        // Pflanzenmargarine
        new("Q400000", "tsp", 4.7m, "FDC 172346 Margarine, regular, 80% fat: 1 tsp = 4.7 g"),
        // Zucker
        new("S111000", "tsp", 4.2m, "FDC 169655 Sugars, granulated: 1 tsp = 4.2 g"),
        // Zucker: derived
        new("S111000", "tbsp", 12.6m, "FDC 169655 Sugars, granulated: 1 tsp = 4.2 g x 3 (cup 200 g / 16 = 12.5 agrees)"),
        // Puderzucker
        new("S111100", "tbsp", 8m, "FDC 169656 Sugars, powdered: 1 tbsp unsifted = 8 g"),
        // Puderzucker
        new("S111100", "tsp", 2.5m, "FDC 169656 Sugars, powdered: 1 tsp = 2.5 g"),
        // brauner Zucker: unpacked; packed is 4.6 g (+53 %)
        new("S112000", "tsp", 3m, "FDC 168833 Sugars, brown: 1 tsp unpacked = 3 g"),
        // brauner Zucker: derived; unpacked
        new("S112000", "tbsp", 9m, "FDC 168833 Sugars, brown: 1 tsp unpacked = 3 g x 3 (cup unpacked 145 g / 16 = 9.1 agrees)"),
        // Weizenmehl Type 405: derived from the cup row; level and unsifted
        new("C214100", "tbsp", 7.8m, "FDC 168894 Wheat flour, white, all-purpose, enriched, bleached: 1 cup = 125 g, / 16 = 7.8 g"),
        // Weizenmehl Type 405: derived
        new("C214100", "tsp", 2.6m, "FDC 168894: 1 cup = 125 g / 48 = 2.6 g"),
        // Speisestärke (Mais): derived from the cup row
        new("C446000", "tbsp", 8m, "FDC 169698 Cornstarch: 1 cup = 128 g / 16 = 8 g"),
        // Speisestärke (Mais): derived
        new("C446000", "tsp", 2.7m, "FDC 169698 Cornstarch: 1 cup = 128 g / 48 = 2.7 g"),
        // Speisesalz
        new("R111000", "tsp", 6m, "FDC 173468 Salt, table: 1 tsp = 6 g"),
        // Speisesalz
        new("R111000", "tbsp", 18m, "FDC 173468 Salt, table: 1 tbsp = 18 g"),
        // Speisesalz: FDC's dash used for a Prise (both about 1/16 tsp = 0.375 g); a gesture, +-50 %, but salt is in nearly every recipe
        new("R111000", "pinch", 0.4m, "FDC 173468 Salt, table: 1 dash = 0.4 g"),
        // Jodsalz
        new("R114000", "tsp", 6m, "FDC 173468 Salt, table (iodised in the US): 1 tsp = 6 g"),
        // Jodsalz
        new("R114000", "tbsp", 18m, "FDC 173468 Salt, table: 1 tbsp = 18 g"),
        // Jodsalz: as Speisesalz
        new("R114000", "pinch", 0.4m, "FDC 173468 Salt, table: 1 dash = 0.4 g"),
        // Backpulver
        new("R421100", "tsp", 4.6m, "FDC 172803 Leavening agents, baking powder, double-acting: 1 tsp = 4.6 g"),
        // Backpulver: derived
        new("R421100", "tbsp", 13.8m, "FDC 172803: 1 tsp = 4.6 g x 3"),
        // schwarzer Pfeffer: ground
        new("R258100", "tsp", 2.3m, "FDC 170931 Spices, pepper, black: 1 tsp, ground = 2.3 g"),
        // schwarzer Pfeffer: ground
        new("R258100", "tbsp", 6.9m, "FDC 170931 Spices, pepper, black: 1 tbsp, ground = 6.9 g"),
        // schwarzer Pfeffer: dash used for a Prise
        new("R258100", "pinch", 0.1m, "FDC 170931 Spices, pepper, black: 1 dash = 0.1 g"),
        // Tomatenmark
        new("R160000", "tbsp", 16m, "FDC 170459 Tomato products, canned, paste, without salt added: 1 tbsp = 16 g"),
        // Tomatenmark: derived
        new("R160000", "tsp", 5.3m, "FDC 170459: 1 tbsp = 16 g / 3"),
        // mittelscharfer Senf: American yellow mustard as proxy for mittelscharfer Senf
        new("R132000", "tsp", 5m, "FDC 172234 Mustard, prepared, yellow: 1 tsp = 5 g"),
        // mittelscharfer Senf: derived
        new("R132000", "tbsp", 15m, "FDC 172234 Mustard, prepared, yellow: 1 tsp = 5 g x 3 (cup 249 g / 16 = 15.6)"),
        // scharfer Senf: as mittelscharf
        new("R133000", "tsp", 5m, "FDC 172234 Mustard, prepared, yellow: 1 tsp = 5 g"),
        // scharfer Senf: derived
        new("R133000", "tbsp", 15m, "FDC 172234: 1 tsp = 5 g x 3"),
        // Tomatenketchup
        new("R141100", "tbsp", 17m, "FDC 168556 Catsup: 1 tbsp = 17 g"),
        // Tomatenketchup: derived
        new("R141100", "tsp", 5.7m, "FDC 168556 Catsup: 1 tbsp = 17 g / 3"),
        // Mayonnaise 80 %
        new("Q991000", "tbsp", 13.8m, "FDC 171009 Salad dressing, mayonnaise, regular: 1 tbsp = 13.8 g"),
        // Mayonnaise 80 %: derived
        new("Q991000", "tsp", 4.6m, "FDC 171009: 1 tbsp = 13.8 g / 3"),
        // Sojasauce
        new("R143000", "tbsp", 16m, "FDC 174277 Soy sauce made from soy and wheat (shoyu): 1 tbsp = 16 g"),
        // Sojasauce
        new("R143000", "tsp", 5.3m, "FDC 174277 Soy sauce (shoyu): 1 tsp = 5.3 g"),
        // Tahin
        new("Q901000", "tbsp", 15m, "FDC 170189 Seeds, sesame butter, tahini, from roasted and toasted kernels (most common type): 1 tbsp = 15 g"),
        // Tahin: derived
        new("Q901000", "tsp", 5m, "FDC 170189: 1 tbsp = 15 g / 3"),
        // Erdnussbutter
        new("H880200", "tbsp", 16m, "FDC 174294 Peanut butter, smooth: 2 tbsp = 32 g"),
        // Sesamsamen
        new("H420100", "tbsp", 9m, "FDC 170150 Seeds, sesame seeds, whole, dried: 1 tbsp = 9 g"),
        // Sesamsamen: derived
        new("H420100", "tsp", 3m, "FDC 170150: 1 tbsp = 9 g / 3"),
        // Leinsamen: whole seeds
        new("H410100", "tbsp", 10.3m, "FDC 169414 Seeds, flaxseed: 1 tbsp, whole = 10.3 g"),
        // Leinsamen: whole seeds
        new("H410100", "tsp", 3.4m, "FDC 169414 Seeds, flaxseed: 1 tsp, whole = 3.4 g"),
        // Kapern: drained, as the BLS entry is
        new("G012902", "tbsp", 8.6m, "FDC 172238 Capers, canned: 1 tbsp, drained = 8.6 g"),
        // Parmesan: grated Parmesan only
        new("M306400", "tbsp", 5m, "FDC 171247 Cheese, parmesan, grated: 1 tbsp = 5 g"),
        // Schmand 20 %: sour cream (18-20 %) as proxy for Schmand 20 %
        new("M172700", "tbsp", 12m, "FDC 171257 Cream, sour, cultured: 1 tbsp = 12 g"),
        // Joghurt 3,5 %: derived from the cup row
        new("M141300", "tbsp", 15m, "FDC 171284 Yogurt, plain, whole milk: 1 cup (8 fl oz) = 245 g / 16 = 15.3 g"),
    ];
}
