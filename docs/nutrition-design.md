# Culina — nutrition, looked up and cited (design)

| | |
| --- | --- |
| Status | Accepted 27 September 2026, including the four decisions in §O. Nothing here is built. |
| Written | 27 September 2026 |
| Decides | Whether Culina shows nutrition, from what data, and how it stays honest |
| Bead | `culina-v2-bk9l` and its children |

The goal from the bead: **a recipe shows nutrition without anybody ever being
asked to do anything.** The constraint from `README.md`: "No calorie
calculation (a wrong number is worse than none)." Both hold only if what ships
is a **lookup with a citation**, never an estimate, and if every figure says
how much of the recipe it covers.

Claims are marked where it matters:

- **Fact**: read out of this repository or out of a primary source, cited.
- **Measured**: counted, on the date given, by the method given.
- **Reasoning**: an argument from those.
- **Assumption**: not verified; stated so that it can be checked.

---

## A. The data source changed since the bead was written

The bead proposed the Open Food Facts **ingredients taxonomy** (a generic,
multilingual name mapped to a CIQUAL code) plus **CIQUAL** (ANSES, per 100 g).
Its reasoning about the *shape* was right and still stands: generic foods, not
barcodes; a file, not an API; zero outbound calls. The *source* does not
survive a closer look.

**Measured (2026-09-27, `ingredients.full.json` of 2026-09-24, CIQUAL 2025):**

- 3,924 taxonomy entries carry a German name or synonym. Only **688** of them
  carry a CIQUAL code of their own; 2,192 get one only by walking up to a
  parent, which is how *Hühnerbrühe* inherits "Chicken, meat and skin, raw".
- Spot checks of codes that *are* present found wrong foods: *Kartoffel* →
  4003 "Potato, boiled"; *Hähnchenbrust* → 36019 "Chicken high leg"; *Milch* →
  19051, skimmed milk. 45 of the 871 codes no longer exist in CIQUAL 2025, among
  them 20047, *Tomate*.
- *Nudeln*, *Hackfleisch*, *Brühe* and *Gemüsebrühe* have German entries and no
  code anywhere; *Rinderhackfleisch* and *Paprikaschote* have no German entry.
- CIQUAL has French and English names and **no German names** (Fact, ANSES
  documentation), so a household correcting a match would pick from English.
- The taxonomy is ODbL (Fact, OFF terms of use), so the one table Culina would
  derive from it carries **share-alike**. That is the question the bead said
  "determines the schema".

