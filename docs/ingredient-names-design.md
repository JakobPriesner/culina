# Culina — what an ingredient name means (research and proposal)

| | |
| --- | --- |
| Status | Built 12 October 2026 (epic `culina-v2-p8tf`), except household foods (§D.4) and the editor hints (§D.3); see §G. |
| Written | 12 October 2026 |
| Decides | How Culina reads variants ("grüner Spargel", "Kokosmilch light"), singular and plural, and what belongs in the note |

Three problems were reported:

1. "Kokosmilch light" cannot be found.
2. Properties such as *grüner* or *weißer* Spargel are not recognised.
3. Singular and plural have no rule ("1 Zwiebel" / "2 Zwiebeln"). Should a comment field carry them?

All three come from one question Culina has never answered: **which parts of an
ingredient name are the thing you buy, and which are only words about it?**
Today every consumer of a name (editor suggestions, shopping list, nutrition,
search, merge suggestions) answers it differently, and some answer it wrongly.

Claims are marked where it matters:

- **Fact**: read out of this repository or a primary source, and cited.
- **Reasoning**: an argument built on those facts.
- **Assumption**: not verified, and stated so it can be checked.

---

## A. What Culina does today

### A.1 The model

**Fact.** A line is `Quantity` (amount and unit) + `Name` (at most 120 characters, "the shoppable noun") + `Note` (at most 200 characters, the preparation).
- Source: `Domain/Recipes/RecipeIngredient.cs:10-13`.
- The note exists so that "Butter, gewürfelt" and "Butter" merge on a shopping list (remark at line 6, and the `note` column comment in `0003_recipes.sql`).
- There is **no ingredient table**: an ingredient is whatever somebody types (`docs/domain-model.md:208`, `docs/search-design.md:185`).
- Every household choice is keyed by the folded written name `ItemName.Fold(name)`: case, umlauts as *ae* and diacritics are folded, and nothing else. This covers shop section, nutrition food, unit weight and merge rule.

So the comment field the question asks about already exists. It is the note.
What is missing is a rule for what goes in the name and what goes in the note,
plus the reading that makes the name useful afterwards.

### A.2 Why "Kokosmilch light" is not found

**Fact.**
- **Editor suggestions** come from `RecipeRepository.OwnIngredientNamesAsync` (`Infrastructure/Persistence/Recipes/RecipeRepository.cs:53-78`), plus the seeded `CommonIngredients`.
  - The household query is `i.name ilike '%' || @query || '%'` on the **raw** name, so the whole typed text must be a substring of a stored name.
  - As soon as " light" is typed, "Kokosmilch" drops out. No seed contains the phrase, so the list is empty and `ComboField` does not open.
  - The same raw match means "kokos light", "Kokosmilch (light)" and "Olivenoel" → "Olivenöl" also find nothing.
  - **Assumption:** with the database in locale C, `ilike` probably does not case-fold non-ASCII letters either. Not verified.
- **Nutrition.** `FoodNames.Match` (`Domain/Nutrition/FoodNames.cs:51-107`) matches only the whole name, with up to two trailing letters on the last word.
  - "Kokosmilch light" matches nothing, so the line is not counted.
  - BLS 4.0 has **no** light coconut milk at all: `bls.tsv:1470`, `H154000` is the only row. Searching for a food in the correction sheet cannot find it either.
- **Written with a comma**, "400 ml Kokosmilch, light", the editor's line parser (`parseIngredientLine.ts:35`) moves "light" into the note.
  - The line then counts as ordinary 227 kcal coconut milk, and it merges with "Kokosmilch" on the shopping list.
  - The variant is gone from every place that matters, and nothing says so.
- **Search.** `CulinaryLexicon.Describe` reads ingredient names generously. "Kokosmilch light" therefore gives the recipe `coconut_milk` **and** the *Character* concept `light` ("leichte Küche", `CulinaryLexicon.cs:778`). A recipe with a can of light coconut milk becomes "light cooking".

