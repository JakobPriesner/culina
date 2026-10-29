# Recipe Search Architecture & Specification

Specification for Culina's bilingual (German/English) hybrid recipe search engine (`src/backend/Culina.Infrastructure/Search` & `Culina.Domain/Search`).

---

## §1–§6. Core Architecture Decisions

1. **PostgreSQL-Native Hybrid Engine:** Uses PostgreSQL 17 Full-Text Search (`tsvector` with `german` and `english` dictionaries) combined with `pg_trgm` trigram similarity and prefix matching inside a dedicated projection table (`recipe_search_documents`). Zero external search sidecars (Meilisearch/Typesense/Qdrant rejected to keep the single-container footprint minimal).
2. **Synchronous Projection Maintenance:** `RecipeSearchDocumentBuilder` updates `recipe_search_documents` inside the same database transaction whenever a recipe, its ingredients, steps, tags, or categories change.
3. **Bilingual & Diacritic-Insensitive by Default:** Queries match seamlessly across German compounds, Austrian/Swiss culinary terms, English loanwords, and ASCII/umlaut variants (`Käsespätzle` ↔ `kaesespaetzle` ↔ `kasespatzle`).
4. **Tiered Lexical + Structural Ranking:** Deterministic tier dominance ensures exact/title matches always outrank fuzzy or body-only matches, with bounded additive signals for culinary synonyms, pantry overlap, and personal history.
5. **Unified Grammar Across Search & Smart Cookbooks:** The same `QueryGrammar` powers interactive search (`GET /api/v1/recipes`), instant completions (`/completions`), and dynamic smart cookbooks (`Cookbook.SmartFilterQuery`).

---

## §7–§8. Text Normalization & Dual Umlaut Folding (`SearchText.Fold`)

`SearchText.Fold` produces normalized text so users can type `käse`, `kaese`, or `kase` and hit the same index tokens:

- **Unicode NFKD decomposition** strips combining diacritics (`é → e`, `ñ → n`) and lowercases all text.
- **Dual German umlaut emission:** Words containing `ä`, `ö`, `ü`, `ß` emit **both** the digraph transliteration (`ae`, `oe`, `ue`, `ss`) and the stripped vowel (`a`, `o`, `u`, `ss`) into index documents, while query tokens fold canonically via digraph normalization.
- **Hyphen & slash splitting:** Compound tokens like `Tex-Mex` or `Rahm/Sahne` emit both the joined form and individual sub-tokens.

---

## §9–§10. Query Grammar & Inline Filter Tokens (`QueryGrammar`)

Users can combine free-text terms with structured inline operators:

| Syntax                    | Meaning                                 | Example                             |
| ------------------------- | --------------------------------------- | ----------------------------------- |
| `#<tag>`                  | Require tag or category slug            | `#vegetarisch`, `#feierabend`       |
| `@<ingredient>`           | Require ingredient substring            | `@kichererbsen`, `@lachs`           |
| `-<term>` / `!<term>`     | Exclude ingredient, tag, or allergen    | `-koriander`, `!erdnuss`, `!gluten` |
| `~<minutes>[m\|h]`        | Maximum total time (`Prep + Cook`)      | `~30m`, `~20`, `~1h`                |
| `k:<kcal>` / `<kcal>kcal` | Maximum calories per serving            | `k:600`, `500kcal`                  |
| `"<phrase>"`              | Exact phrase match in title/ingredients | `"schwarze bohnen"`                 |

Natural-language German time/diet phrases in free text (e.g. `"unter 30 minuten"`, `"schnelle vegane pasta"`) are also extracted automatically by `QueryGrammar` into structured filter predicates plus residual lexical tokens.

---

## §11. Dietary & Allergen Inference (`DietInference`)

Dietary compliance (`vegan`, `vegetarisch`, `glutenfrei`, `laktosefrei`, `nussfrei`) uses a **two-level model**:

1. **Explicit Confirmation:** Recipe carries the explicit tag (`#vegan`, `#glutenfrei`).
2. **Ingredient Presumption (`PresumedDiet`):** Evaluated at index build time by scanning normalized `RecipeIngredient` names against curated exclusion lexicons (`MeatFishKeywords`, `AnimalProductKeywords`, `GlutenKeywords`, `LactoseKeywords`, `TreeNutPeanutKeywords`).
    - Recipes with zero offending ingredients (and `>= 2` ingredients total) are flagged as presumed-compatible in `recipe_search_documents.presumed_diets`, allowing `#vegan` or `!laktose` filters to surface untagged compliant recipes while ranking explicitly tagged recipes slightly higher.

---

## §12. `recipe_search_documents` Schema & Four Index Lanes

Each `Recipe` has a 1:1 row in `recipe_search_documents`:

| Column               | Type       | Index                | Purpose                                                                                 |
| -------------------- | ---------- | -------------------- | --------------------------------------------------------------------------------------- |
| `recipe_id`          | `uuid PK`  | B-Tree PK            | Joins back to `Recipes`                                                                 |
| `household_id`       | `uuid`     | B-Tree               | Tenant visibility pre-filter                                                            |
| `title_folded`       | `text`     | `GIN (gin_trgm_ops)` | Lane 1: Title prefix, substring & fuzzy trigram match                                   |
| `ingredients_folded` | `text`     | `GIN (gin_trgm_ops)` | Lane 2: Ingredient `@zutat` & fuzzy trigram match                                       |
| `tags_folded`        | `text[]`   | `GIN`                | Exact & prefix `#tag` / category array match                                            |
| `presumed_diets`     | `text[]`   | `GIN`                | Inferred dietary & allergen-free flags                                                  |
| `search_vector_de`   | `tsvector` | `GIN`                | Lane 3: Weighted German FTS (`A`: title, `B`: ingredients/tags, `C`: description/steps) |
| `search_vector_en`   | `tsvector` | `GIN`                | Lane 4: Weighted English FTS stemmer                                                    |
| `total_time_minutes` | `int?`     | B-Tree               | Fast `~30m` time ceiling filter                                                         |

---

## §13–§16. Candidate Retrieval, Tiers (`0`–`D`) & Ranking Formula

### Candidate Gate & Union Index Scan (§27.6)

To avoid sequential table scans, candidate retrieval issues a union of index-backed predicates (`search_vector_de @@ tsquery OR search_vector_en @@ tsquery OR title_folded % query OR ingredients_folded % query`), restricted by household/cookbook visibility and hard filters (`~time`, `k:kcal`, `#tag`, `!exclusion`).

### Deterministic Relevance Tiers

Every matching candidate is assigned to its highest qualifying structural tier (`TierBaseScore`):

| Tier         | Base Score | Qualification Rule                                                                                |
| ------------ | ---------- | ------------------------------------------------------------------------------------------------- |
| **Tier `0`** | `1000`     | Exact normalized title match or exact multi-word phrase match at start of title                   |
| **Tier `A`** | `800`      | All query tokens match in `title_folded` (exact word or prefix `>= 3` chars)                      |
| **Tier `B`** | `600`      | Query tokens split across `title_folded` + `ingredients_folded` / `tags_folded`                   |
| **Tier `C`** | `400`      | All query tokens satisfied via `ingredients_folded`, `tags_folded`, or `CulinaryLexicon` synonyms |
| **Tier `D`** | `200`      | Body/step-only FTS match or fuzzy trigram (`pg_trgm`) typo match (`similarity >= 0.32`)           |

### Within-Tier Additive Signals (Bounded `< 180` pts so Tiers Never Cross)

$$\text{Score}(r) = \text{TierBase}(r) + 60 \cdot \text{TitleSim}(r) + 40 \cdot \text{FtsRank}(r) + 25 \cdot \text{IngCoverage}(r) + 15 \cdot \text{SynonymBoost}(r) + \text{PersonalTieBreak}(r)$$

---

## §17. Culinary Lexicon & Regional Equivalents (`CulinaryLexicon`)

`CulinaryLexicon` expands query tokens with bidirectional and directional culinary synonyms before building `tsquery` and ingredient predicates:

- **Regional DE/AT/CH:** `Karotte ↔ Möhre ↔ Rüebli`, `Kartoffel ↔ Erdapfel`, `Sahne ↔ Schlagobers ↔ Rahm`, `Quark ↔ Topfen`, `Frühlingszwiebel ↔ Lauchzwiebel ↔ Jungzwiebel`, `Hackfleisch ↔ Faschiertes`, `Aubergine ↔ Melanzani`, `Zucchini ↔ Zucchetti`, `Feldsalat ↔ Vogerlsalat ↔ Nüsslisalat`.
- **DE ↔ EN Culinary Loanwords:** `Kichererbsen ↔ Chickpeas`, `Haferflocken ↔ Oats`, `Erdnussbutter ↔ Peanut Butter`, `Süßkartoffel ↔ Sweet Potato`, `Kurkuma ↔ Turmeric`, `Kreuzkümmel ↔ Cumin`.
- **Category Hypernyms:** Searching `pasta` or `nudeln` expands to `spaghetti`, `penne`, `rigatoni`, `tagliatelle`, `fusilli`, `lasagne`, `gnocchi`.

---

## §18. Personalisation Tie-Break Cap (§18.8)

Personal signals (user rating `1–5★`, favorite flag, and cook count) act strictly as a **within-tier tie-breaker** capped at **`+18` points max** (`+8` for favorite, up to `+6` for 5★ rating, up to `+4` for cook frequency). A 5-star recipe with a weak body match (Tier `D`) can never jump ahead of an unrated recipe whose title directly matches the query (Tier `A`).

---

## §19. Search Endpoints & Surface Contracts

- **§19.1 `GET /api/v1/recipes`:** Returns paginated recipe cards, applied parsed tokens (`parsedQuery`), facet counts, and recovery hints if zero results matched.
- **§19.2 `GET /api/v1/recipes/completions`:** Sub-15ms prefix + trigram lookup returning up to `8` grouped suggestions (`Rezepte`, `Zutaten`, `Tags & Kategorien`).
- **§19.3 `GET /api/v1/recipes/{id}/related`:** Combines explicit `RecipeRelations` (`SideDish`, `BaseRecipe`, `Variant`) with content-based neighbors scored by shared non-staple ingredients (`Jaccard` weighted by IDF) + shared category/tags.
- **§19.4 Search Overlay UX (`SearchOverlay.svelte`):** Supports instant keyboard navigation, clickable filter chips (`#vegetarisch`, `~30m`, `Vorrat passt`), and recent search persistence.

---

## §20–§23. Lookalikes, Auto-Tagging, Smart Cookbooks & Recovery Ladder

- **§20 Duplicate Lookalikes (`GET /api/v1/recipes/lookalikes`):** Checks normalized `SourceUrl` equality first, then `title_folded` trigram similarity (`>= 0.55`) during intake/creation to warn before importing duplicates.
- **§21 Deterministic Tag Suggestions (`POST /api/v1/tags/suggest`):** Rule-based classifier inspecting prep time (`<= 25m → #schnell`), cook vessel (`pfanne/blech/topf → #one-pot`, `#blechgericht`), meal type, and dietary presumptions (`#vegan`, `#vegetarisch`, `#high-protein`).
- **§22 Smart Cookbooks (`SmartFilterEvaluator`):** Evaluates saved `SmartFilterQuery` strings (e.g. `#asiatisch ~30m -koriander`) dynamically against `recipe_search_documents`.
- **§23 Zero-Result Recovery Ladder:** When a multi-constraint query returns `0` hits, the API progressively relaxes constraints in order: (1) drop `~time` / `k:kcal` ceiling → (2) drop `#tag` filter → (3) widen lexical AND to OR / trigram fuzzy match, returning actionable one-click relaxation pills.

---

## §24–§27. Golden Evaluation & Index Benchmark (`SearchGoldenTests`)

- `src/backend/Culina.Tests/Search/` runs deterministic golden-set assertions (`NDCG@5 >= 0.92`, `MRR >= 0.90`, zero tier inversions across German compounds, Austrian/Swiss synonyms, typos, and negative `!allergen` filters).
- **§27.6 Union Index Scan Verification:** Integration tests assert via `EXPLAIN (FORMAT JSON)` that candidate queries use `Bitmap Index Scan` across the 4 GIN indexes on `recipe_search_documents`.