**What replaced it (Fact).** The German federal food composition table, the
**Bundeslebensmittelschlüssel (BLS) 4.0** of the Max Rubner-Institut, has been
free since 17 December 2025 under **CC BY 4.0**: redistribution and
modification allowed, attribution required, no share-alike
(<https://blsdb.de/download>, DOI 10.25826/Data20251217-134202-0).

**Measured (2026-09-27, `BLS_4_0_Daten_2025_DE.xlsx`):**

- 7,140 foods, 138 components, a **German and an English name** for every one
  of them.
- Of the seven values a European food label carries, energy, protein,
  carbohydrate, sugars and salt are missing for **no** food. Fat is missing for
  3 and saturated fat for 23.
- Foods are named in the state they are bought and weighed in:
  `G480100 Speisezwiebel roh`, `E111100 Hühnerei roh`,
  `V416100 Hähnchen Brustfilet, roh`, `E401000 Teigwaren eifrei, roh`,
  `M111300 Vollmilch frisch, 3,5 % Fett, pasteurisiert`.
- Every value carries its provenance (`Datenherkunft`, `Referenz`), and a
  missing value is `-`, "nicht gleichbedeutend mit Null" (not the same as
  zero).

**Recommendation: BLS 4.0, and nothing from Open Food Facts.** It removes the
ODbL question instead of answering it, it speaks German, and the one gap it
leaves (how a recipe word finds a BLS food, §D) is a gap the OFF taxonomy did
not close either. Accepted, and the bead was renamed to match (§O).

Also considered, briefly:

| Source | Why not the primary |
| --- | --- |
| USDA FoodData Central | CC0 and excellent portion data, but English only and American foods. Used for one narrow job in §C. |
| Swiss Food Composition Database 7.1 | German names with synonyms, but 1,216 generic foods, Swiss vocabulary (*Rahm*), and a "free with a source credit" term that is not a named licence and does not clearly permit redistributing the file. |
| CIQUAL 2025 alone | Open Licence 2.0 / CC BY, good data, no German names. |

---

## B. The data: what ships, and how

**Kept from BLS 4.0:** code, German name, English name, and per 100 g edible
portion: energy (kJ, kcal), fat, saturated fat, carbohydrate, sugars, protein,
salt. The seven values of the EU label (Regulation 1169/2011), because that is
the table every reader already knows from a package. Nothing else: not the 130
other components, not the provenance columns.

**One committed file.** An extract (around 7,000 rows, under 1 MB as text) sits
in `Infrastructure` beside the migrations and is embedded the same way
(`<EmbeddedResource>`), loaded once at startup into an in-memory table. `-`
becomes *unknown*, never zero. The raw 14 MB workbook is not committed; the
extraction is a documented, repeatable step that runs when the MRI publishes a
new version, which is measured in years.

**Why not the other two shipping options the bead listed** (Reasoning):

- *A volume:* a fourth mandatory volume for read-only reference data is an
  operational cost for no benefit. The data is part of the release, not the
  installation.
- *Downloaded on first run:* the first unsolicited outbound request Culina
  would ever make (Fact: every current outbound call is user- or
  admin-initiated: URL import, Tandoor sources, the assistant providers).
- *The frontend budget* the bead worried about is not touched: the table lives
  in the backend image. The image grows by roughly the size of the file.

**A version.** The extract, the name table (§D) and the conversion rules (§C)
together have one version constant, the way `CulinaryLexicon.Version` does. It
feeds the ETag (§H), so a deploy with new data is never answered with a `304`
from before it.

---

## C. From a line to grams — the hard part

Composition is per 100 g. A recipe line is `Quantity(decimal? Amount, Unit?)`
(`Domain/Recipes/Quantity.cs`), and `Units.cs` is explicit that only mass
converts freely.

**Measured (dev database, 2026-09-27, 1,208 lines, 125 recipes with at least
three lines):**

| Line shape | Lines | |
| --- | --- | --- |
| mass (`g`, `kg`) | 560 | 46 % |
| a bare count (`2 Eier`, no unit) | 164 | 14 % |
| spoons (`tsp`, `tbsp`) | 156 | 13 % |
| built-in counts and a household's own units | 145 | 12 % |
| no amount at all (`Salz`) | 97 | 8 % |
| volume (`ml`, `l`) | 86 | 7 % |

The median recipe has **33 %** of its lines in mass, **50 %** in mass or
volume, 67 % with spoons, 85 % with bare counts. Only **18 of 125** recipes have
every line in mass or volume.

So a design that counts only grams produces an incomplete figure for nearly
every recipe, and a design that counts everything has to invent weights for
onions. Neither is acceptable, and the answer is a rule per shape:

| Shape | Counted? | How |
| --- | --- | --- |
| Mass | Always | Exact: `kg` × 1000. |
| Volume (`ml`, `l`) | Only for foods that pour | × the food's density. |
| Spoons | Only for foods that pour | 15 ml / 5 ml, × the food's density. |
| Spoons of anything that does not pour (flour, sugar, spices, butter) | **Never** | — |
| Eggs, by count | Yes | EU size class M. |
| Every other count (`1 Zwiebel`, `2 cloves`, `1 can`, `ein Schuss`) | **Never** | — |
| No amount | **Never** | — |

**Why pouring foods, and only those** (Reasoning). The reason Culina refuses
to turn volume into mass is written down twice already, and it is *packing*,
not arithmetic. `scaling-rules.md`: "1 cup of flour is between 120 g and 150 g
depending on how it was packed". A heaped tablespoon of flour is twice a level
one. Oil, milk, cream, water, stock, wine, vinegar, lemon juice, honey and
syrups do not pack. Their density is a physical constant, not a judgement.

**Why spoons may convert here and nowhere else** (Reasoning, and a deliberate
exception to `Units.cs`). The spoon rule protects two things: an amount *shown
to a cook*, and a *shopping-list merge*. A US tablespoon (14.8 ml) against a
metric one (15 ml) is 1.3 %, and turning `2 tbsp` into `30 ml` on a screen
claims a measurement nobody made. Nutrition shows no converted amount. It adds
the grams into a figure whose own uncertainty is an order of magnitude larger:
a food varies from one reference value by far more than 1.3 %. The breakdown
(§E) says `2 EL Olivenöl ≈ 27 g`, so the conversion is shown, not hidden.
`Units.cs` keeps its rule, and the nutrition code gets its own conversion
beside it, named so that nobody mistakes one for the other.

**Where densities come from.** Only foods that pour have one, a few dozen
values, attached to their entries in the name table (§D). They are taken from
USDA FoodData Central portion rows, which are CC0 (Fact, FDC API guide:
"published under CC0 1.0 Universal"). **Not** from the FAO/INFOODS density
database, which is "All rights reserved" (Fact). **Not** from the eight
`density_g_per_ml` values in the OFF taxonomy, which are ODbL. Assumption: FDC
has a usable household-measure row for each of the few dozen pouring foods.
Phase 1 checks every one.

**Eggs** (decided, §O). An egg is the one countable ingredient
sold in **legally defined sizes**: Regulation (EC) No 589/2008, M = 53–63 g in
the shell. `Ei` and its forms are 45 of the 164 bare-count lines above, and
they are energy-dense. Counting an egg as size M is a range of ±9 %, stated in
the breakdown (`2 Eier ≈ 2 × Größe M`). An onion is 50–250 g, and has no such
standard. That is the line. Assumption: BLS values are per 100 g of edible
portion, so the shell fraction must come off. Phase 1 confirms this from the
BLS documentation and records the figure it uses.

**What is deliberately not done:**

- No weight for a "medium onion", however well cited. That is precisely the
  number the README refuses.
- No reading grams out of the note ("Lachsfilet, á 180 g"). Measured: 2 of
  1,208 lines do it.
- No prompt asking the author to write grams. The breakdown says why a line was
  not counted ("Menge nicht in Gramm"). What the author does about it is theirs.

---

## D. From a name to a food — a fourth table

Culina already has three tables of food words, and `CulinaryLexicon` states why
they are separate: "Three questions, three tables, because merging them would
make each answer worse at its own job" (Fact,
`Domain/Search/CulinaryLexicon.cs`). Nutrition asks a fourth question: **which
food, exactly, is this?** None of the three answers it.