### A.3 Why grüner/weißer Spargel goes wrong

**Fact.**
- **Nutrition.** `FoodNames` lists "Spargel", "weißer Spargel" and "Grüner Spargel" as forms of one food, `G450100 Spargel roh` (`FoodNames.cs:585-587`).
  - For nutrition that is right: BLS has one raw asparagus, and green and white are within a few kcal.
- **Merge suggestions.** `IngredientLookalikes.Reason` (`Domain/Ingredients/IngredientLookalikes.cs:121-135`) uses the same food code as proof of the same ingredient (`SameFood`).
  - So the merge page **proposes rewriting every "grüner Spargel" into "Spargel"**. One click destroys the information the cook needed in the shop.
  - The same happens for Kirschtomaten → Tomaten and Vollmilch → Milch: anything `FoodNames` folds together for nutrition.
- **Inflection.** "grünen Spargel" fails `FoodNames`, because words before the last must match exactly.
- **Word order.**
  - "Spargel, grün" (the BLS, USDA and Chefkoch order) is split by the line parser into name "Spargel" and note "grün". Same loss as A.2.
  - "Spargel grün" as one name is a different `name_key` from "grüner Spargel", although it is the same thing.
- **Search filter.** The ingredient filter and smart-shelf rules use `ri.name ilike '%' || rule || '%'` (`RecipeSearcher.cs:43-51`).
  - "Spargel" finds "grüner Spargel", which is right.
  - "grüner Spargel" does not find "Spargel, grün" or "Grünspargel".
- **Shopping list.** "grüner Spargel" and "weißer Spargel" are two lines, which is right. They sort independently inside the vegetable section, so nothing shows they are two kinds of one thing.

### A.4 Singular and plural

**Fact.**
- There is no rule. Plurals are handled piecemeal:
  - `FoodNames` enumerates them as forms.
  - The lexicon allows two extra letters on a query word.
  - Postgres Snowball folds them in the full-text lane only.
  - `ItemName.Fold` does nothing, so "Tomate" and "Tomaten" are **two shopping lines** and two keys for every household choice.
- `IngredientLookalikes` calls "Tomate"/"Tomaten" a `Typo` (its doc comment, line 18) and proposes merging one into the other.
  - Accepting that rewrites every recipe to one form. So "1 Tomate" becomes "1 Tomaten", or "4 Tomaten" becomes "4 Tomate".
  - **The merge feature shipped on 12 October turns a matching problem into a grammar problem.**
- Scaling never touches the name. "1 Zwiebel" at double servings reads "2 Zwiebel".
- The line parser splits on a comma **inside parentheses**: "Tomaten (gehackt, aus der Dose)" becomes name "Tomaten (gehackt" and note "aus der Dose)".
- `CommonIngredients` puts Kokosmilch in *Konserven* (`CommonIngredients.cs:170`). `SectionKeywords` puts "kokosmilch" in *Milchprodukte* (`SectionKeywords.cs:55`). Unrelated to the rest of this document, but found on the way.

---

## B. How other apps do it

Details, field names and URLs are in §F. In short:

| App | Identity | Variants (grün, light) | Preparation | Plural |
| --- | --- | --- | --- | --- |
| Tandoor | `Food` table, a tree | baked into the food name or a child food | `note` | `plural_name`; singular only when amount == 1 |
| Mealie | `Food` table, flat, with `aliases` | baked into the food name | `note` (the parser joins preparation, comment and size into it) | `plural_name`; per-locale rule, de = always plural except amount ≤ 1 |
| Grocy | product with one-level `parent_product_id` | child product | `note` | `name_plural` + gettext forms |
| Bring! | a catalogue of 444 German items | free `specification` ("fettarm") | in the specification | stores the **plural** as the only name ("Zwiebeln") |
| Chefkoch | `foodId` + name | name suffix ("Schokolade, zartbitter") | `usageInfo` (", geschält") | writes "Zwiebel(n)" |
| Cooklang | free text | in the name | `(preparation)` | locale-driven |
| Open Food Facts | DAG taxonomy | child entries: `en:coconut-milk` → `en:coconut-milk-light`, `en:asparagus` → green and white | — | plurals and inflections are just synonyms ("grüner Spargel, Grüne Spargeln") |
| Spoonacular | parsed | strips adjectives into `meta`: "green tea" → name "tea". Cautionary. | `meta` | — |