| Table | Question | Why it cannot answer this one |
| --- | --- | --- |
| `CommonIngredients` | What might they be typing? | ~120 names for offering, no food behind them. |
| `SectionKeywords` | Which aisle? | Stems matched *inside* a phrase. A wrong aisle costs a walk; a wrong food is the README's objection. |
| `CulinaryLexicon` | What else means this? | Concepts are coarse on purpose: *chicken*, not *chicken breast, raw*. Its mistakes are contained by landing in the bottom search tier; a nutrition mistake is not contained anywhere. |

**BLS names cannot be matched directly** (Measured). *Butter* is a substring
of 116 BLS names, *Zucker* of 132, *Milch* of 261. The one that is meant (e.g.
`Q611000 Butter mild gesäuert`) is not recoverable by any text rule. Offering
116 choices to a *person* is fine, and §G does that. Choosing one automatically
is `SectionKeywords`' failure with higher stakes.

**So: a curated, bilingual table of names pointing at BLS codes.** It is
`Domain` code like the other three. Each entry has:

- a BLS code;
- its German and English forms;
- a density, if and only if the food pours;
- an egg size, if and only if it is eggs.

**Size (Measured, dev database).** The 150 most frequent names cover **78 %**
of lines, 300 cover **91 %**. Those are lower-cased, unfolded names, so the
true curve is better, because folding and plurals merge rows. That is the size
`CulinaryLexicon` argues for ("a table of three hundred entries can be read and
argued with; three thousand would be a liability"), and the same argument holds.

**Matching is strict**, the way the lexicon reads a *query* and not the way it
reads a recipe (Fact: "a form must be the whole word, give or take a short
ending"). The rules:

1. The name is folded with `ItemName.Fold`, the same comparison form the
   shopping list uses. That keeps `Müsli` and `Muesli` together, and keeps the
   correction key (§G) the one the section overrides already use.
2. A form must account for the **whole** name, give or take a short ending.
3. **No compound splitting.** The lexicon's rule for entries is the rule here,
   and matters more: *Kokosmilch* is not milk. A compound earns an entry
   (`Hähnchenbrustfilet`, `Rinderhack`, `Kochsahne`) or is not counted.
4. **Both languages, always.** Measured: many recipes marked `en` in the dev
   database have German ingredient names (*zucker*, *mehl*, *kidneybohnen*).
   So `recipes.language` cannot choose the table.
5. The `Note` is never read. Culina keeps the preparation out of the name, so
   the matcher already gets the shoppable noun (Fact, `domain-model.md`).

**Defaults are visible, not silent.** An unqualified *Milch* is a choice
between 3.5 % and 1.5 % fat, 62 against 44 kcal per 100 g (BLS `M111300`,
`M111200`). The table picks what the
unqualified word usually means in a German shop (*Vollmilch*). The breakdown
names the food every line was counted as, and one tap corrects it (§G). "Never
bother the user" means never *asking*; it does not mean *hiding*. This is the
section guess exactly: right most of the time, visible, one tap, remembered.

**Every entry is the raw, as-bought state** (`roh`, dry pasta, uncooked rice).
A recipe weighs what goes in, and a cooked entry would count 500 g of dry
spaghetti as 500 g of boiled spaghetti: 346 against 146 kcal per 100 g
(`E401000`, `E401032`), a factor of 2.4.

---

## E. Honesty: a lower bound that says what it covers

**Every value in the table is non-negative, so a sum over the lines that could
be counted is a lower bound on the whole** (Reasoning). That turns partial
coverage from a caveat into a true statement:

> **mind. 520 kcal** pro Portion · 7 von 9 Zutaten
> nicht gezählt: 1 Zwiebel (Menge nicht in Gramm), Salz (keine Menge)

- **Complete** (every line counted): the figure, with no qualifier.
- **Partial**: "at least" / "mindestens", the count of lines, and the lines
  that were left out, each with its reason.
- **Nothing counted**: no figure, and one plain sentence saying why.
  Unknown is a legitimate answer and looks like one.

Per value, not only per recipe: if a counted food has no saturated-fat value
(23 BLS foods), saturated fat becomes "at least" while energy may still be
complete.

**Lower bounds round down**, exact values round the way a package label does
(EU guidance on tolerances and rounding, 2012). A lower bound rounded up is no
longer a lower bound.

**Per portion, or per piece.** `Yield.Kind = servings` gives "pro Portion";
`pieces` gives "pro Stück", which is the more honest of the two, because a
muffin is a thing and a serving is a number somebody typed.

**Per portion is invariant under scaling** (Reasoning). Doubling the portions
doubles every amount and the yield together, so the figure per portion does
not move. The client applies no factor, and nothing can scale twice. That
settles the bead's question 6 without code.

**Not per 100 g of the dish** (a reversal of the bead's first sketch). The
weight going *in* is known; the weight coming *out* is not. Pasta takes up
water and a roast gives it off, and "per 100 g" of the raw sum is a number
that is wrong for anything cooked. Per 100 g stays where it is a fact: on each
food, in the breakdown.

**The bound is about coverage, not identity.** "At least" is exact about the
lines left out. It cannot protect against a line counted as the wrong food, so
that is what §D's visible defaults and §G's correction are for, and what
Phase 1's precision gate (§N) measures.

---

## F. Where it is computed: on read, in memory

The bead expected the opposite pressure from the smart cookbooks: "resolution
is expensive and the inputs rarely change — so it probably IS materialised".
With the data in memory, resolution is a dictionary lookup per line (Reasoning:
a recipe has on the order of ten lines, and nothing is scanned). So the smart
cookbook's rule applies after all: **store the rules, not the results.**

- `GET` reads the recipe (already loaded for the page), this household's
  corrections for those names (one indexed query), and computes: a pure
  function in `Domain`, from lines, yield, table and corrections to a result.
- **Nothing is stored per recipe, and nothing per ingredient line.** Measured
  reason beyond the principle: every recipe save deletes and re-inserts its
  groups and ingredients (`RecipeRepository.cs:339-374`), so a row hanging off
  `recipe_ingredients` would be wiped on each save anyway.
- **No background job.** Nothing is ever pending, stale or waiting to be
  recomputed, and nothing user-facing can block on it, because there is
  nothing to wait for. The bead's title says "computed in the background"; the
  promise behind it ("never asked about, never waited on") is kept better by
  not having one.

When this stops being true: the day a ranking term (§L) or a list filter needs
nutrition *in SQL* for hundreds of recipes at once. Then the question of a
reference table in Postgres and a materialised summary comes back, and it
should be decided with a measurement, as `recipe-search-performance` was.

---

## G. Correction: the section override, a second time

`shopping_section_overrides(household_id, name_key, section)` is the pattern
(Fact, `0006_shopping.sql`). It holds a seeded guess, one tap to fix it, and
the fix is correct for that household from then on, with no configuration
screen. (`domain-model.md` calls it `HouseholdIngredientSection`, a name that
exists nowhere in the code.)

```sql
create table nutrition_food_overrides (
    household_id uuid        not null references households (id) on delete cascade,
    name_key     text        not null,
    food_code    text,         -- null: "do not count this"
    updated_at   timestamptz not null,
    primary key (household_id, name_key)
);
```

- `name_key` is `ItemName.Fold`, the same key the section override uses.
- `food_code` null means "this is not something to count", for the line no BLS
  food fits. Removing the row returns the line to the table's default.
- **The household it is read in**, not the one the recipe belongs to. A
  correction is something a household *did*, and `domain-model.md` keeps
  everything a household did with the household it happened in. An inherited
  recipe therefore reads with the heir's corrections.
- Any member may correct, as with sections.
- No version column: it is a fact at a known address, written idempotently,
  like `cookbook_recipes` and `suggestion_dismissals`.

**The picker searches BLS itself**, in German and English names. That is the
job of offering, where 116 butters ranked by name are fine because a person
chooses.

**A warning from the precedent** (Fact): the section override's backend exists,
and nothing in the frontend calls it. `moveToSection` in
`features/shopping/stores/shopping.svelte.ts` has no caller, so the promised
one tap was never reachable. Nutrition's correction must ship with its UI, in
the same phase, and the section gap is filed separately.

---

## H. API

| Method | Path | Notes |
| --- | --- | --- |
| `GET` | `/recipes/{recipeId}/nutrition?householdId=…` | The result below. `householdId` is whose corrections apply, defaulting to the recipe's own; access as `RecipeAccess.VisibleInAsync`. ETag, `304`. |
| `GET` | `/foods?q=…` | BLS search for the picker, German and English names, best first. Not household data, not paged beyond a limit. |
| `PUT` | `/households/{householdId}/ingredients/{name}` | `{ "food": "Q611000" }` or `{ "food": null }`: what this household means by that name. `204`. |
| `DELETE` | `/households/{householdId}/ingredients/{name}` | Back to the default. `204`. |

`/households/{id}/ingredients` already exists for suggestions. A `PUT` to one
name in it states what the household means by that name. The exact shape is
checked against `rest-api-design` in the phase that builds it.

**A separate resource, not a field on `RecipeDetail`** (Fact and Reasoning).
`GET /recipes/{id}` answers with `ETag "v{version}"`, derived from the recipe's
version alone (`Api/Infrastructure/ETag.cs`). A correction or a data update
changes nutrition without touching that version, so a field there would be
served stale behind `304`s. The nutrition ETag is built from the recipe's
version, the data version (§B), and a fingerprint of the corrections that
apply to this recipe's names. `ETag.Of(version, identity, variant)` exists for
this.

The shape, roughly:

```jsonc
{
  "per": "serving",              // or "piece"
  "complete": false,
  "counted": 7, "lines": 9,
  "values": {                    // per portion
    "energyKcal": { "value": 520, "atLeast": true },
    "energyKj":   { "value": 2180, "atLeast": true },
    "fat":        { "value": 31.2, "atLeast": true },
    "...": {}
  },
  "ingredients": [
    { "ingredientId": "…", "status": "counted",
      "food": { "code": "Q120000", "name": "Olivenöl" },
      "grams": 27, "via": "density" },
    { "ingredientId": "…", "status": "amountNotInGrams" },
    { "ingredientId": "…", "status": "noAmount" },
    { "ingredientId": "…", "status": "unknownFood" },
    { "ingredientId": "…", "status": "excluded" }     // a household said so
  ],
  "source": { "name": "Bundeslebensmittelschlüssel", "version": "4.0" }
}
```

Unrounded numbers, like every other quantity the server sends. Rounding is
presentation (`scaling-rules.md`), so it lives in the client, lower bounds
downward.

---

## I. Frontend

- **A disclosure on the recipe page, after the recipe surface** and before the
  personal notes. That is not inside `RecipeSurface`, whose layout invariant
  keeps the ingredients and servings in place while cooking. Nobody needs
  nutrition mid-step.
- **The headline is in the summary line**, so it needs no tap:
  "mind. 520 kcal pro Portion · 7 von 9 Zutaten". Opening it shows the seven
  label values and the breakdown, one row per ingredient: what it was counted
  as, the grams (with `≈` and the reason when converted), or why it was not
  counted.
- **Attribution in the panel itself**, one line: "Nährwerte: Max Rubner-Institut,
  Bundeslebensmittelschlüssel 4.0 (CC BY 4.0)". CC BY requires it wherever the
  data is shown.
- A skeleton that mirrors the panel, not a spinner
  (`sveltekit-loading-and-skeletons`). Offline, the panel either comes from the
  same private cache as the recipe or says plainly that it is not available
  offline; the phase that builds it decides and tests it.
- The design-system gallery fixture already says "Nutrition / Roughly 320 kcal
  a serving" (`__fixtures__/Overlays.svelte`). *Roughly* is exactly the word
  this design refuses, so the shipped copy must not borrow it.
- New message keys are appended to the end of both catalogues, never
  re-serialised (bead memory `paraglide-message-files-must-keep-their-order`).

---

## J. Licences, attribution and the archive

| Data | Licence | Obligation |
| --- | --- | --- |
| BLS 4.0 values and names | CC BY 4.0 | Attribution: "Max Rubner-Institut (2025): Bundeslebensmittelschlüssel (BLS), Version 4.0 — Deutsche Nährstoffdatenbank. Karlsruhe." In the panel, and in the repository beside the file. |
| USDA FDC portion data (densities) | CC0 | None; cited anyway. |
| The name table (§D) | Culina's own, PolyForm Noncommercial 1.0.0 with the code | — |

**The bead's ODbL question no longer arises.** Nothing is share-alike, so a
household's database with nutrition in it is not a derivative database of
anything. That is doubly true, because nothing nutritional is written into a
household's database at all, only which food a household means by a name.

**The archive is unchanged.** It carries recipes. Nutrition is derived from
them on read, so it travels by being recomputed. Corrections are household
preferences, and the section overrides, the nearest precedent, are not in the
archive either. Revisit only if someone misses them after a restore.

---

## K. What this reverses, on the record

The phase that closes the epic edits:

- `README.md`, "What it deliberately does not do": the calorie sentence becomes
  one about *estimates*, which Culina still refuses, next to the lookup it now
  does.
- `culina-v2-erv`: a note that "nutrition estimates" stays rejected, and a
  cited lookup with stated coverage is what was built instead.
- `Domain/Search/DietRules.cs`: "Culina has no nutrition table and will not
  have one". The reasoning about diets does not change (BLS does not say what is
  vegetarian), but the sentence stops being true.
- `docs/product-quality-plan.md` ("nutrition estimates" after the core release),
  and `docs/domain-model.md`, `docs/api.md`.

---

## L. The recommender: prepared, not built

Nothing in `culina-v2-9y8y` changes. The seams the bead listed are real:
`ScoreTerm` is a list (`Domain/Suggestions/SuggestionExplanation.cs`),
`SuggestionContext` a record, `RankingWeights` a record. One consequence of §F
for whoever builds a nutrition term: ranking runs in SQL, and this design keeps
nutrition in memory. A term would need the reference data in Postgres (§F's
"when this stops being true"), and it must read *coverage*, not just the value.
Ranking on a lower bound as if it were the figure would be worse than not
ranking on it.

---

## M. Rejected

| | Why |
| --- | --- |
| OFF taxonomy + CIQUAL | §A: thin German coverage, wrong and stale codes, ODbL share-alike, no German food names for correcting. |
| The OFF product (barcode) database | The bead's own reason: a recipe says "200 g Butter", not an EAN. |
| Any runtime API | Rate limits, and the first unsolicited outbound call. |
| Standard weights for onions, cloves, bunches, cans | The number the README refuses. |
| Spoons of powders and solids | Packing: the reason `scaling-rules.md` gives for never turning mass into cups. |
| Per 100 g of the dish | The cooked weight is unknown (§E). |
| Matching against BLS names directly | 116 butters (§D). |
| Compound splitting | *Kokosmilch* is not milk. |
| A background resolver and materialised values | Nothing expensive left to materialise (§F). |
| A confirmation step ("is this butter?") | The bead's premise: never ask. Visible defaults and one tap instead. |
| Micronutrients, allergens, diet labels | Not what anybody asked, and each is its own honesty problem. |
| Nutrition in list cards, search filters, sort | §F and §L: a different computation, and not before a measurement asks for it. |

---

## N. Phases, and the gate that can stop it

Each phase is a child bead of `culina-v2-bk9l`.

1. **The data, the names, and the gate.** The BLS extract and its loader, the
   name table (§D), the conversion rules (§C), and a measured evaluation. The
   evaluation runs over a committed fixture of real lines (quantity, unit and
   name only), each with a hand-judged answer, and reports:
   - **precision**: of the lines the table recognises, how many it counted as
     the right food. **The hard bar is 98 %.**
   - recognition: lines whose food is known;
   - counted: lines both known and convertible, with and without the egg and
     spoon rules;
   - recipes counted completely, and the median share of each recipe's lines
     counted.

   The precision bar is not negotiable. A wrong food is the README's objection.
   The coverage numbers go to Jakob, because they decide whether "at least"
   carries a headline or only a breakdown. *If the median recipe counts under
   half its lines, reconsider the summary line before building the UI.*
2. **The endpoint.** The calculator in `Domain`, `GET
   /recipes/{id}/nutrition`, the ETag, integration tests. Blocked by 1.
3. **The recipe page.** The disclosure, the breakdown, attribution, offline,
   messages, accessibility, the weight budget. Blocked by 2.
4. **Correction.** The table, `PUT`/`DELETE`, `GET /foods`, and the picker
   from a breakdown row, backend and UI together (§G's warning). Blocked by 2
   and 3.
5. **The record.** §K. Blocked by 3 and 4.

---

## O. Decisions

Each of these reverses something already written, so each was put to Jakob
rather than left to drift. **All four were accepted on 27 September 2026.**

1. **BLS 4.0 instead of Open Food Facts + CIQUAL** (§A), and the bead renamed.
2. **Spoons and volumes count for foods that pour**, at 15 ml / 5 ml. This is
   an exception to `Units.cs`' never-convert-spoons rule, confined to
   nutrition, and shown in the breakdown (§C).
3. **Eggs count, as EU size class M**, the one count with a legal size
   standard (§C). Without it, nearly every baking recipe is "at least".
4. **No per 100 g of the dish** (§E), which reverses the bead's first sketch.

---

## P. Sources

- BLS 4.0: <https://blsdb.de/download>, CC BY 4.0, DOI
  10.25826/Data20251217-134202-0; documentation §9 (Zitierweise,
  Nutzungsbedingungen).
- CIQUAL 2025: <https://ciqual.anses.fr/cms/en/download>,
  DOI 10.5281/zenodo.17550133.
- Open Food Facts ingredients taxonomy:
  `openfoodfacts-server/taxonomies/food/ingredients.txt`,
  <https://static.openfoodfacts.org/data/taxonomies/ingredients.full.json>;
  terms of use (ODbL): <https://world.openfoodfacts.org/terms-of-use>.
- USDA FoodData Central licence (CC0): <https://fdc.nal.usda.gov/api-guide>.
- FAO/INFOODS Density Database v2.0 (all rights reserved):
  <https://www.fao.org/4/ap815e/ap815e.pdf>.
- Swiss Food Composition Database: <https://naehrwertdaten.ch/de/downloads/>.
- Regulation (EU) No 1169/2011 (food information to consumers); Regulation
  (EC) No 589/2008 (egg size classes).
- In this repository: `README.md`, `docs/domain-model.md`,
  `docs/scaling-rules.md`, `docs/suggestions-research.md`,
  `Domain/Recipes/Quantity.cs`, `Domain/Recipes/Units.cs`,
  `Domain/Search/CulinaryLexicon.cs`, `0006_shopping.sql`,
  `Api/Infrastructure/ETag.cs`, `Infrastructure/Persistence/Recipes/RecipeRepository.cs`.