Patterns that hold across them (**Reasoning** from §F):

1. **Three slots and one note are universal.** Nobody who stores a separate "preparation" and "comment" shows them differently. Mealie's parser distinguishes four kinds and then concatenates them into one note.
2. **A word that changes what you buy belongs to the identity.** Every food database keeps "light coconut milk" and "green asparagus" as their own entries, or as children. The one app that strips adjectives (Spoonacular) turns green tea into tea.
3. **Hierarchy is used for search and grouping, not for adding up.**
   - Tandoor searches child foods under a parent.
   - Plan to Eat groups all cheeses under a "cheese" heading.
   - Nobody sums 500 g green and 1 kg white asparagus into 1.5 kg asparagus.
4. **Plural is a stored second form, not generated.** It has to feed the *matcher* as well as the display. Tandoor's #3964 ("5 bananas" created a new food "bananas") is exactly Culina's Tomate/Tomaten.
5. **German apps dodge plural.** Chefkoch writes "Zwiebel(n)", and Bring! stores only "Zwiebeln".

## C. What users say

From GitHub issues on Tandoor and Mealie, Hacker News, Lemmy and vendor help pages. Reddit could not be searched. Sources are in §F.

- **Strict food tables rot.**
  - Tandoor #2934 is the clearest. Imports fill the food table with junk ("1- is a unit", "to 1¼-pound pork tenderloins" as a food) until it is "essentially unusable". Merge rules do not keep up, because the next import brings new junk.
  - The user asks for lines to stay plain text until a match is confirmed, and for unknown adjectives such as "jumbo" to go to the note.
  - Repeated in #672 (egg vs egg(s)), #588, #4219 (another household member adds "weird" names) and #2283 (users write SQL to purge unused foods).
  - The Mealie FAQ warns against bulk parsing "without a very complete set of Foods". Mealie's fix (PR #6151) was to keep the original line, match when confident, and ask about uncertain lines one at a time.
  - **This is the strongest evidence that Culina's no-table decision is right.**
- **Users expect the base item to merge, and differences to stay visible.**
  - AnyList users complain that "2 onions" and "1/2 cup yellow onion, chopped", or "2 cloves" and "2 large cloves", stay separate.
  - Plan to Eat's documented answer is: prep words go into the note, the note is ignored for merging, and it is shown after the combined item.
  - Against that, Tandoor #3800: "the notes are more important than having the ingredients combined", if same-food lines at least sit next to each other.
  - Mealie #70: "1 cup sifted flour" and "1 cup flour, sifted" are different amounts.
- **Plural.**
  - A German Tandoor user (#2737) wants "Äpfel" whenever the amount is not exactly 1 without a unit, so "1 kg Äpfel", not "1 kg Apfel". Others dispute the fraction case.
  - Mealie's German maintainer: German pluralises foods after units too ("500 g Zwiebeln"). English does not ("500 g onion").
  - The maintainer in #2737: "plurals will always run into issues". Rules are per language, and a fixed rule frustrates someone.
- **Notes are wanted inline.**
  - Tandoor #455, #3312, #3800, #4002 and discussion #3129 (a user's partner rejected the app over it): show "Butter, *weich*" in the line, not behind an icon.
  - Notes on the shopping list are wanted only sometimes, for substitutes, "optional" and buying hints, behind a toggle.
- **Search.** Tandoor #1320: choosing "cheese" found nothing although Gouda was a child, and the maintainers changed it. A broader term is expected to find narrower ones. No user voice was found for "Kokosmilch" vs "Kokosmilch light" specifically.

---

## D. Proposal

### D.1 The rule: the name says what you buy, the note says what you do with it

Keep free text as the only truth. **Do not add a food table.** §C is unambiguous about what that costs a household, and nothing below needs one.

Instead, Culina **reads** a name the way it already reads recipes for search: with a small curated vocabulary, derived and recomputable, never rewriting what the cook typed. A name reads as:

```
  [descriptors] [qualifiers] HEAD [qualifiers]        e.g. "2 große rote Zwiebeln"
                                                           "Spargel, grün"
                                                           "Kokosmilch light"
```

| Part | Examples | Belongs in | Identity? | Shopping list | Nutrition | Search |
| --- | --- | --- | --- | --- | --- | --- |
| **Head** | Spargel, Kokosmilch, Zwiebel/Zwiebeln | name | yes, as the lemma (singular and plural are one) | merges | looked up | the broad term |
| **Qualifier**: changes what you buy | grün, weiß, rot · light, fettarm, fettreduziert, Vollkorn · TK, getrocknet, geräuchert, aus der Dose · gemahlen, ganz | name | **yes** | its own line, **grouped under the head** | a variant if one is known (§D.4) | narrows |
| **Descriptor**: changes neither | groß, klein, reif, frisch, gut, Bio | name (as typed) | no | merges with the plain head | ignored | ignored |
| **Preparation** | gehackt, gewürfelt, geschält, gerieben, zimmerwarm, weich, zum Bestreuen | **note** | no | merges; the note is not carried | ignored | ignored (stays in the D-weighted text as today) |

The identity of a line is then `lemma(head) + {qualifiers}`. Word order, inflection, commas and number do not matter. These are all one identity:
- "grüner Spargel"
- "Grüner Spargel"
- "grünen Spargel"
- "Spargel, grün"
- "Spargel grün"
- "Grünspargel"

"Spargel" and "weißer Spargel" are not that identity.

**Why qualifiers stay in the name, not the note** (the "comment field" idea). The note is ignored by the shopping list and by nutrition, deliberately. That is the property that lets "Butter" and "Butter, gewürfelt" merge. Putting "grün" or "light" there sends the cook into the shop for the wrong asparagus, and counts light coconut milk at full fat. That is A.2's silent loss, made into policy. Every database in §B keeps these words in the identity. The note stays what `RecipeIngredient` already says it is: what you do in the kitchen.

**Why the vocabulary is curated, not learned.**
- Whether a word changes the purchase is cultural, and context-dependent:
  - "gehackte Tomaten" is a product on the shelf.
  - "gehackte Zwiebel" is preparation.
  - "gemahlener Zimt" is a product, while "geriebener Parmesan" is ambiguous.
- The lexicon's own design (search-design §12: small, curated, "not an ontology") already settled this for search. Use the same approach here, with exceptions listed as forms where the combination is a product (`FoodNames` already does this for "gehackte Tomaten"-style names).
- Size: about 60 qualifier forms and 40 preparation forms, in DE and EN.

**Unknown words are left alone.** A word the vocabulary does not know is treated as part of the head ("Jumbo-Garnelen" stays its own identity). That is how every name behaves today, so the worst case is the status quo, never a wrong merge.

### D.2 Singular and plural: one identity, two forms, chosen by the amount

Not a comment field. Number is not information the cook adds; it follows from the amount.

1. **Identity.** A noun table maps singular ↔ plural. The lemma is used wherever names are compared:
   - the shopping merge
   - lookalikes
   - nutrition
   - the ingredient filter
   - editor suggestions

   "1 Tomate" from one recipe and "3 Tomaten" from another become one shopping line, "4 Tomaten".
2. **Display.** When the amount crosses 1 (scaling, the shopping sum, "scale to amount"), swap the noun to the other form. Do this **only if the written form is one of the table's two forms**; an unknown word is never touched. The rule follows Mealie's de-DE choice and the German user in Tandoor #2737:
   - no unit and amount ≤ 1: singular ("1 Zwiebel", "½ Zwiebel")
   - no unit and amount > 1: plural ("1,5 Zwiebeln", "2 Zwiebeln")
   - with a unit and a countable noun: plural ("500 g Tomaten", "1 Dose Kichererbsen")
   - mass nouns have one form and are never swapped (Spargel, Mehl, Kokosmilch, Zucchini)
3. **Adjectives** follow the noun in the nominative: -er and -es become -e ("1 großer Apfel" → "2 große Äpfel", "1 kleines Ei" → "2 kleine Eier"). Feminine is unchanged ("1 rote Zwiebel" → "2 rote Zwiebeln").
4. **Where the table comes from.**
   - `FoodNames` and `CommonIngredients` already list most pairs for the ~260 common foods.
   - Compounds take the plural of their last part: Knoblauch**zehe**n, Fleisch**tomate**n, Lorbeer**blätter**. So a table of heads covers far more names than it lists.
   - A household can set the other form for its own names on the Ingredients settings page, next to merge and rename.
5. **Lookalikes stop proposing singular/plural merges.** They are already one identity, and merging them is what breaks grammar (A.4).

**Assumption:** a curated table of ~200 heads covers most real lines. `nutrition-design.md` §D measured that 150 names cover 78 % of lines and 300 cover 91 %, and countable foods are a minority of those. Check this against the replay or golden fixtures before building.

### D.3 What each consumer does with the reading

- **Editor suggestions** (`OwnIngredientNamesAsync`, `CommonIngredients.Matching`).
  - Match word by word on the folded columns that already exist (`name_ae`/`name_a`, migration 0020): every typed word must begin some word of the name, in any order.
  - "kokos light", "Kokosmilch light" and "Olivenoel" then find their names.
  - When nothing matches, show the typed name as new, plus what Culina understood: "new · Kokosmilch, light". The cook then sees that the variant was recognised, not lost.
- **Line parser** (`parseIngredientLine.ts`).
  - Do not split on a comma inside parentheses.
  - When the text after the comma is only qualifiers ("Spargel, grün", "Kokosmilch, light"), keep it in the name.
  - A preparation word in the head ("gehackte Zwiebeln") is offered for the note as a one-tap hint, never moved silently. Exceptions are names `FoodNames` lists as products.
- **Merge suggestions** (`IngredientLookalikes`).
  - Same food code is no longer enough. Two names are lookalikes only if their **identities are equal**: same lemma and same qualifiers, differing only in spelling, word order, inflection or descriptors. A real typo still counts ("Origano").
  - "grüner Spargel" → "Spargel" and "Vollmilch" → "Milch" are never proposed. "Spargel grün" → "grüner Spargel" is.
- **Shopping list** (`ItemMergePolicy`, `ItemName`).
  - Merge on identity, not on the folded written name.
  - Lines with the same head and different qualifiers sit **together under the head**, not summed: "Spargel — 500 g grün · 1 kg weiß". Plan to Eat does this, and it is what Tandoor #3800 asks for.
  - The note is still not carried. Whether "optional" should be carried is an open question (§E).
- **Nutrition**: see §D.4.
- **Search.**
  - The ingredient filter and smart-shelf rules match by identity. "Spargel" finds every asparagus. "grüner Spargel" finds "Spargel, grün" and "Grünspargel" but not white.
  - The lexicon stops inferring the *Character* `light` from an ingredient's qualifier. "light" on a can is not "leichte Küche".
  - Bump `CulinaryLexicon.Version` so documents are rebuilt.
- **Household keys** (`name_key`).
  - Do not migrate the stored keys. Look up by the written fold first, then by identity, so existing choices keep working and "Tomate" inherits the section the household set for "Tomaten".

### D.4 Nutrition and variants without a food

The qualifier gives nutrition something to say instead of silently guessing:

1. **The variant has a food** (the name, or head + qualifier, is a form): count it as today. For example "fettarme Milch" → 1.5 %, and `FoodVariants` already knows "Mozzarella light".
2. **The qualifier does not change nutrition** (colour for vegetables, size, Bio): count the head's food. Grüner Spargel is G450100, as now.
3. **The qualifier changes nutrition and no food is known** (Kokosmilch light): **do not count it as the head.** The light can is roughly half the energy, and `nutrition-design.md` holds that a wrong number is worse than none. Show "Kokosmilch light: Culina knows Kokosmilch, not the light one" with the correction sheet one tap away.

   This is still "not counted", but with the reason, instead of today's silence.

That leaves the real gap: **BLS has no light coconut milk**, so the correction sheet has nothing to pick. Plenty of shop products will be the same (Skyr light, protein pudding, oat drink). The honest fix is a **household food from the package label**:
- name
- kcal and the seven other values per 100 g, typed from the can
- cited as "from the package" instead of BLS

It is small (one table keyed by household, picked in the same correction sheet), but it is a product decision (§E).

### D.5 Order of work

Each step is useful on its own and none needs the next:

1. **Stop the damage** (small).
   - Lookalikes no longer propose merging different variants (SameFood with differing qualifier words) or singular/plural pairs.
   - The line parser keeps commas inside parentheses.
   - Fix the Kokosmilch section disagreement.
2. **Find what is typed.** Word-wise folded editor suggestions, and a "new · understood as" hint.
3. **Qualifier vocabulary and the name reading** in Domain (one static reader next to `CulinaryLexicon`, with a `Version`). Use it in:
   - lookalikes
   - the line parser (the comma rule)
   - the ingredient filter
   - the lexicon's `light` exception
4. **Shopping by identity.** Merge by lemma and qualifiers, and group variants under the head.
5. **Singular and plural.** The noun table, swapping forms on scale and sum, and the per-household plural on the settings page.
6. **Nutrition variants.** D.4 points 1–3, then household foods if §E says yes.

Run the golden search evaluation (`IntegrationTests/Recipes/Evaluation`) before and after step 3, and the nutrition lines fixture (`nutrition-lines.tsv`) before and after steps 3 and 6.

### D.6 Rejected

- **A food table (Tandoor/Mealie).** It forces every line through a match, rots with imports (§C), and contradicts `domain-model.md`. Everything above works on the text alone.
- **Variants in the note.** It loses them on the shopping list and in nutrition (§D.1).
- **Generating German plurals by rule.** Apfel/Äpfel, Ei/Eier, Paprika/Paprika: an algorithm is wrong often enough to be worse than leaving the word alone. Use the table plus households; unknown words are never touched.
- **A tree of foods.** One implicit level (head → head with qualifiers) covers the cases raised. Tandoor's tree, with inheritance and substitutes, is the complexity §C users already find tedious.

## E. Open questions (product decisions)

1. **Shopping list variants:** grouped under the head (proposed), fully separate (today), or merged with the qualifiers listed?
2. **Household foods** with values from the package (D.4): yes or no? It is the only way "Kokosmilch light" ever gets counted.
3. **Is "Bio" a descriptor or a qualifier?** Proposed: a descriptor. It merges and does not change nutrition, but it stays visible in the recipe.
4. **Notes on the shopping list:** never (today), or carry only "optional"/substitute hints?
5. **Which surface was meant by "cannot find"?** This document assumes the recipe editor's ingredient field and the nutrition correction sheet; both are covered above.

## F. Sources

**Apps and data**
- Tandoor `Food`/`Ingredient` model: https://github.com/TandoorRecipes/recipes/blob/develop/cookbook/models.py
- Tandoor automations: https://docs.tandoor.dev/features/automation/
- Mealie ingredient schema: https://github.com/mealie-recipes/mealie/blob/mealie-next/mealie/schema/recipe/recipe_ingredient.py
- Mealie plural rules per locale: https://github.com/mealie-recipes/mealie/blob/mealie-next/mealie/lang/locale_config.py
- Grocy migrations (`parent_product_id`, `plural_forms`): https://github.com/grocy/grocy/tree/master/migrations
- Bring! German catalogue: https://github.com/miaucl/bring-api/blob/main/bring_api/locales/articles.de-DE.json
- Chefkoch API fields (`usageInfo`): https://github.com/fwcd/d2/blob/main/Sources/D2NetAPIs/Chefkoch/ChefkochRecipe.swift
- Cooklang spec: https://cooklang.org/docs/spec/
- NYT ingredient-phrase-tagger: https://github.com/nytimes/ingredient-phrase-tagger
- ingredient-parser-nlp: https://ingredient-parser.readthedocs.io/
- Spoonacular: https://spoonacular.com/food-api/docs
- Open Food Facts ingredients taxonomy: `api/v2/taxonomy?tagtype=ingredients`
- BLS: https://www.blsdb.de/
- Plan to Eat merging: https://learn.plantoeat.com/help/manually-merge-ingredients-on-your-shopping-list
- Plan to Eat grouping: https://learn.plantoeat.com/help/sort-group-and-combine-items-on-your-shopping-list

**User voices**
- Tandoor:
  - #2934 https://github.com/TandoorRecipes/recipes/issues/2934
  - #672 https://github.com/TandoorRecipes/recipes/issues/672
  - #588 https://github.com/TandoorRecipes/recipes/issues/588
  - #4219 https://github.com/TandoorRecipes/recipes/discussions/4219
  - #2283 https://github.com/TandoorRecipes/recipes/issues/2283
  - #2737 https://github.com/TandoorRecipes/recipes/issues/2737
  - #3964 https://github.com/TandoorRecipes/recipes/issues/3964
  - #3800 https://github.com/TandoorRecipes/recipes/issues/3800
  - #455 https://github.com/TandoorRecipes/recipes/issues/455
  - #3129 https://github.com/TandoorRecipes/recipes/discussions/3129
  - #1320 https://github.com/TandoorRecipes/recipes/issues/1320
- Mealie:
  - FAQ https://mealie.io/documentation/getting-started/faq/
  - PR #6151 https://github.com/mealie-recipes/mealie/pull/6151
  - #5777 https://github.com/mealie-recipes/mealie/issues/5777
  - #70 https://github.com/mealie-recipes/mealie/issues/70
  - #1219 https://github.com/mealie-recipes/mealie/discussions/1219
- AnyList on Hacker News: https://news.ycombinator.com/item?id=35154493

## G. What was built

The §E questions were settled with the defaults this document proposed (bead `culina-v2-p8tf.9`):
- variants are grouped under their head on the shopping list, not summed;
- there are no household foods yet;
- "Bio" is a descriptor;
- notes are not carried to the shopping list.

| Step | Where |
| --- | --- |
| 1. Stop the damage | `IngredientLookalikes` refuses variants and singular/plural pairs; `parseIngredientLine` keeps commas inside parentheses; Kokosmilch is under Konserven |
| 2. Find what is typed | `OwnIngredientNamesAsync` and `CommonIngredients.Matching` match every folded word, in any order |
| 3. The name reading | `Domain/Ingredients/IngredientWords` + `IngredientReading`. Used by lookalikes, the lexicon (no *Character* concept from an ingredient, `Version` 5), the ingredient filters (migration 0038, `IngredientSql`), and the line parser (`qualifierWords.ts`) |
| 4. Shopping by identity | `ItemMergePolicy.NamesMatch` reads names. A new kind is placed under the first one (`ShoppingList.PlaceFor`). A remembered section is found for the same ingredient written otherwise |
| 5. Singular and plural | `Domain/Ingredients/IngredientNouns` and its frontend copy `nounForms.ts`, held together by a test. Recipe lines are scaled through `Scaling.nameFor`, and a shopping line's name follows its amount |
| 6. Nutrition | `NutritionCalculator` tries the other spellings (`IngredientWords.Spellings`), applies a household correction to the same ingredient written otherwise, and reports `unknownKind` for an unlisted kind |
