# Culina — search: retrieval, query understanding, ranking and experience

The design for Culina's search system: what it should do, what it should be
built from, how it ranks, how it feels, and what it deliberately refuses.

Layer rules come from `dotnet-project-setup`; the domain vocabulary from
[`domain-model.md`](domain-model.md); the HTTP conventions from
[`api.md`](api.md) and `rest-api-design`; the interaction conventions from
[`design-system.md`](design-system.md). This document fixes *what search is*
and *why it works the way it does*. Tracked as `culina-v2-0r34`.

---

## Table of contents

1. [Executive summary](#1-executive-summary)
2. [The application as it stands, and the search it has](#2-the-application-as-it-stands-and-the-search-it-has)
3. [Requirements](#3-requirements)
4. [A taxonomy of the queries people actually type](#4-a-taxonomy-of-the-queries-people-actually-type)
5. [Technology research](#5-technology-research)
6. [Comparison matrix](#6-comparison-matrix)
7. [Query understanding](#7-query-understanding)
8. [Lexical search](#8-lexical-search)
9. [Semantic search](#9-semantic-search)
10. [Hybrid search and fusion](#10-hybrid-search-and-fusion)
11. [Ranking and reranking](#11-ranking-and-reranking)
12. [The culinary lexicon](#12-the-culinary-lexicon)
13. [Personalisation and context](#13-personalisation-and-context)
14. [The recommended architecture](#14-the-recommended-architecture)
15. [The search data model](#15-the-search-data-model)
16. [Backend integration](#16-backend-integration)
17. [The search API](#17-the-search-api)
18. [Frontend and interaction design](#18-frontend-and-interaction-design)
19. [One intelligence layer: related recipes, duplicates, tagging, shelves](#19-one-intelligence-layer-related-recipes-duplicates-tagging-shelves)
20. [Performance and resources](#20-performance-and-resources)
21. [Privacy and self-hosting](#21-privacy-and-self-hosting)
22. [Failure and fallback behaviour](#22-failure-and-fallback-behaviour)
23. [Search-quality evaluation](#23-search-quality-evaluation)
24. [Testing strategy](#24-testing-strategy)
25. [Implementation roadmap](#25-implementation-roadmap)
26. [Risks and tradeoffs](#26-risks-and-tradeoffs)
27. [What phase 1 actually shipped](#27-what-phase-1-actually-shipped)

---

## 1. Executive summary

### The recommendation in one paragraph

Build search **inside PostgreSQL**, as a maintained per-recipe search document
combining a weighted `tsvector`, a trigram-indexed folded text, and a
concept array; retrieve through **four named lanes** (exact, lexical, fuzzy,
concept); parse the query with a **deterministic bilingual grammar** that turns
"vegetarisch unter 30 Minuten mit Kartoffeln" into real filters and shows the
user what it understood; and rank by an **explicit tier followed by a bounded
additive score**, never by an opaque similarity number. No second server, no
embedding model, no vector database, no LLM. The whole thing adds one table,
one migration, about 1,200 lines of C#, and roughly 8 MB of index for a
2,000-recipe library.

### The five decisions that matter

**1. Replacing `ILIKE` with full-text search alone would make German search
worse.** The single most important technical fact in this document. Culina's
current search is `title ILIKE '%q%'`, and the one thing substring matching
gets right by accident is the hardest thing about German: compound nouns.
`Hähnchen` finds `Hähnchenbrustfilet` today. `to_tsvector('german', …)` will
not — the Snowball stemmer strips suffixes, it does not decompose compounds.
Any design that swaps `ILIKE` for `@@` and calls it an upgrade ships a
regression on the app's primary content language. The trigram lane is therefore
not a nice-to-have bolted on for typos; it is load-bearing, and it stays.

**2. Synonym expansion belongs in the application, not in a PostgreSQL synonym
dictionary.** A `synonym` or `thesaurus` template dictionary would work, and it
is the textbook answer. It is the wrong answer here for two reasons. It needs
files placed in the database container's `$SHAREDIR/tsearch_data`, which turns
one image into two maintained images and puts a piece of Culina's behaviour
somewhere `docs/operations.md` would have to teach people to back up. More
decisively: once a synonym is resolved inside the dictionary, *the index cannot
tell you it was a synonym*. `Huhn` and `Hähnchen` arrive as the same lexeme and
rank identically. Ranking has to know the difference — a direct match must beat
a synonym match — so the expansion has to happen where the ranking can see it.

**3. Reciprocal Rank Fusion is the wrong fusion mechanism for this problem.**
RRF exists to combine rankers whose scores are incomparable, and it works by
throwing score magnitude away. Culina's lanes are not independent opinions
about the same question; they are *evidence of different quality*, in a known
order. An exact title match is not one vote among four — it is the answer. RRF
would happily let three weak lanes outvote it, which is precisely the failure
the product must not have. Tier first, score within tier. RRF becomes correct
the day a genuinely independent semantic lane exists, and not before.

**4. Personalisation is a tie-break, and nothing more.** Search relevance is
household-scoped and identical for every member. What one person cooks may
reorder two results that the relevance model already considers equivalent; it
may never move a result between tiers, and it is capped at a size that cannot
change the first screen's membership. Two people in one kitchen searching
`Bolognese` get the same recipes in almost the same order, which is the only
way search stays trustworthy.

**5. No embeddings.** Not "not yet, pending budget" — not, for a reason that is
structural rather than about resources. *A household recipe library is a corpus
where the user has seen every document.* Typical size is 50–500 recipes, all of
them written or imported by the four people searching them. The job is
**recall of the known**, not discovery of the unknown, and that is lexical
search's home ground. Dense retrieval earns its cost when a user cannot
enumerate the corpus; here they nearly can. Section 9 sets a measurable gate
that would reverse this, and section 9 also predicts, with reasons, that the
gate will not be met.

### What is deliberately not built

Named here so that the absence reads as a decision rather than an oversight.

| Not built | Why not |
| --- | --- |
| Meilisearch / Typesense / Elasticsearch / OpenSearch | A second container, a second backup artefact, a second thing that can disagree with PostgreSQL, ~80–500 MB RSS, for a corpus that fits in a browser tab. Breaks "one image serving both halves". |
| Embeddings + pgvector / Qdrant | §9. ~120–470 MB of model in the image, ~250 MB RSS, a second database image, to improve the one query class the user is least able to verify. |
| Cross-encoder reranking | 30–80 ms per *candidate* on a home CPU. A 50-candidate rerank is a 2-second search. |
| Learning to rank | Needs labelled relevance judgements. Eight users produce perhaps forty clicks a week. The model would be noise. |
| LLM query parsing (local or remote) | Latency, an availability dependency on the search box, and non-determinism in a UI that shows the user what it parsed. §7 designs the optional, non-blocking version anyway. |
| Hunspell compound splitting in PostgreSQL | Dictionary files in the DB container; §8 shows the trigram lane already solves compounds. |
| Seasonality ranking from an ingredient→month table | Already rejected on `culina-v2-erv` for the reason `README.md` rejects a pantry: curated data nobody maintains, going stale, poisoning everything above it. §12 handles `Sommergericht` without it. |
| "Popular searches" / trending | A statistic over 2–8 people is not a statistic, and showing a household what its members search for is a privacy leak inside the home. Recent searches are per-person and never leave the browser. |
| A `/search` page | §18. Search that navigates away has already lost. |

### Expected outcome

| | Today | After phase 1 | After phase 2 |
| --- | --- | --- | --- |
| `Bolognäse` finds Bolognese | no | yes | yes |
| `Hühnchen` finds `Hähnchen` recipes | no | yes | yes |
| `chicken` finds `Hähnchenbrust` | no | yes | yes |
| `Hähnchen` finds `Hähnchenbrustfilet` | yes | yes | yes |
| `vegetarisch unter 30 Minuten` | 0 results | 0 results | filters + ranks |
| `was kann ich mit Kartoffeln machen?` | 0 results | 0 results | ingredient query |
| Search p95, 2,000 recipes | ~25 ms | ~45 ms | ~55 ms |
| Container images | 1 | 1 | 1 |
| Added RSS | — | ~0 | ~4 MB |

---

## 2. The application as it stands, and the search it has

### The shape of the data

Everything search can use already exists. Nothing in this design requires a new
field on `Recipe`.

```
Household ──< Recipe
                ├── title            text, required, the only required field
                ├── description      text?, ≤ 2000
                ├── language         'de' | 'en'   ← per recipe, not per user
                ├── prep_minutes     int?          ← total_minutes is derived
                ├── cook_minutes     int?
                ├── yield            amount + kind ('servings' | 'pieces')
                ├── image_id         uuid?
                ├──< IngredientGroup ──< RecipeIngredient
                │                          ├── name     the shoppable noun
                │                          ├── note     'finely chopped'
                │                          └── quantity + unit
                ├──< Step ── body (with [[ingredient:id]] tokens)
                │              └──< StepIngredientRef   (derived index)
                └──< RecipeTag >── Tag (household-scoped: name + slug)

User ──< CookLogEntry ── Recipe    append-only, dated, per person
User ──< CookSession  ── Recipe    resume point
User ──< PersonalNote ── Recipe
Household ──< Cookbook             manual rows, or smart rules
Household ──< MealPlanEntry ── Recipe
```

Five properties of this model matter enormously for search, and three of them
are unusual enough to be worth naming.

**`recipes.language` is per recipe, not per user.** A household reading the app
in English can hold German recipes, and
[`domain-model.md`](domain-model.md#ingredient-suggestions) already commits to
naming ingredients in the *recipe's* language. This is exactly the right shape
for search: it lets each document be analysed with the right stemmer without
guessing, which is the hard part of multilingual retrieval and which Culina has
already solved by accident of good modelling.

**There is no ingredient table.** An ingredient is whatever somebody typed.
That rules out an ingredient-id-based ontology and forces name-level matching —
which is fine, and which the `CommonIngredients` / `SectionKeywords` pair
already establishes the pattern for.

**Tags are household-scoped free vocabulary.** One household's `vegetarisch` is
another's `veggie` is a third's nothing at all. Search cannot assume tags exist,
and must not require them; but where they exist they are the single most
authoritative signal available, because a person put them there on purpose.

**`cook_log_entries` is the honest popularity signal.** There are no ratings
and there will be none (`README.md`). "I made this" is a better signal than a
star, and it is already recorded, dated, and scoped per person.

**Steps know their ingredients.** `step_ingredient_refs` is a derived table
rebuilt on every save — the precedent for maintaining a derived index inside
the write transaction already exists in this codebase, which is exactly the
mechanism §16 proposes to reuse.

### The search there is

One SQL query in
[`RecipeSearcher.cs`](../src/backend/src/Infrastructure/Persistence/Recipes/RecipeSearcher.cs).
The free-text predicate is, in its entirety:

```sql
and (@query is null or (
      r.title       ilike @queryLike
   or r.description ilike @queryLike
   or exists (select 1 from recipe_ingredients ri
              join ingredient_groups g on g.id = ri.group_id
              where g.recipe_id = r.id and ri.name ilike @queryLike)))
```

with `@queryLike = '%' || query || '%'`, backed by two GIN trigram indexes
(`recipes_title_trgm_idx`, `recipe_ingredients_name_trgm_idx`). Ranking, when
`sort=relevance`, is:

```sql
matched_ingredients desc, extra_ingredients asc, updated_at desc, id desc
```

which ranks the *ingredient* filter and ignores the text query entirely.

It is a good piece of engineering for what it is. It is one query, so the count
and the page cannot disagree; the cursor is a row comparison, so paging is
stable; the smart-shelf predicate is shared with the cookbook card, so a shelf
and its count cannot drift. None of that should change, and this design keeps
all of it.

### What it cannot do

Measured against the queries in §4, from an ordinary German household library:

| Query | Today | Why |
| --- | --- | --- |
| `Bolognese` | works | substring |
| `bolognese` | works | `ilike` |
| `Bolognäse` | **nothing** | `ä` ≠ `a`; no folding anywhere in the query path |
| `Bolgnese` | **nothing** | substring is all-or-nothing; the trigram index is used for the `ILIKE` scan, not for similarity |
| `Hühnchen` | **nothing** | no relation to `Hähnchen` |
| `Huhn` | **nothing** | not a substring of `Hähnchen` |
| `chicken` | **nothing** | no cross-language anything |
| `Hähnchen` → `Hähnchenbrustfilet` | works | substring, and this is the part that must survive |
| `Nudeln` → `Nudelauflauf` | works | substring |
| `Tomaten` → `passierte Tomaten` | works | substring |
| `Tomate` → `Tomaten` | works | substring |
| `Tomaten` → `Tomate` | **nothing** | substring is directional |
| `vegetarisch unter 30 Minuten` | **nothing** | the whole phrase is one substring |
| `schnelles Abendessen` | **nothing** | ditto |
| `was kann ich mit Kartoffeln machen?` | **nothing** | ditto |
| `Pasta` → `Spaghetti Bolognese` | **nothing** | no concept relation |
| `Bolognese` → `Spaghetti Bolognese` before `Lasagne Bolognese` | arbitrary | ranked by `updated_at` |

Nine of seventeen return nothing. The failures are not exotic: `Bolognäse`,
`Hühnchen` and `unter 30 Minuten` are things a person types on a Tuesday.

There are two further problems that do not show up as empty results.

**Text matches do not affect ranking at all.** `sort=relevance` orders by
ingredient overlap; with a text query and no `ingredient` parameters, every row
has `matched_ingredients = 0` and the order collapses to `updated_at desc`.
Searching `Bolognese` in a library with three of them returns them in the order
they were last edited. The user's example — `Spaghetti Bolognese` above
`Lasagne Bolognese` — is currently a coin flip.

**Steps are not searched.** `description` is, ingredient *names* are, but step
text is not, so "the one where you deglaze with red wine" is unfindable. This
is deliberate under the current design (a `%…%` scan over step bodies would be
expensive) and stops being a tradeoff once there is a real index.

### The frontend as it stands

- `SearchField` (design system) — a debounced input with a clear button and
  `Escape` handling. Solid, reusable, and the base for everything in §18.
- `libraryView` — a tiny store holding `query` and a `quick` boolean, scoped to
  the household so the library keeps its place across a recipe round trip.
- `recipes.svelte.ts` — the store. It already carries a `#readToken` guard for
  out-of-order responses, keeps the previous list on screen during a refetch,
  and stops auto-paging after a failure. All three are correct and all three are
  reused.
- `RecipePicker` — the meal-plan and shopping-list picker, with its **own**
  store instance so the sheet's search does not empty the page behind it. The
  right call, and the precedent for the search overlay in §18.
- Debounce is 250 ms in both places, chosen when a search was a full page load.

There is no global search, no keyboard entry point, no autocomplete, no
suggestions, and no way to see or edit what a query was interpreted as. Search
exists on exactly two surfaces and is a toolbar field on both.

### Deployment constraints this design must respect

From `README.md`, `Dockerfile` and `compose.prod.yaml`:

- **One image.** API and SPA from the same origin, same process. This is not
  packaging convenience — it is what removes CORS and makes the cookie/CSRF
  model sound. A second container for search is a real cost against a stated
  architectural commitment.
- **`postgres:18-alpine`**, superuser-installed extensions in
  [`scripts/db-init.sh`](../scripts/db-init.sh): `citext`, `pg_trgm`,
  **`unaccent`**. The app role is not a superuser and cannot create extensions.
  *`unaccent` is already installed and currently unused.*
- **`--locale=C`** on the cluster. Deterministic collation only. Text search
  configurations are independent of collation, so this does not constrain FTS,
  but it does mean `ORDER BY title` is byte order — worth knowing, unchanged
  here.
- `read_only: true`, `cap_drop: ALL`, two volumes (`/data/images`,
  `/data/keys`). Anything that wants to write to disk needs a third volume and
  a paragraph in `docs/operations.md`. This design writes nothing to disk.
- Frontend weight budget: 140 kB of JavaScript across every route, 97.4 kB used.
  There is ~42 kB of headroom, and `culina-v2-xcs` says even that is contested.
  Client-side search libraries are priced accordingly.
- **No model on the search path.** Culina now has an assistant — it writes and
  tidies recipes — but nothing here depends on it. The reasons below are about
  latency, determinism and a search box that must not stop working when somebody
  else's API does, and none of them changed when the README line did.

---

## 3. Requirements

### What excellent recipe search means here

Culina's search is not a discovery engine. It is the answer to *"where did I
put that?"* in a cupboard the user stocked themselves. Everything below follows
from that.

**R1 — Forgiving about spelling.** `Bolognäse`, `Bolognese` and `Bolgnese` are
one query. So are `Müsli`/`Muesli`, `Soße`/`Sosse`/`Sauce`, `Joghurt`/`Yoghurt`.
A German keyboard is not always what is to hand, and nobody proofreads a search
box.

**R2 — Forgiving about grammar.** `Tomate`/`Tomaten`, `Nudel`/`Nudeln`,
`gebacken`/`backen`/`Backofen` are one query. Both directions: today
`Tomaten` finds `Tomate` but not the reverse.

**R3 — Compound-aware.** `Hähnchen` must find `Hähnchenbrustfilet`;
`Nudel` must find `Nudelauflauf`; `Kartoffel` must find `Kartoffelgratin` and
`Süßkartoffel`. This is the single most common German failure mode and the
thing the current implementation already gets right.

**R4 — Concept-aware, in two languages.** `Hühnchen` finds `Hähnchen`, `Huhn`,
`Poulet` and `chicken`. `chicken` finds `Hähnchenbrust`. `Pasta` finds
`Spaghetti`, `Nudeln`, `Penne`. Crucially: **a concept match is visibly weaker
than a direct match**, and ranks accordingly.

**R5 — Structured intent.** `vegetarisch unter 30 Minuten mit Kartoffeln` is
three filters and an ingredient preference, not a string. The system must
extract them, apply them, and *show the user that it did*, with a way to undo
each one.

**R6 — Hard constraints are hard.** `vegetarisch` must never return a recipe
with `Hackfleisch` in it, however semantically adjacent. Constraints filter;
they do not boost.

**R7 — Predictable, explainable order.** Given the query `Bolognese`, a person
should be able to look at the result list and understand it without being told.
Exact beats strong-lexical beats structural beats associative, always, and a
result that is only here because of a synonym says so.

**R8 — Instant.** Results change as the query does. p95 ≤ 60 ms server-side for
search, ≤ 40 ms for suggestions, on the hardware in §20.

**R9 — Never empty without an offer.** Zero results is a failure of the system,
not of the user. §22 defines the recovery ladder.

**R10 — Everywhere, without navigation.** Search is reachable from any screen
by keyboard or by one tap, opens over what you were doing, and leaves it
untouched when it closes.

**R11 — One retrieval system.** The library grid, the meal-plan picker, the
shopping-list picker, related recipes, duplicate detection at import and smart
cookbook rules are all the same question asked differently. One service.

**R12 — Private and local.** No query leaves the server. No corpus leaves the
server. Search works with the network cable unplugged from the house.

### Non-requirements

- **Scale.** 2–8 users, 50–2,000 recipes, a handful of searches a day each. A
  design that would also serve 10,000 queries per second is a design that
  bought something nobody will use with money that could have been spent on
  quality.
- **Nutrition, calories, difficulty.** `README.md` rules these out, so no
  ranking signal may depend on them.
- **Cross-household search.** Recipes are household-owned; the household filter
  is a security boundary and stays the outermost predicate in every query.
- **Web-scale spell correction.** §7 corrects against the household's own
  vocabulary, which is both smaller and better.

### Quality targets

| Metric | Target | Measured by |
| --- | --- | --- |
| Precision@3 over the golden set | ≥ 0.85 | §23 |
| NDCG@10 over the golden set | ≥ 0.80 | §23 |
| MRR for known-item queries | ≥ 0.95 | §23 |
| Zero-result rate on the golden set | 0 except where empty is correct | §23 |
| Typo recovery (edit distance ≤ 2) | ≥ 0.90 | §23 |
| Cross-language recall (DE↔EN culinary head nouns) | ≥ 0.85 | §23 |
| Search p95 | ≤ 60 ms | §20 |
| Suggestion p95 | ≤ 40 ms | §20 |
| Added resident memory | ≤ 10 MB | §20 |

---

## 4. A taxonomy of the queries people actually type

Eleven classes. Each names how it should be retrieved and what should rank
first, and each is a section of the golden set in §23. The German is what a
German household types; the English column is not a translation but the
equivalent query an English-speaking member would type into the same library.

### A. Known-item, exact

> `Spaghetti Bolognese` · `Omas Käsekuchen` · `Butter chicken`

The user knows the recipe exists and roughly what it is called. **This is the
majority class and the one that must never fail.** Retrieval: exact/prefix lane.
Rank 1 must be that recipe. Everything else on the page is noise the user will
not read.

### B. Known-item, partial or reordered

> `Bolognese` · `Käsekuchen` · `Kuchen Oma` · `chicken curry`

A fragment, or the words in the wrong order. Retrieval: lexical lane with
title-band weighting. Rank 1 should be the recipe whose title the fragment is
most of — §11 handles the `Spaghetti Bolognese` vs `Lasagne Bolognese` case.

### C. Misspelled

> `Bolgnese` · `Bolognäse` · `Spagetti` · `Haenchen` · `Zuccini` · `Muesli`

Two distinct sub-problems that are usually conflated:

- **Orthographic variants** — `ä`/`ae`/`a`, `ß`/`ss`, `Joghurt`/`Yoghurt`.
  These are *not typos*; they are correct alternative spellings and must be
  handled by **folding**, deterministically, at index and query time both.
- **Actual typos** — transposition, omission, doubled letters. Handled by
  **trigram similarity**, with a threshold.

Conflating them is how you end up with a fuzzy matcher doing work a `replace`
should do, at ten times the cost and half the accuracy.

### D. Inflected or derived form

> `Tomate` for `Tomaten` · `Nudel` for `Nudeln` · `gebackene` for `backen` ·
> `Kartoffeln` for `Kartoffel`

Retrieval: the German/English Snowball stemmer in the FTS lane. Plural,
genitive and participle forms collapse; this is the one thing FTS does that
nothing else does as cheaply.

### E. Compound

> `Hähnchen` → `Hähnchenbrustfilet` · `Kartoffel` → `Kartoffelsalat`,
> `Süßkartoffelcurry` · `Nudel` → `Nudelauflauf` · `Zwiebel` → `Röstzwiebeln`

Retrieval: **trigram substring**, exclusively. The German Snowball stemmer does
not decompose compounds and never will; §8 covers why Hunspell is not the
answer here.

### F. Synonym and cross-language

> `Hühnchen` → `Hähnchen` · `Huhn` → `Hähnchen` · `chicken` → `Hähnchen` ·
> `Poulet` → `Hähnchen` · `Aubergine` → `Melanzani` · `Pasta` → `Nudeln` ·
> `Rahm`/`Sahne`/`cream` · `Gockel` → `Hähnchen` (weakly)

Retrieval: concept lane. **Always ranks below a direct match**, and the result
row says why it is there. `Gockel` is the interesting edge: it is a real German
word for a rooster, it is colloquial, and expanding it to `Hähnchen` is right —
but a recipe that literally contains `Gockel` must outrank one that only
contains `Hähnchen`, which the tier system gives for free.

### G. Ingredient-led

> `etwas mit Hähnchen` · `was kann ich mit Kartoffeln machen?` ·
> `Rezept mit Zucchini und Feta` · `what can I make with potatoes`

The carrier phrase (`etwas mit`, `was kann ich mit … machen`, `Rezept mit`) is
stripped; the nouns become **ingredient preferences**, which Culina already
supports and already ranks well — `matched_ingredients desc, extra_ingredients
asc` is a genuinely good answer to "what can I cook". This class is mostly a
parsing problem, and the retrieval half already exists.

### H. Constraint

> `unter 30 Minuten` · `vegetarisch` · `ohne Fleisch` · `schnell` ·
> `Frühstück` · `italienisch` · `under 30 minutes` · `vegan`

Pure structured query, no free text left over after parsing. Retrieval: filters
only; rank by the default order (recently updated), or by cook count when the
household has one. These must become **chips**, because a constraint the user
cannot see is a constraint they cannot remove.

### I. Compound natural language

> `vegetarisches Abendessen unter 30 Minuten mit Kartoffeln` ·
> `schnelles Abendessen ohne Fleisch` · `Nudeln mit Tomatensoße` ·
> `leichte Sommergerichte`

Parses into several constraints plus residual free text. The residual is what
goes to the text lanes. `Nudeln mit Tomatensoße` is the instructive one: it
parses to *no* constraints — `mit` here joins two food nouns rather than
introducing an ingredient list — and should simply be two strong content terms.
Over-parsing is worse than under-parsing, and §7 biases accordingly.

### J. Vague / mood

> `warme Mahlzeit` · `comfort food` · `etwas leichtes für den Sommer` ·
> `Winteressen` · `Abendessen mit wenig Aufwand`

The class embeddings are supposedly for. In practice, in a 300-recipe library,
each of these has an honest answer built from things that are already written
down: `warme Mahlzeit` = not a salad, not a dessert; `wenig Aufwand` =
`maxMinutes ≈ 30` and few ingredients; `Sommer` = the household's own `Salat`,
`Grillen`, `kalt` tags. §12 handles these as **concepts that map to the
household's own vocabulary**, and §22 makes the answer honest when they map to
nothing: an empty result with a suggestion beats five confidently wrong results.

### K. Contradictory or unanswerable

> `vegetarisch mit Lachs` · `Bolognese vegan unter 5 Minuten` ·
> `Schnitzel` in a library that has none

The system must distinguish *"your filters conflict"* from *"you have no such
recipe"* and say which. §22.

### Distribution, and what it implies

An estimate for a household library, from the shape of the app rather than from
telemetry Culina does not collect:

| Class | Share | |
| --- | --- | --- |
| A + B known-item | **~55 %** | ████████████████████████ |
| C misspelled | ~10 % | ████ |
| D + E morphology | ~8 % | ███ |
| G ingredient-led | ~10 % | ████ |
| H constraint | ~8 % | ███ |
| F synonym | ~5 % | ██ |
| I compound NL | ~3 % | █ |
| J vague | **~1 %** | ▏ |

**Over half of all searches are somebody looking for a recipe they know they
have.** That single fact is the whole argument of this document. It is why
exact-match ranking and typo tolerance are worth more than any amount of
semantic sophistication, why the tier system is ordered the way it is, and why
§9 concludes what it concludes.

---

## 5. Technology research

Every candidate is evaluated against the same question: *what does it buy, for
a 2–8 user home server running one container against one PostgreSQL, where the
corpus is 50–2,000 German and English recipes?*

### 5.1 PostgreSQL full-text search

Built in, already there, no extension needed.

- `to_tsvector('german', …)` — Snowball stemmer + German stopwords. `setweight`
  gives four bands (A–D) so title, tags, ingredients and body can be scored
  differently in one vector.
- `ts_rank_cd` implements a cover-density rank with the standard
  length-normalisation flags, which is BM25-adjacent in behaviour. It is not
  BM25 — it has no IDF term — but at 300 documents IDF is close to noise
  anyway, and §11 supplies the discrimination IDF would have.
- `websearch_to_tsquery` parses user-ish syntax (quotes, `or`, `-`) safely and
  never throws on malformed input, unlike `to_tsquery`.
- GIN index, incremental, ~1–2 KB per recipe.

**German specifically.** Two facts decide the design.

1. **The `german` Snowball stemmer does not remove umlauts.**
   `to_tsvector('german', 'Hähnchen')` and `to_tsvector('german', 'Haehnchen')`
   produce different lexemes. The documented fix is a custom configuration
   chaining `unaccent` before the stemmer
   ([dbi-services](https://www.dbi-services.com/blog/dealing-with-german-umlaute-in-postgresqls-full-text-search/)),
   which folds `ä→a`. Note that this is `ä→a`, **not** `ä→ae` — so `Müsli` and
   `Muesli` still do not meet. Culina's own `ItemName.Fold` folds `ü→ue`. The
   two disagree, and §8 resolves it by indexing both transliterations rather
   than by picking a winner.
2. **It does not split compounds.** Snowball is suffix-stripping over letter
   patterns, not a dictionary. `Hähnchenbrustfilet` is one lexeme forever.

Compound splitting is possible with an `ispell`-template dictionary over
Hunspell German files — PostgreSQL's dictionary documentation describes exactly
this, and the affix file's `compoundwords controlled` flag is what makes it
work ([PostgreSQL 18 §12.6](https://www.postgresql.org/docs/current/textsearch-dictionaries.html)).
It requires `de_DE.dic`/`de_DE.aff` placed in the database container's
`$SHAREDIR/tsearch_data` — PostgreSQL ships no Ispell files — which means a
custom `postgres` image, a documented upgrade path for it, and a new way for a
restore to come back subtly wrong. **Rejected**: §8 shows the trigram lane
already answers compounds at zero operational cost, and answers them in the
direction users actually search (`Hähnchen` → `Hähnchenbrustfilet`).

### 5.2 `pg_trgm`

**Already installed.** Three capabilities, and Culina currently uses one third
of one of them.

- `ILIKE '%x%'` accelerated by a GIN trigram index — what exists today.
- `similarity(a, b)` and the `%` operator — whole-string trigram overlap.
  Useless against a long document: `similarity('bolognese', <400 words>)`
  is ~0.02.
- **`word_similarity(a, b)`** and `<%` — the maximum similarity between `a` and
  any *word extent* of `b`. This is the correct function for "is this typo
  somewhere in this document", it is what a typo-tolerant lane must be built
  on, and it is the single most valuable thing in this section.

`set_limit()` / `pg_trgm.word_similarity_threshold` set the cutoff. 0.6 is the
operating point in §8 — high enough that `Bolgnese`→`Bolognese` (0.73) passes
and `Bolognese`→`Bologneser Wurst` does not get confused with unrelated words.

### 5.3 `unaccent`

**Already installed and entirely unused.** A filtering dictionary that strips
diacritics, usable both as a function (`unaccent('Bolognäse')` → `Bolognase`)
and as the first link in a text-search configuration chain. Folds `ä→a`,
`é→e`, `ñ→n`. Does not fold `ß→ss` (default rules) and does not expand
`ä→ae`.

### 5.4 Meilisearch

Rust, `MIT`-licensed core, memory-mapped LMDB storage, typo tolerance on by
default and well-tuned, prefix search built for search-as-you-type, filters,
facets, custom ranking rules, and a genuinely good developer experience. Its
memory-mapped store means the index is not capped by RAM
([comparison](https://www.meilisearch.com/docs/resources/comparisons/typesense)),
which is the friendliest property of any dedicated engine for small hardware.
German works out of the box; compound splitting does not (it is not a
dictionary-based analyser either), so `Hähnchen` → `Hähnchenbrustfilet` relies
on its prefix and typo machinery, which handles the *prefix* direction well and
the infix direction less so.

Cost: a second container, ~80–150 MB idle RSS, a second thing to back up, a
second thing to upgrade, an index that is eventually consistent with PostgreSQL
and therefore a new class of bug ("the recipe I just saved isn't findable"),
and a `compose.yaml` that no longer matches "one image serving both halves". If
Culina were a hosted product with a hundred thousand recipes this would be the
answer. It is not.

### 5.5 Typesense

C++, GPL-3.0, in-memory index. Typo tolerance is explicitly length-scaled (one
typo at five characters, two at nine), which is well-judged. Faceting and
filtering are strong.

The in-memory index is the disqualifier: RAM is the scarce resource on a home
server that is also running a media server and a DNS filter, and Typesense
wants the whole index resident. For 300 recipes the index is small, so the
floor is the process, not the data — but the process floor is still tens of
megabytes plus a container.

### 5.6 Elasticsearch / OpenSearch

The most capable option and the most obviously wrong one. A JVM with a 1–2 GB
heap floor, a cluster concept, index lifecycle management, and an operational
surface larger than the rest of Culina combined. Elasticsearch's SSPL/Elastic
licence would also need weighing against Culina's AGPL-3.0. Named only so that
its absence is a decision.

### 5.7 Lucene.NET / Tantivy-based (`lnx`, Quickwit, `tantivy-py`)

Lucene.NET would put a real BM25 engine **in process**, which is genuinely
attractive: no second container, no consistency gap, German analysers
(`GermanAnalyzer`, `GermanNormalizationFilter` — which folds `ä→a` *and*
handles `ß`) and a decomposition filter (`DictionaryCompoundWordTokenFilter`)
that would solve §4.E properly.

Against it: Lucene.NET 4.8 has been in beta for the better part of a decade and
the German analysis package's status on .NET 10 would need verifying; the index
is a directory on disk, which means a **third volume**, a backup story, and a
way for the index and the database to disagree after a restore — the exact
problem `docs/operations.md` exists to prevent. Tantivy is Rust, so it means
either a sidecar process or P/Invoke against a native library that must be
built for every architecture the image supports.

The decisive argument is subtler: adopting Lucene means adopting *its* ranking
model, and §11's tier system — the thing that makes the ranking explainable —
would be fighting BM25 rather than built on it. In PostgreSQL the ranking is
SQL Culina wrote and can test.

### 5.8 Client-side search (`FlexSearch`, `MiniSearch`, `Fuse.js`, `Orama`)

Tempting for a corpus this small: ship every recipe title to the browser and
search locally at zero latency, with no server round trip at all. `MiniSearch`
is ~15 kB gzipped and supports prefix and fuzzy search.

Three problems. The JavaScript budget has ~42 kB of headroom and
`culina-v2-xcs` already wants it back. The index has to be built on the client
and kept current across household members' edits. And, decisively, **the same
retrieval must serve the meal-plan picker, related recipes, duplicate detection
at import and smart cookbook counts** — all of which are server-side questions.
A client-only implementation is a second implementation, which §3 R11 forbids.

Client-side *ranking of an already-returned page* is a different matter and is
used in §18 for suggestion ordering.

### 5.9 Vector search: `pgvector`, Qdrant, sqlite-vec

`pgvector` is the only one worth considering, because it keeps everything in
one database. `apk add postgresql-pgvector` works on Alpine, or a pre-built
`pgvector/pgvector:pg18` image exists
([pgvector](https://github.com/pgvector/pgvector)) — but either way Culina's
documented `postgres:18-alpine` becomes a Culina-maintained image, which is a
real change to `docs/operations.md` and to every existing installation's
upgrade path.

At 2,000 × 384 dimensions the vectors are ~3 MB. No index is needed at all —
a sequential scan over 2,000 rows with a `<=>` operator is sub-millisecond.
So the vector *storage* is free. The cost is entirely in §5.10.

### 5.10 Local embedding models in .NET

The realistic path is ONNX Runtime (`Microsoft.ML.OnnxRuntime`) plus
`Microsoft.ML.Tokenizers`, running a multilingual sentence encoder in-process.

| Model | Dim | ONNX fp32 | int8 | Notes |
| --- | --- | --- | --- | --- |
| `multilingual-e5-small` | 384 | ~470 MB | ~120 MB | 94 languages, strong DE ([HF](https://huggingface.co/intfloat/multilingual-e5-small)) |
| `paraphrase-multilingual-MiniLM-L12-v2` | 384 | ~470 MB | ~120 MB | older, weaker on retrieval |
| `bge-m3` | 1024 | ~2.2 GB | ~570 MB | excellent, far too large |

Plus ~15–20 MB of ONNX Runtime native libraries per platform, per architecture.

Practical numbers on the §20 reference hardware (4-core x86 mini-PC, no GPU):
query encode 15–40 ms single-threaded for a short query, corpus encode of 2,000
recipes 60–120 s once, resident memory +200–300 MB with the session loaded and
+60–90 MB if lazily loaded and evicted. Image growth: **+120 MB at int8**, on a
base image currently in the low hundreds.

`ElBruno.LocalEmbeddings` shows the .NET integration pattern via
`Microsoft.Extensions.AI`, so the engineering is well-trodden. The cost is not
the code.

### 5.11 Cross-encoder reranking

`mmarco-mMiniLMv2-L12-H384-v1` or similar, run over the top *k* candidates.
Quality is genuinely better than bi-encoder similarity — cross-encoders see the
query and document together.

Cost: it is *k* forward passes, not one. 30–80 ms each on CPU for a 12-layer
model. A 25-candidate rerank is 0.75–2 s, which is ten to thirty times the
entire latency budget. Rejected on arithmetic.

### 5.12 Local LLMs for query understanding

A 1–3 B parameter instruct model via `llama.cpp` or ONNX, asked to turn
`vegetarisches Abendessen unter 30 Minuten` into JSON.

- **Latency**: 200 ms–2 s to first token on CPU. The search-as-you-type budget
  is 40 ms.
- **Availability**: search must not stop working because a model process died.
- **Determinism**: the interface *shows the user what it parsed*. A parse that
  varies run to run makes that display a lie.
- **Size**: +1–2 GB image, +1–2 GB RSS.
- **Product**: the assistant Culina has is optional and off by default, so
  search cannot be built on the assumption that one is connected.

§7 shows that ~40 grammar rules per language cover the query classes that
actually occur, deterministically, in under a millisecond. §7.6 designs the
optional LLM path anyway, in the only shape that is defensible: asynchronous,
additive, and incapable of changing a result set on its own.

### 5.13 NLP libraries for .NET

- **`Snowball` stemmers** — available as a NuGet port, but PostgreSQL already
  has them and applying a stemmer in two places is how two stemmers drift.
  Rejected for content; the *query* is stemmed by PostgreSQL too, via
  `websearch_to_tsquery` against the same configuration.
- **Compound splitters** (`JWordSplitter`-style, dictionary-driven) — a German
  compound splitter needs a German dictionary, which is curated data nobody
  maintains. §12's ~350-entry culinary lexicon is a bounded, reviewable version
  of the same idea restricted to food, which is where it actually matters.
- **SymSpell** — excellent spelling correction, and a reasonable NuGet exists.
  Worth knowing about, but §7.4 corrects against the household's own vocabulary
  using the trigram index already present, which needs no dictionary and no new
  dependency and is strictly better: you can only usefully search for recipes
  you have.

### 5.14 What is already in the codebase

Three assets that a naive design would rebuild.

- **`ItemName.Fold`** (`Domain/Shopping`) — folds case, `ä→ae`, `ö→oe`,
  `ü→ue`, `ß→ss`, and strips the accents German and English borrow. Written
  because "Müsli" and "Muesli" are the same thing in a trolley. It is the same
  thing in a search box.
- **`SectionKeywords`** (`Domain/Shopping`) — ~250 bilingual food keywords
  mapped to shop sections, longest-first, matched as substrings against a folded
  name. Its `MeatFish` group is, as it stands, a working vegetarian detector.
- **`CommonIngredients`** (`Domain/Recipes`) — ~120 DE/EN ingredient pairs with
  sections, deliberately short, already serving autocomplete.

Together these are most of a bilingual culinary lexicon, written by the people
who will maintain it, in the language the domain already speaks. §12 extends
this pattern rather than importing an ontology.

---

## 6. Comparison matrix

Scored for **this** application: 2–8 users, 50–2,000 recipes, one container,
one home server. A different deployment would score these differently, and
several of the low scorers are better products.

Legend: ●●●●● excellent · ●●●●○ good · ●●●○○ adequate · ●●○○○ poor · ●○○○○ unusable

| | PG FTS + trgm + app lexicon | Meilisearch | Typesense | Elastic / OpenSearch | Lucene.NET in-proc | PG + pgvector + local model | Client-side (MiniSearch) |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Exact / known-item quality | ●●●●● | ●●●●● | ●●●●● | ●●●●● | ●●●●● | ●●●○○ | ●●●●○ |
| Typo tolerance | ●●●●○ | ●●●●● | ●●●●● | ●●●●○ | ●●●●○ | ●●●○○ | ●●●○○ |
| German morphology | ●●●●○ | ●●●○○ | ●●●○○ | ●●●●○ | ●●●●● | ●●●●○ | ●●○○○ |
| German compounds | ●●●●○ trigram | ●●●○○ prefix only | ●●●○○ | ●●●●○ decompounder | ●●●●● decompounder | ●●●●○ | ●●○○○ |
| Cross-language DE↔EN | ●●●●○ lexicon | ●●○○○ | ●●○○○ | ●●●○○ | ●●●○○ | ●●●●● | ●○○○○ |
| Semantic / vague queries | ●●●○○ concepts | ●●●○○ | ●●○○○ | ●●●○○ | ●●○○○ | ●●●●● | ●○○○○ |
| Filtering + faceting | ●●●●● SQL | ●●●●● | ●●●●● | ●●●●● | ●●●○○ | ●●●●● | ●●●○○ |
| Ranking customisation | ●●●●● own SQL | ●●●●○ rules | ●●●●○ | ●●●●● | ●●●●○ | ●●●○○ | ●●○○○ |
| **Explainability** | ●●●●● | ●●●○○ | ●●●○○ | ●●●●○ | ●●●○○ | ●●○○○ | ●●●○○ |
| Consistency with the DB | ●●●●● same txn | ●●○○○ eventual | ●●○○○ | ●●○○○ | ●●●○○ | ●●●●● | ●●○○○ |
| Added RSS | **~4 MB** | 80–150 MB | 60–200 MB | 1–2 GB | 50–150 MB | 200–300 MB | 0 server |
| Added image size | 0 | second container | second container | second container | +20 MB | **+120–470 MB** | +15 kB JS |
| New volumes / backup artefacts | **0** | 1 | 1 | 1 | 1 | 0 | 0 |
| Deployment change | **none** | compose + proxy | compose | compose + tuning | volume | **new DB image** | none |
| Maintenance burden | low | medium | medium | high | medium | medium-high | low |
| Licence | PostgreSQL | MIT | GPL-3.0 | SSPL / Elastic | Apache-2.0 | MIT + model | MIT |
| Latency, 300 docs | **10–40 ms** | 2–10 ms | 1–5 ms | 5–20 ms | 1–5 ms | 30–80 ms | < 1 ms |
| Integration effort | medium | medium | medium | high | high | high | low |
| **Fit for 2–8 users** | **●●●●●** | ●●●○○ | ●●●○○ | ●○○○○ | ●●●○○ | ●●○○○ | ●●○○○ |

Three observations the matrix makes plain.

**Latency is not the differentiator.** Every serious option answers in under
100 ms at this corpus size. Meilisearch's 5 ms and PostgreSQL's 40 ms are the
same number once a 20 ms network hop and a 16 ms frame are added. Choosing on
latency here is choosing on a difference no user can perceive.

**The dedicated engines' real advantage is typo tolerance, and it is 1.0 points
in one row.** Everything else they win is a scale property. PostgreSQL's
`word_similarity` closes most of that gap for free.

**The cost column is where the decision is made.** "Added RSS ~4 MB, zero new
volumes, no deployment change" against "second container, second backup, second
upgrade path" is not close for an app whose README leads with being one image.

---

## 7. Query understanding

### 7.1 The pipeline, and which stages earn their place

The canonical search pipeline has thirteen stages. Culina needs seven of them,
and saying why the other six are absent is as much of the design as the seven.

```
raw query
    │
    ├─▶ 1. NORMALISE             ✔ fold, trim, collapse, strip punctuation
    ├─▶ 2. LANGUAGE DETECTION    ✘ not on the query — see below
    ├─▶ 3. CARRIER STRIPPING     ✔ "was kann ich mit X machen?" → "X"
    ├─▶ 4. CONSTRAINT EXTRACTION ✔ time, diet, meal, cuisine, method, negation
    ├─▶ 5. ENTITY RECOGNITION    ✔ ingredient / tag / dish, dictionary-driven
    ├─▶ 6. SPELL CORRECTION      ✔ but deferred — only on zero results
    ├─▶ 7. SYNONYM EXPANSION     ✔ into a separate lane, not into the query
    ├─▶ 8. CANDIDATE RETRIEVAL   ✔ four lanes, §8
    ├─▶ 9. LEXICAL SCORING       ✔ §11
    ├─▶ 10. SEMANTIC SCORING     ✘ §9
    ├─▶ 11. METADATA SCORING     ✔ §11
    ├─▶ 12. PERSONALISATION      ✔ tie-break only, §13
    ├─▶ 13. RERANKING            ✘ §11.6
    └─▶ 14. DIVERSIFICATION      ✘ see below
```

**No query language detection.** This is the stage most designs get wrong. A
one-to-three-word query carries almost no signal — `Pasta`, `Curry`, `Toast`
and `Pizza` are in both languages, and `Butter` is spelled identically. Guessing
wrong picks the wrong stemmer and silently halves recall. Culina does not need
to guess: **every query is run against both configurations and both halves of
the lexicon**, because the corpus is mixed anyway and the cost of the second
`tsquery` is a few microseconds. The *documents* are analysed in their own
declared language, which is where the information actually is.

**Spell correction is deferred, not pre-emptive.** Correcting `Gemuese` to
`Gemüse` before searching would be wrong in a library that spells it `Gemuese`.
The rule is: search what was typed; if that returns nothing, *then* correct
against the household's own vocabulary and search again, saying so. This makes
correction free in the common case and always honest.

**No diversification.** MMR and its relatives exist to stop ten near-identical
documents filling a page. A household with three chicken curries wants to see
all three. Suppressing one because it resembles another is the system deciding
it knows better, on a page of eight results.

### 7.2 Normalisation

One function, in `Domain`, next to the `ItemName.Fold` it is modelled on.
**Everything that is indexed and everything that is queried goes through it**,
which is the only way the two can agree.

```csharp
// Domain/Search/SearchText.cs
public static class SearchText
{
    /// <summary>
    /// The form two pieces of text are compared in, in both of the
    /// transliterations German is written with.
    /// </summary>
    /// <remarks>
    /// "Müsli", "Muesli" and "Musli" are one word to a person and three to a
    /// computer. ItemName.Fold already answers this for a shopping list, and
    /// answers it with ü → ue, because that is the spelling a German keyboard
    /// falls back to. PostgreSQL's unaccent answers it with ü → u, because
    /// that is what stripping a diacritic means. Neither is wrong and they do
    /// not meet, so both are emitted and the index carries both.
    /// </remarks>
    public static FoldedText Fold(string value);   // → { Umlaut: "muesli", Stripped: "musli" }

    /// <summary>Content words, in both folds, stopwords removed.</summary>
    public static IReadOnlyList<string> Tokenize(string value);

    /// <summary>
    /// Everything about a recipe that the fuzzy lane looks inside: title,
    /// tags, ingredient names, description and step text, every word in both
    /// folds, space-joined.
    /// </summary>
    public static string FuzzyCorpus(Recipe recipe);
}
```

The fold also drops `%`, `_` and `\` along with the rest of the punctuation.
That is a footnote about spelling and a load-bearing fact about safety: lanes 0
and 3 build `LIKE` patterns by concatenation, so a query containing a
metacharacter would otherwise be a wildcard the user did not ask for — `%%`
matching the whole library, `_` matching any letter. Escaping at each call site
is the version of this that gets forgotten once; removing them in the one
function every query already passes through is the version that cannot be.

Concretely, for `Hähnchenbrust in Zitronensoße`:

| | |
| --- | --- |
| Umlaut fold | `haehnchenbrust in zitronensosse` |
| Stripped fold | `hahnchenbrust in zitronensosse` |
| Tokens | `haehnchenbrust`, `hahnchenbrust`, `zitronensosse` |

`in` is dropped as a stopword. The two folds happen to coincide for `ß→ss`,
which both do, so most words yield one token and only umlaut words yield two.
Index growth from the duplicate is measured in §20 and is about 18 %.

### 7.3 The grammar

A deterministic, ordered rule set. Roughly 40 patterns per language, expressed
as data rather than code, matched against the token stream left-to-right,
longest-match-first. Every rule that fires **consumes** its tokens; what is left
at the end is the free-text residual.

```csharp
// Application/Search/QueryGrammar.cs — shape, not the full table
private static readonly Rule[] Rules =
[
    // ── Time. Ordered before everything, because a number is unambiguous. ──
    Rule.Time(@"(?:unter|weniger als|höchstens|max\.?|bis zu)\s+(\d+)\s*(min|minuten)",
              m => Minutes(m, 1)),
    Rule.Time(@"(?:unter|weniger als|höchstens)\s+(\d+)\s*(std|stunden?)",
              m => Minutes(m, 60)),
    Rule.Time(@"(?:under|less than|at most|within)\s+(\d+)\s*(min|minutes?)", …),
    Rule.Time(@"^(\d+)\s*(min|minuten|minutes?)$", …),            // bare "30 min"

    // ── Effort. A PREFERENCE, not a filter: see the note below. ──
    Rule.Soft(Signal.Quick, "schnell", "schnelles", "fix", "quick", "fast",
                            "einfach", "easy", "wenig aufwand", "simple"),

    // ── Diet. A HARD constraint, and the only kind that can exclude. ──
    Rule.Diet(Diet.Vegetarian, "vegetarisch", "vegetarisches", "veggie",
                               "vegetarian", "ohne fleisch", "fleischlos",
                               "meat free", "meatless", "no meat"),
    Rule.Diet(Diet.Vegan,      "vegan", "vegane", "veganes", "plant based"),

    // ── Meal. ──
    Rule.Meal(Meal.Breakfast, "frühstück", "breakfast", "brunch", "morgens"),
    Rule.Meal(Meal.Lunch,     "mittagessen", "mittag", "lunch"),
    Rule.Meal(Meal.Dinner,    "abendessen", "abendbrot", "dinner", "supper", "abends"),
    Rule.Meal(Meal.Dessert,   "nachtisch", "dessert", "nachspeise", "süßspeise"),
    Rule.Meal(Meal.Snack,     "snack", "zwischendurch", "fingerfood"),

    // ── Cuisine. ──
    Rule.Cuisine("italian", "italienisch", "italienische", "italian"),
    Rule.Cuisine("asian",   "asiatisch", "asiatische", "asian"),
    // … ~14 cuisines

    // ── Negation. Must run before ingredient recognition claims the noun. ──
    Rule.Exclude(@"ohne\s+(\w+)"),
    Rule.Exclude(@"without\s+(\w+)"),
    Rule.Exclude(@"\-(\w+)"),

    // ── Carriers: strip the sentence, keep the food. ──
    Rule.Carrier(@"^was kann ich (?:heute |noch )?mit (.+?) (?:machen|kochen|zubereiten)\??$"),
    Rule.Carrier(@"^(?:etwas|was|irgendwas) mit (.+)$"),
    Rule.Carrier(@"^(?:ein |das )?rezept (?:für|mit) (.+)$"),
    Rule.Carrier(@"^(?:ich (?:suche|will|möchte|brauche)) (?:ein |etwas )?(.+)$"),
    Rule.Carrier(@"^what can i (?:make|cook) with (.+?)\??$"),
    Rule.Carrier(@"^(?:something|anything) with (.+)$"),
    Rule.Carrier(@"^(?:a )?recipe (?:for|with) (.+)$"),
];
```

Three rules about the rules.

**Time is a filter; effort is a preference.** `unter 30 Minuten` states a
number and means it — `maxMinutes = 30`, excluding recipes with no stated time,
exactly as [`RecipeSearcher`](../src/backend/src/Infrastructure/Persistence/Recipes/RecipeSearcher.cs)
already does, and for the reason its comment already gives. `schnell` states an
intent. Turning `schnell` into `maxMinutes = 30` would silently delete the
25-minute recipe with no time recorded, and the user would never learn why. It
becomes a **ranking boost** instead, and the chip reads `Schnell` rather than
`< 30 Min.`, because those are different promises.

**Negation runs before entity recognition.** `Gericht ohne Fleisch` must not
produce "ingredient preference: Fleisch". Ordered rules, first match wins,
consumption is destructive.

**Under-parsing beats over-parsing.** `Nudeln mit Tomatensoße` is *not* an
ingredient query — `mit` joins two food nouns in a dish name. The carrier rules
are anchored (`^…$`) and require an explicit carrier verb or pronoun precisely
so that this sentence falls through to plain free text, which is the right
answer. Every carrier rule is anchored; none is a bare `mit`.

### 7.4 Worked example

```
Input:  "vegetarisches Abendessen unter 30 Minuten mit Kartoffeln"

 1. normalise   → vegetarisches abendessen unter 30 minuten mit kartoffeln
 2. rules, longest-first, left-to-right
    ├ Time     "unter 30 minuten"     → maxMinutes = 30           [consumed]
    ├ Diet     "vegetarisches"        → diet = vegetarian  (hard) [consumed]
    ├ Meal     "abendessen"           → meal = dinner             [consumed]
    └ residual "mit kartoffeln"
 3. residual, ingredient pass
    ├ "mit" is a stopword                                         [dropped]
    └ "kartoffeln" ∈ lexicon as ingredient `potato`
      → preferredIngredients = [kartoffel]                        [consumed]
 4. free text  → (empty)

Result
  filters      { maxMinutes: 30, diet: [vegetarian], meal: [dinner] }
  prefer       { ingredients: [kartoffel] }
  freeText     ""
  interpretation (shown as chips, each removable)
      [Vegetarisch ×] [Abendessen ×] [< 30 Min. ×] [mit Kartoffeln ×]
```

and a second, which deliberately parses to almost nothing:

```
Input:  "Nudeln mit Tomatensoße"

 1. normalise   → nudeln mit tomatensosse
 2. rules       → no carrier rule anchors; no time, diet, meal or cuisine
 3. residual    → "nudeln mit tomatensosse"
 4. free text   → "nudeln tomatensosse"    ("mit" dropped as a stopword)

Result
  filters      { }
  freeText     "nudeln tomatensosse"
  concepts     nudel → { pasta, spaghetti, penne, noodle }   (lane 4 only)
               tomatensosse → { tomate, sugo, passata, tomato sauce }
  interpretation  (no chips — nothing was inferred, so nothing is claimed)
```

Showing no chips here is as important as showing four in the first example.
A system that invents an interpretation for every query teaches people to
distrust the ones it means.

### 7.5 Diet, honestly

`vegetarisch` is a hard constraint (§3 R6) over data Culina does not actually
have. There is no nutrition table and there will not be one. The honest
implementation is asymmetric, and the asymmetry must be visible in the design
rather than papered over:

```
EXCLUSION is reliable.
    A recipe whose folded ingredient names contain any keyword from the
    lexicon's `meat` family is NOT vegetarian. High precision: "Hackfleisch",
    "Speck", "Lachs" mean what they say.

INCLUSION is ranked, never asserted.
    tagged vegetarisch by the household   → certain      (tier and boost)
    no meat keyword found                 → presumed     (eligible, no boost)

The gap is named: a recipe using "Brühe", "Fischsauce", "Gelatine" or
"Parmesan" is presumed vegetarian and is not. The lexicon carries a
`hidden-animal` family for the worst of these, which downgrades presumption to
"not shown under a vegan filter". Beyond that, the fix is the household
tagging the recipe — and the UI asks: a presumed-vegetarian recipe opened from
a vegetarian search shows a quiet "Ist das vegetarisch? [Ja] [Nein]" that
writes a tag.
```

That last line is the part that makes this work. The system is wrong
occasionally, it knows which results it is unsure about, and it converts that
uncertainty into one tap that makes it permanently right. That is worth more
than any curated table, and it is the same principle
[`domain-model.md`](domain-model.md#ingredient-suggestions) already applies to
ingredient vocabulary: after a few recipes, the household's own words win.

### 7.6 If an LLM were ever added

It is not, for the reasons in §5.12. Should that change, the only defensible
shape is:

```
User types ──▶ parse (deterministic, < 1 ms) ──▶ retrieve ──▶ RESULTS ON SCREEN
                                                                    │
                        ┌───────────────────────────────────────────┘
                        │  (independently, best-effort, cancellable)
                        ▼
              local model, 200 ms–2 s
                        │
                        ▼
     a SUGGESTED refinement chip appears, greyed, not applied
     [＋ Vegetarisch?]   ← one tap applies it; ignoring it costs nothing
```

Constraints, all of them load-bearing: it never blocks a result; it never
changes a result set without a tap; it is off by default
(`Search__Assist__Enabled=false`); its failure or absence is invisible; and if
it is a remote model, `docs/configuration.md` states in plain words that the
query text leaves the server, and the setting is off unless an operator turns
it on knowing that.

---

## 8. Lexical search

The core of the system. Four lanes over one maintained document per recipe.

### 8.1 The document

One row per recipe in `recipe_search_documents`, written in the same
transaction as the recipe (§16). Four fields do the work:

| Field | Built from | Serves |
| --- | --- | --- |
| `document tsvector` | title (A), tags + concepts (B), ingredient names (C), description + steps (D) | lanes 1–2: stemming, morphology, weighted ranking |
| `fuzzy_text text` | every word above, both folds, space-joined | lane 3: typos and compounds |
| `title_ae`, `title_a` | the title in each fold, one per column | lane 0: exact and prefix |
| `concepts text[]` | lexicon expansion of title + tags + ingredients | lane 4: synonyms, cross-language, ontology |

The `tsvector` is built with the recipe's **own** language configuration:

```sql
insert into recipe_search_documents (recipe_id, household_id, language, document, …)
values (
    @recipeId, @householdId, @language,
      setweight(to_tsvector(@config::regconfig, @title),        'A')
   || setweight(to_tsvector(@config::regconfig, @tagsAndConcepts), 'B')
   || setweight(to_tsvector(@config::regconfig, @ingredientNames), 'C')
   || setweight(to_tsvector(@config::regconfig, @descriptionAndSteps), 'D'),
    …);
```

`@config` is `'culina_de'` or `'culina_en'`, chosen from a closed two-member
enum in C#, never from user input.

The configurations are created in a migration, and they are the entire fix for
§5.1's umlaut problem:

```sql
-- 0012_search.sql
create text search configuration culina_de (copy = german);
alter text search configuration culina_de
    alter mapping for asciiword, asciihword, hword_asciipart,
                      word, hword, hword_part, numword, numhword
    with unaccent, german_stem;

create text search configuration culina_en (copy = english);
alter text search configuration culina_en
    alter mapping for asciiword, asciihword, hword_asciipart,
                      word, hword, hword_part, numword, numhword
    with unaccent, english_stem;
```

`unaccent` before the stemmer means `Hähnchen` and `Hahnchen` produce the same
lexeme. `Haehnchen` still does not — which is what `fuzzy_text` is for, and why
it carries both folds.

**Why no synonym dictionary in this chain.** A `synonym` template dictionary
reading `culina_de.syn` would fold `huhn`, `haehnchen` and `chicken` to one
lexeme here, which is the textbook answer and is genuinely tempting. It is
rejected for the two reasons in §1: it needs files inside the *database*
container, and — decisively — once the dictionary has resolved them, `Huhn` and
`Hähnchen` are indistinguishable in the index, so ranking cannot prefer the
direct match. Culina expands in the application, into a **separate lane**, so
the ranker can see which branch fired. This is the difference between a search
that returns the right set and one that returns it in the right order.

### 8.2 The four lanes

```
                            query (parsed, §7)
                                    │
        ┌───────────────┬───────────┴──────────┬────────────────┐
        ▼               ▼                      ▼                ▼
  ┌───────────┐  ┌─────────────┐      ┌───────────────┐  ┌────────────┐
  │ 0 EXACT   │  │ 2 LEXICAL   │      │ 3 FUZZY       │  │ 4 CONCEPT  │
  │ title_ae/_a│ │ document @@ │      │ word_similar. │  │ concepts   │
  │ = / like  │  │ tsquery     │      │ + ILIKE infix │  │ && array   │
  │           │  │             │      │               │  │            │
  │ btree     │  │ GIN tsvector│      │ GIN trigram   │  │ GIN array  │
  │ ~0.2 ms   │  │ ~3 ms       │      │ ~12 ms        │  │ ~2 ms      │
  └─────┬─────┘  └──────┬──────┘      └───────┬───────┘  └─────┬──────┘
        │               │                     │                │
        │    solves     │    solves           │   solves       │  solves
        │  §4.A known   │  §4.B partial       │  §4.C typos    │ §4.F synonym
        │               │  §4.D morphology    │  §4.E compound │ cross-lang
        └───────────────┴──────────┬──────────┴────────────────┘
                                   ▼
                   candidate set  ∪  with per-lane evidence
                                   ▼
                     hard filters (diet, time, tags, cookbook)
                                   ▼
                         tier + score  (§11)
```

Lane 3 is the expensive one and runs **conditionally**: it is skipped entirely
when lanes 0–2 already produced enough candidates. §20 has the arithmetic.

### 8.3 The query, in full

```sql
with q as (
    select
        @householdId::uuid              as household_id,
        @freeText::text                 as raw,          -- residual free text
        @tsqueryDe::tsquery             as tsq_de,
        @tsqueryEn::tsquery             as tsq_en,
        @terms::text[]                  as terms,        -- folded, both variants
        @concepts::text[]               as concepts,     -- lexicon expansion
        @titleAe::text                  as title_ae,     -- query, ä → ae
        @titleA::text                   as title_a       -- query, ä → a
),
-- ── lane 0: exact and prefix on the title ──────────────────────────────────
-- Both folds, matched pairwise rather than crosswise: a title stored as
-- ('kaesekuchen', 'kasekuchen') is reached by a query folded either way, and
-- neither column has to contain the other's spelling. q.title_ae and
-- q.title_a have already had LIKE's metacharacters removed by the fold, which
-- is why they can be concatenated into a pattern.
exact as (
    select d.recipe_id,
           case when d.title_ae = q.title_ae or d.title_a = q.title_a then 1.00
                when d.title_ae like q.title_ae || '%'
                  or d.title_a  like q.title_a  || '%'                then 0.80
                else 0.60 end as score          -- word-boundary, mid-title
    from recipe_search_documents d, q
    where d.household_id = q.household_id
      and (d.title_ae = q.title_ae         or d.title_a = q.title_a
        or d.title_ae like q.title_ae || '%'
        or d.title_a  like q.title_a  || '%'
        or d.title_ae like '% ' || q.title_ae || '%'
        or d.title_a  like '% ' || q.title_a  || '%')
),
-- ── lane 2: weighted full text, in the document's own language ─────────────
lexical as (
    select d.recipe_id,
           ts_rank_cd('{0.1, 0.3, 0.6, 1.0}'::float4[],   -- D, C, B, A
                      d.document,
                      case when d.language = 'de' then q.tsq_de else q.tsq_en end,
                      2|4)                                 -- length-normalised
               as score,
           -- Whether the TITLE band matched, which drives the tier and not
           -- only the score. Asked by ranking the same vector with every
           -- weight but A set to zero: non-zero means a lexeme landed in the
           -- title. There is no other way to ask a tsvector where it matched.
           ts_rank_cd('{0, 0, 0, 1}'::float4[],
                      d.document,
                      case when d.language = 'de' then q.tsq_de else q.tsq_en end,
                      2|4) > 0 as title_hit
    from recipe_search_documents d, q
    where d.household_id = q.household_id
      and d.document @@ (case when d.language = 'de' then q.tsq_de else q.tsq_en end)
),
-- ── lane 3: typo + compound, one row per query term ────────────────────────
fuzzy as (
    select d.recipe_id,
           max(case
                 when d.fuzzy_text like '%' || t || '%'         then 0.85
                 else word_similarity(t, d.fuzzy_text)
               end) as score,
           -- distinct, not count(*): the fold emits two transliterations per
           -- word and they coincide for every word without an umlaut, so
           -- count(*) would score 'Tomaten' twice and 'Hähnchen' once.
           count(distinct t) filter (
               where d.fuzzy_text like '%' || t || '%'
                  or word_similarity(t, d.fuzzy_text) >= 0.60) as matched_terms
    from recipe_search_documents d, q, unnest(q.terms) as t
    where d.household_id = q.household_id
      and (d.fuzzy_text like '%' || t || '%' or t <% d.fuzzy_text)
    group by d.recipe_id
),
-- ── lane 4: concepts ───────────────────────────────────────────────────────
concept as (
    select d.recipe_id,
           cardinality(array(select unnest(d.concepts) intersect select unnest(q.concepts)))
             ::float / greatest(cardinality(q.concepts), 1) as score
    from recipe_search_documents d, q
    where d.household_id = q.household_id
      and d.concepts && q.concepts
)
select … from candidates   -- full outer join of the four, then §11
```

`t <% d.fuzzy_text` is the indexable form of `word_similarity(t, fuzzy_text) >
threshold`, and it is what lets the GIN trigram index serve lane 3 rather than
forcing a sequential scan. `set_limit` / `pg_trgm.word_similarity_threshold` is
set per session to 0.60.

### 8.4 Why the lanes are where they are

**Lane 3 is what makes German work, and it is the lane a "modern" rewrite would
delete.** Worth being explicit about the numbers.

| Query | Document | FTS (lane 2) | Trigram (lane 3) |
| --- | --- | --- | --- |
| `Hähnchen` | `Hähnchenbrustfilet mit Reis` | ✗ different lexemes | ✓ substring, 0.85 |
| `Kartoffel` | `Süßkartoffelcurry` | ✗ | ✓ substring, 0.85 |
| `Nudel` | `Nudelauflauf` | ✗ | ✓ substring, 0.85 |
| `Bolgnese` | `Spaghetti Bolognese` | ✗ | ✓ `word_similarity` 0.73 |
| `Bolognäse` | `Spaghetti Bolognese` | ✓ (`unaccent`) | ✓ |
| `Tomate` | `Tomaten, passiert` | ✓ (stemmer) | ✓ |
| `Tomaten` | `eine Tomate` | ✓ (stemmer) | ✗ substring is directional |
| `gebackene` | `im Ofen backen` | ✓ (stemmer) | ✗ |

Neither lane subsumes the other. FTS owns morphology in both directions;
trigram owns compounds and typos. The current implementation has only the
weaker half of trigram (substring, no similarity) and no FTS at all.

**Lane 3's cost is why it is conditional.** A GIN trigram scan over
`fuzzy_text` for a household of 2,000 recipes is 8–20 ms, an order of magnitude
above the others. The rule:

```
run lanes 0, 2 and 4 always.
run lane 3 when
      lanes 0 ∪ 2 ∪ 4 produced fewer than (limit × 2) candidates
   or no candidate reached tier B
   or any query term is ≥ 4 characters and matched nothing at all
```

In the majority case — a known-item query that lane 0 or 2 answers — lane 3
never runs and search costs ~6 ms.

### 8.5 Building the `tsquery`

`websearch_to_tsquery` is the only safe parser to hand a user string to. It
never throws on `((` or `&|`, and it gives users quoting and `-exclusion` for
free.

```csharp
// one per configuration; both are always built, per §7.1
var de = "websearch_to_tsquery('culina_de', @freeText)";
var en = "websearch_to_tsquery('culina_en', @freeText)";
```

Multi-term queries are **AND by default** — `Spaghetti Bolognese` must not
return every recipe containing `Spaghetti`. When the AND query returns fewer
than three candidates the query is retried as OR, and §22 says so in the
interface (`Alle Wörter: 0 · Einige Wörter: 6`). Silent OR-fallback is how a
search box starts returning things nobody asked for.

---

## 9. Semantic search

The section this document could have been written to justify, and does not.

### 9.1 Where embeddings would genuinely help

Two query classes, honestly:

- **§4.J, vague/mood** — `warme Mahlzeit`, `etwas leichtes für den Sommer`,
  `comfort food`. A sentence encoder puts `Kürbissuppe` near `warme Mahlzeit`
  with no lexicon entry at all.
- **"more like this"** — given `Spaghetti Bolognese`, find its neighbours.
  Cosine over recipe vectors is a good answer to a question tags answer poorly.

And one class where they would help less than they appear to:

- **§4.F, cross-language** — a multilingual encoder does put `chicken` near
  `Hähnchen`. But so does one line in a lexicon, deterministically, in 200 ns,
  with an explanation the UI can show. Culinary head nouns are a closed set of
  a few hundred words. This is the rare case where the dictionary is not the
  poor man's embedding; it is the better tool.

### 9.2 The structural argument against

Not a cost argument. A cost argument would be answerable with a bigger server.

> **A household recipe library is a corpus where the user has seen every
> document.**

Fifty to five hundred recipes, every one of them written or deliberately
imported by the same four people who search them. When someone types
`Bolognese`, they are not exploring a corpus — they are *retrieving a memory*.
They know the recipe exists; they know roughly what it is called; the search
box is a shortcut past scrolling.

Dense retrieval's advantage is over corpora too large to enumerate, where the
user cannot name what they want because they have never seen it. That
advantage is worth a great deal at 10⁶ documents. At 10² it is worth
approximately what §4's distribution says: about 1 % of queries.

Against that 1 %, dense retrieval brings a liability that lands on the other
99 %: **it always returns something, and it is confident about it.** Cosine
similarity has no zero. A query for a recipe the household does not have gets
five plausible-looking neighbours instead of an honest "you have no Schnitzel —
shall we import one?", which §22 argues is the more useful answer and which §19
shows Culina is uniquely well placed to give.

### 9.3 What the 1 % gets instead

`warme Mahlzeit` is not unanswerable without a neural network. It decomposes
into things already written down:

| Query | Lexicon concepts | Becomes |
| --- | --- | --- |
| `warme Mahlzeit` | `warm` | not `salat`, not `dessert`; prefer recipes with `cook_minutes` |
| `Sommergericht` | `summer` | household tags `salat`/`grillen`/`kalt`; prefer low `cook_minutes` |
| `Winteressen` | `winter` | household tags `eintopf`/`auflauf`/`suppe`/`ofen` |
| `comfort food` | `comfort` | `auflauf`, `pasta`, `kartoffel`, `käse`, `suppe` |
| `wenig Aufwand` | `easy` | `maxMinutes ≈ 30` boost + few ingredients + few steps |
| `leicht` | `light` | not `frittiert`, not `sahne`; prefer `salat`, `gemüse`, `fisch` |

Twelve concepts, each three or four lines of lexicon. Each maps to **the
household's own tags first** and to ingredient families second, so a household
that tags `Sommer` gets a perfect answer and one that does not gets a decent
one. Neither gets a confident wrong one, because §22's ladder makes the weak
match say it is weak.

### 9.4 The gate

This is a falsifiable position, so here is what would falsify it. After phase 2,
against the §23 golden set:

```
IF   NDCG@5 over the "vague" section      < 0.50
AND  that section's real-world share      > 3 %   (measured, not assumed)
AND  the concept lexicon has been extended once and still fails
THEN reopen, with the design in §9.5.
```

The prediction, recorded so that it can be checked: the first condition may
well hold — vague queries are hard — and the **second will not**. Vague queries
are rare in a corpus you wrote yourself.

### 9.5 The design, if the gate ever opens

Not "add embeddings". **Query-conditional** embeddings, which is the only shape
proportionate to a 1 % query class:

```
parse ──▶ is this query VAGUE?
            │  vague ⇔ no structured constraint extracted
            │        ∧ no candidate reached tier C in the lexical lanes
            │        ∧ query length ≥ 2 tokens
            │
      no ───┴──▶ lexical only. 99 % of queries. unchanged. ~6–40 ms.
            │
      yes ──────▶ encode query (multilingual-e5-small int8, 15–40 ms)
                  cosine over recipe_embeddings (2,000 × 384, seq scan, < 1 ms)
                  RRF(lexical_composite, dense) with k = 20
                       ← small k, because at 300 documents rank differences
                         are meaningful and k = 60 flattens them
                  results marked "Ähnliche Rezepte", visibly a different answer
```

Everything about it is opt-in and reversible: `Search__Semantic__Enabled=false`
by default; the model is a separate image tag (`culina:latest-semantic`) so the
default image does not grow 120 MB; the embedding table is additive, so
disabling it changes nothing else; and if the model fails to load the setting
turns itself off and logs once.

This is also the only place RRF belongs, for the reason §10 gives.

---

## 10. Hybrid search and fusion

### 10.1 Culina is a hybrid — of the three kinds that matter here

The word "hybrid" usually means lexical + dense. Culina is a hybrid of
**lexical, fuzzy and structured**, which is a different and, for this corpus, a
more useful combination.

```
   ┌─────────────────┐   ┌──────────────────┐   ┌─────────────────────┐
   │ LEXICAL         │   │ FUZZY / SUBSTRING│   │ STRUCTURED          │
   │ tsvector + rank │   │ trigram          │   │ SQL predicates      │
   │                 │   │                  │   │                     │
   │ exact names     │   │ typos            │   │ time ceiling        │
   │ inflections     │   │ compounds        │   │ diet                │
   │ rare terms      │   │ partial words    │   │ tags, cookbook      │
   │ predictable     │   │ forgiving        │   │ ingredients on hand │
   └────────┬────────┘   └────────┬─────────┘   └──────────┬──────────┘
            │                     │                        │
            │                ┌────┴────┐                   │
            │                │ CONCEPT │                   │
            │                │ lexicon │                   │
            │                └────┬────┘                   │
            └────────────────┬────┴────────────────────────┘
                             ▼
                    FILTER, then TIER, then SCORE
```

Structured is not a peer of the other two. **It is a gate.** Filters do not
contribute score; they decide eligibility. A vegetarian filter does not make
meat recipes rank lower — it removes them. This is §3 R6, and it is the
distinction that stops semantic similarity overruling a stated constraint.

### 10.2 Why not Reciprocal Rank Fusion

RRF is the default recommendation for hybrid retrieval and it is right far more
often than not. It is wrong here, and the reason is specific.

```
RRF:   score(d) = Σ  1 / (k + rank_i(d))
                  i
```

Its virtues are exactly its problem in this system.

- **It discards score magnitude.** That is what makes it work across rankers
  whose scores are incomparable. But a lane-0 exact title match is not a score
  that needs normalising — it is a *fact*, and the correct behaviour is to put
  that recipe first, full stop.
- **It treats lanes as equal voters.** Three weak lanes at rank 1 outvote one
  strong lane at rank 1. For `Bolognese`, a recipe that is fuzzy-rank-1,
  concept-rank-1 and lexical-rank-3 would beat the actual `Bolognese`, which is
  exactly §3 R7's forbidden outcome.
- **Its lanes are supposed to be independent.** Culina's are not, by
  construction: lane 3 matches nearly everything lane 2 matches, plus more.
  Fusing correlated rankers, as the retrieval literature notes, buys little and
  distorts much.

RRF's home is fusing a *lexical composite* with a *dense retriever* — two
genuinely different views of relevance. That is precisely where §9.5 puts it,
with `k = 20` rather than the conventional 60, because at a few hundred
documents rank differences carry real information and a large `k` flattens
them.

### 10.3 What is used instead: tier, then bounded score

```
for each candidate d:
    tier(d)  = the strongest evidence class that fired        (discrete, 0–4)
    score(d) = Σ wᵢ · signalᵢ(d)                              (continuous, 0–1)

order by  tier asc, score desc, personalisation desc, updated_at desc, id desc
```

Three properties follow, and all three are requirements rather than
conveniences.

**Explainable.** Every result's position is a sentence: *"tier B, because the
stem of your query is in its title; score 0.71, mostly title coverage."* The
UI can show a short version of that sentence and be telling the truth.

**Predictable.** Adding a recipe cannot reorder unrelated results, because
nothing is normalised across the result set. `ts_rank_cd` is per-document.
There is no corpus-wide IDF to shift when a recipe is saved. In a library where
somebody adds a recipe every few days, a ranking that visibly reshuffles on
every write is a ranking people stop trusting.

**Testable.** Tier is an integer. An integration test asserts `tier == B`, not
`score > 0.62`, and does not become flaky when a weight is tuned. §24 leans on
this heavily.

### 10.4 Weighted sum, or learned?

Learned ranking needs labels. Eight users at perhaps five searches a day each
produce ~300 implicit judgements a month, most of them for the same twenty
recipes, all of them confounded by position bias. A learning-to-rank model on
that data would overfit within a week and be impossible to debug when it went
wrong — and "search got worse and nobody knows why" is a much worse failure
than "search is 3 % less good than it could be".

The weights in §11 are set by hand against the §23 golden set, checked into
source, and changed in a commit that says what moved and which test case
motivated it. For 300 documents and 40 golden queries, hand-tuning is not a
compromise; it is the method with the better error bars.

---

## 11. Ranking and reranking

### 11.1 Tiers

Five, ordered by how confident the system is about *why* a recipe is here.
A result never crosses a tier boundary for any reason other than the evidence
that put it there — not for popularity, not for recency, not for
personalisation.

| Tier | Name | Fires when | Example: query `Bolognese` |
| --- | --- | --- | --- |
| **0** | Exact | a folded title column equals the query in the same fold | `Bolognese` |
| **A** | Title | query is a prefix, suffix or whole word of the title, or every query term lands in the title band | `Spaghetti Bolognese`, `Lasagne Bolognese` |
| **B** | Strong lexical | all query terms matched, at least one in the title or tag band | `Bolognese-Sauce auf Vorrat` |
| **C** | Structural | all terms matched, but only in ingredients, description or steps | a chilli whose steps say "wie eine Bolognese" |
| **D** | Associative | matched only through concept expansion or fuzzy correction | `Ragù alla Napoletana`, via concept `ragout` |

`tier(d) = min over lanes of the tier that lane's evidence supports` — the
strongest evidence wins, which is why a recipe that matches both exactly and
fuzzily is tier 0 and not tier D.

**This is the mechanism that satisfies §3 R7.** Semantic adjacency cannot
outrank an exact match, ever, because it lives two tiers below and the sort is
lexicographic on tier first. It is also what makes the failure mode of a bad
concept entry bounded: a wrong synonym pollutes tier D, which is below the
fold, rather than the first result.

### 11.2 Signals

Within a tier, an additive score in `[0, 1]`. Every signal is bounded, named,
and independently testable.

| Signal | Weight | Definition | Why |
| --- | --- | --- | --- |
| `titleCoverage` | **0.30** | matched title tokens ÷ total title tokens | `Bolognese` is 100 % of `Bolognese`, 50 % of `Spaghetti Bolognese`, 33 % of `Lasagne Bolognese Auflauf`. The shorter the title, the more the recipe *is* the query. This is field-length normalisation, the same idea as BM25's `b`. |
| `queryCoverage` | **0.25** | matched query terms ÷ total query terms | Partial matches rank below complete ones. Carries most of the AND/OR distinction. |
| `lexicalRank` | **0.15** | `ts_rank_cd` with `{0.1, 0.3, 0.6, 1.0}` and normalisation `2|4` | Position and proximity within the document. |
| `fieldWeight` | **0.10** | best band matched: A 1.0, B 0.8, C 0.6, D 0.3 | A title match means more than a step mention. |
| `evidenceWeight` | **0.10** | exact 1.0, stem 0.85, substring 0.7, concept 0.5, fuzzy 0.4 | How the match happened, not only where. |
| `structuralFit` | **0.10** | preferred ingredients matched ÷ requested, minus a small penalty per extra ingredient; soft constraints (`schnell`) honoured here | Reuses the existing, good `matched_ingredients desc, extra_ingredients asc` logic. |
| — | — | — | — |
| `personalisation` | **≤ 0.15, applied after** | §13 | A tie-break, never a term in the relevance score. |

`personalisation` is deliberately outside the sum. It participates only in the
sort, after `score`, and only where scores are within 0.05 — see §13.

### 11.3 The worked example

Library:

```
1  Spaghetti Bolognese          tags: pasta, italienisch      cooked 12×
2  Lasagne Bolognese            tags: pasta, italienisch, ofen cooked 4×
3  Bolognese-Sauce auf Vorrat   tags: sauce, meal-prep         cooked 2×
4  Ragù alla Napoletana         tags: pasta, italienisch       cooked 0×
5  Gemüselasagne                tags: pasta, vegetarisch       cooked 6×
```

Query `Bolognese`:

| | Recipe | Tier | titleCov | queryCov | field | evidence | score | rank |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| | Spaghetti Bolognese | A | 0.50 | 1.0 | A | stem | **0.72** | 1 |
| | Lasagne Bolognese | A | 0.50 | 1.0 | A | stem | **0.72** | 2 |
| | Bolognese-Sauce auf Vorrat | B | 0.25 | 1.0 | A | stem | 0.61 | 3 |
| | Ragù alla Napoletana | D | 0.00 | 1.0 | B | concept | 0.34 | 4 |
| | Gemüselasagne | — | — | — | — | — | excluded | — |

The requested order, with two things worth saying plainly.

**#1 and #2 tie on relevance, and that is correct.** Both are two-token titles
carrying the query in the same position with the same evidence. There is no
*linguistic* fact that makes `Spaghetti Bolognese` more Bolognese than
`Lasagne Bolognese`; the intuition that it is comes from knowing which one the
household cooks. So that is what breaks the tie: `personalisation` (cook count
12 vs 4) puts Spaghetti first — and in a household that only ever makes the
lasagne, the lasagne comes first, which is right.

Inventing a semantic hierarchy to produce the "expected" order without that
evidence would be inventing a number users would rightly distrust — the exact
criterion `culina-v2-erv` already used to reject difficulty scores. This design
declines to, and says so.

**#4 is the requirement that actually matters.** `Ragù alla Napoletana` shares
no token with the query and is here only through the lexicon's
`bolognese → ragout` edge. It is in tier D, so it can never reach the top three
however high its score climbs, and its row in the UI says
`Ähnlich: Ragout` (§18.5). That is the "semantic results must not float to the
top" requirement, enforced structurally rather than by tuning.

A second example, `vegetarisch`, to show a filter doing its job:

```
query "vegetarisch"
  → parsed: diet = vegetarian (HARD).  free text: "" .  no lanes run at all.
  → filter: exclude every recipe whose ingredients contain a `meat` keyword
  → order:  tagged vegetarisch first (certain), then presumed (§7.5)
            within each, by cook count, then updated_at

  1  Gemüselasagne          tagged vegetarisch      ✓ certain
  2  Tomatensuppe           tagged vegetarisch      ✓ certain
  3  Kartoffelgratin        no meat keyword found   ~ presumed  ← "Ist das vegetarisch?"
  ✗  Spaghetti Bolognese    contains "Hackfleisch"  excluded, not demoted
```

The Bolognese is *absent*, not ranked low. Its embedding proximity to
"vegetarisch" — if there were embeddings — is irrelevant, because a hard
constraint is a `WHERE` clause.

### 11.4 Negative signals and hard constraints

| Constraint | Kind | Implementation |
| --- | --- | --- |
| `diet = vegetarian/vegan` | hard exclude | ingredient keyword families (§7.5) |
| `ohne X` | hard exclude | `not exists (… ri.name ilike '%X%')` |
| `maxMinutes` | hard exclude | unchanged from today, including the deliberate exclusion of recipes with no stated time |
| `tag` | hard | unchanged: all tags must be present |
| `cookbookId` | hard | unchanged, including smart-shelf rules |
| `meal`, `cuisine` | **soft** | boost, because they are derived rather than declared and a wrong exclusion is invisible |
| `schnell` | **soft** | boost toward `totalMinutes ≤ 30`; never excludes an untimed recipe |

The hard/soft line is drawn at *who asserted it*. A number the user typed and a
tag the household wrote are assertions, and are enforced. Something the lexicon
inferred is a guess, and guesses rank rather than exclude.

### 11.5 Cookbooks and existing sorts

Nothing about the existing sort options changes. `sort=relevance` gains a real
meaning (tier + score, rather than ingredient overlap alone); `-updatedAt`,
`title`, `totalMinutes`, `-cookCount` and `cookbookOrder` are untouched, and
still work inside a cookbook because a cookbook remains a filter over the
collection. The relevance cursor gains `tier` and `score` as leading key
components (§15).

### 11.6 Reranking: not built, and why

| Option | Verdict |
| --- | --- |
| Handcrafted rules over the top *k* | **This is what §11.1–11.3 is.** A rerank stage would be a second place ranking lives, which is how two rankings drift. |
| BM25 + metadata boosts | Effectively what `ts_rank_cd` + signals already is, minus a corpus-wide IDF that is noise at 300 documents. |
| Embedding similarity rerank | §9. |
| RRF | §10.2. |
| Cross-encoder | 30–80 ms × 25 candidates = 0.75–2 s, against a 60 ms budget. Twelve to thirty times over. |
| Local LLM rerank | Slower still, non-deterministic, and search stops working when the model does. |
| Learning to rank | §10.4: ~300 noisy implicit judgements a month is not a training set. |

There is one thing a reranker would genuinely add and this design does not
have: awareness of *term order* beyond what `ts_rank_cd`'s cover density
provides — `Zitronen Hähnchen` vs `Hähnchen Zitrone`. For recipe titles this is
worth approximately nothing, and it is recorded here so that the omission is a
choice.

---

## 12. The culinary lexicon

### 12.1 What it is, and what it is emphatically not

A single curated bilingual table of culinary concepts. **Not an ontology** —
no inference engine, no reasoner, no OWL, no transitive closure computed at
query time. A flat map from surface forms to concepts, plus a shallow parent
edge, compiled into two artefacts at startup.

It is the third member of a family that already exists in this codebase, and
it is designed to read like its siblings:

| | Answers | Shape |
| --- | --- | --- |
| `CommonIngredients` | "what might they be typing?" | ~120 DE/EN pairs + section |
| `SectionKeywords` | "which shop aisle?" | ~250 stems → section, longest first |
| **`CulinaryLexicon`** | "what else means this?" | ~350 concepts → surface forms, family, kind |

`docs/domain-model.md` already argues that the first two must stay separate
because one is for offering and the other for matching. The third is for
*relating*, which is a third question.

### 12.2 Shape

```csharp
// Domain/Search/CulinaryLexicon.cs
public sealed record Concept(
    string Key,                       // "chicken" — stable, never shown to a user
    ConceptKind Kind,                 // Ingredient | Dish | Cuisine | Meal
                                      // | Method | Diet | Character
    IReadOnlyList<string> De,         // surface forms, folded at build time
    IReadOnlyList<string> En,
    IReadOnlyList<string> Parents,    // one level, occasionally two
    float Distance = 1.0f);           // < 1 for colloquial or marginal forms

// ── Ingredients ───────────────────────────────────────────────────────────
new("chicken", Ingredient,
    De: ["haehnchen", "hahnchen", "huhn", "huehnchen", "hendl", "poulet",
         "haehnchenbrust", "haehnchenschenkel", "poularde"],
    En: ["chicken", "hen", "poultry"],
    Parents: ["poultry", "meat"]),

new("rooster", Ingredient,
    De: ["gockel", "hahn"], En: ["rooster", "cockerel"],
    Parents: ["chicken", "poultry", "meat"],
    Distance: 0.6f),        // colloquial: expands, but ranks visibly lower

new("tomato", Ingredient,
    De: ["tomate", "tomaten", "cocktailtomate", "kirschtomate",
         "passierte tomaten", "passata", "tomatenmark", "dosentomaten"],
    En: ["tomato", "tomatoes", "cherry tomato", "passata", "tomato paste"],
    Parents: ["vegetable"]),

// ── Dishes ────────────────────────────────────────────────────────────────
new("bolognese", Dish,
    De: ["bolognese", "bolognaise", "hackfleischsosse", "ragu"],
    En: ["bolognese", "meat sauce", "ragu"],
    Parents: ["pasta_sauce", "italian", "hot_meal"]),

new("lasagne", Dish,
    De: ["lasagne", "lasagna"], En: ["lasagne", "lasagna"],
    Parents: ["pasta", "italian", "baked_dish", "hot_meal"]),

new("pasta", Dish,
    De: ["nudel", "nudeln", "pasta", "spaghetti", "penne", "fusilli",
         "tagliatelle", "makkaroni", "spatzle", "spaetzle"],
    En: ["pasta", "noodles", "spaghetti", "penne"],
    Parents: ["italian"]),

// ── Character: what §4.J's vague queries resolve to ───────────────────────
new("summer", Character,
    De: ["sommer", "sommergericht", "sommerlich", "leicht", "erfrischend"],
    En: ["summer", "summery", "light", "refreshing"],
    Parents: []),           // maps to household tags first — see §12.4

new("comfort", Character,
    De: ["comfort food", "seelenfutter", "deftig", "herzhaft"],
    En: ["comfort food", "hearty"],
    Parents: ["hot_meal"]),
```

Size and shape, deliberately bounded:

| Kind | Count | Sourced from |
| --- | --- | --- |
| Ingredient | ~180 | `SectionKeywords` + `CommonIngredients`, already bilingual |
| Dish | ~80 | the dishes a German/English household actually cooks |
| Cuisine | ~16 | |
| Meal | 6 | |
| Method | ~20 | braten, backen, grillen, kochen, dünsten… |
| Diet | 6 | plus the `meat` / `fish` / `hidden-animal` families |
| Character | ~12 | warm, kalt, leicht, deftig, süß, scharf, sommer, winter… |
| **Total** | **~320** | |

Three hundred and twenty entries is one afternoon to write and half a day to
review. Three thousand would be an unmaintainable liability whose long tail is
exactly where it is least likely to be right — the same argument
`CommonIngredients` already makes for staying short, in its own comment.

### 12.3 Where it is used

Compiled once at startup into two lookup tables, from the same source, which is
the property that keeps index and query in step:

```
                        CulinaryLexicon (C#, source-controlled, ~320 entries)
                                    │
                    ┌───────────────┴────────────────┐
                    ▼                                ▼
         surface form → concept[]          concept → concept[]  (parents)
                    │                                │
    ┌───────────────┼────────────────┐               │
    ▼               ▼                ▼               ▼
 INDEXING        QUERY            FACETS       SIMILARITY
 concepts[]      concept lane     refinement   related recipes
 on each doc     expansion        chips        (§19)
```

**Indexing.** A recipe's title, tags and ingredient names are matched against
the surface forms; the concepts found, plus their parents, become
`concepts text[]`. `Spaghetti Bolognese` with `Hackfleisch`, `Tomaten`,
`Zwiebel` yields:

```
{ pasta, bolognese, pasta_sauce, italian, hot_meal,
  beef, meat, tomato, vegetable, onion }
```

**Querying.** The same expansion runs on the query, producing lane 4's array,
and the `&&` overlap is the match.

**Cross-language falls out for free.** `chicken` and `Hähnchen` are surface
forms of one concept, so an English query finds a German recipe and vice versa
with no translation step anywhere.

### 12.4 Household vocabulary beats the lexicon

The one rule that keeps this from becoming curated data nobody maintains.

```
concepts_for(query) =
        household tags whose slug or name matches         ← FIRST, and strongest
      ∪ lexicon concepts                                  ← fallback
```

A household that tags `sommer` gets its own tag for `Sommergericht`, ranked
higher than anything the lexicon inferred, because a tag is an assertion and a
lexicon entry is a guess. This is the same principle
[`domain-model.md`](domain-model.md#ingredient-suggestions) already applies to
ingredient suggestions: after a few recipes, a kitchen's own words are the
better answer. It also means the lexicon can be wrong about a household without
that household being stuck with it — they tag their way out.

### 12.5 What is not modelled

- **Seasonality as ingredient→month.** Rejected on `culina-v2-erv`, for the
  reason `README.md` rejects a pantry: curated data that goes stale and
  poisons everything above it. `sommer`/`winter` exist as *Character* concepts
  that resolve to household tags and dish families, which is a claim about
  cooking rather than about agriculture.
- **Quantities, nutrition, allergens.** No data, and a wrong number is worse
  than none. Allergen search is answered as a negative ingredient constraint
  (`ohne Nüsse`), which is honest about being a name match.
- **Substitution rules** (`Butter → Margarine`). A different feature with a
  different failure cost; §19 notes the lexicon would support it and this
  document does not propose it.
- **Transitive inference.** Parents are stored expanded at index time, one or
  two levels, materialised into `concepts[]`. No closure is computed at query
  time. If `Hähnchen → Geflügel → Fleisch` needs a third level, it is written
  out, because a three-line array beats a graph walk in the hot path.

### 12.6 Maintenance

The lexicon is C# source. Changing it is a pull request with a test, not a data
migration. A `LexiconVersion` constant is stored on every search document; a
hosted service at startup reindexes rows whose version is stale (§16.4), so
shipping a lexicon change is shipping a container.

---

## 13. Personalisation and context

### 13.1 The distinction that matters

> **Search relevance** answers *"does this recipe match what was asked?"* It is
> household-scoped, identical for every member, and reproducible.
>
> **Personalised ranking** answers *"of the things that match equally well,
> which do you mean?"* It is per person and it only ever reorders ties.

Culina implements the first as tier + score, the second as a bounded final sort
key. They are never added together.

### 13.2 Which signals, and how much

| Signal | Source | Used | Weight |
| --- | --- | --- | --- |
| Times **you** have cooked it | `cook_log_entries` (already counted in the search projection) | tie-break | `0.08 · log₂(1+n)/log₂(1+n_max)` |
| How recently you cooked it | `cook_log_entries.made_at` | tie-break | `0.04` if within 30 days |
| Planned this week | `meal_plan_entries` | tie-break | `0.03` |
| **Cap** | | | **0.15** |
| Times the *household* cooked it | `cook_log_entries` | **no** | — |
| Recipes you opened | not recorded | **no** | — |
| Recipes you disliked | not recorded, and will not be | **no** | — |
| Current season | — | **no** | §12.5 |
| Time of day | — | **no** | see below |
| Recent searches | client-side only | suggestions only, never ranking | — |

And the rule that contains all of it:

```
personalisation may only reorder results whose (tier, score) differ by
    tier: 0        (same tier — never crosses one)
    score: ≤ 0.05  (a genuine tie)
```

At most 0.15 against a 0.05 window means personalisation can swap neighbours
and can do nothing else. The first screen's *membership* is identical for every
member of the household; only the order of near-equals differs. That is the
line between "it knows me" and "it is hiding things from me", and it is drawn
in code rather than in intent.

### 13.3 What is deliberately not personalised

**Time of day.** It is the most tempting contextual signal and the worst. A
person searching `Pfannkuchen` at 19:00 is not asking for dinner; they are
planning Sunday. A search that quietly reorders because of the clock produces
exactly the failure mode §3 R7 forbids: the same query gives different answers
and nothing explains why. If time of day ever appears, it belongs on the home
screen as a *labelled* suggestion shelf ("Frühstück"), where it is visible and
dismissible — not inside search.

**Session search history as a ranking signal.** Recent searches make excellent
*suggestions* and terrible *boosts*. They are stored in `localStorage`, shown
in the overlay's empty state, never sent to the server, and never touch a score
(§21).

**Anything the household can see about an individual.** Two to eight people in
one kitchen, one household id. Aggregating "what does this household search
for" is a privacy leak inside a home, and this design does not record it.

### 13.4 Reproducibility

An integration test asserts it directly: the same query, run as two different
members of one household, returns the **same set** in the **same tiers**, and
differs only in the order of results within 0.05 of each other. That test is
what keeps §13.2's cap honest as the code changes.

---

## 14. The recommended architecture

### 14.1 Four candidates

**Architecture A — Minimal.** Fold the query, add `word_similarity` to the
existing `ILIKE`, add a small synonym map, boost title matches. One file
changed, no new table, ~2 days.
*Quality: fixes typos and synonyms. Does not fix morphology, cannot rank, has no
query understanding, cannot search steps, and re-scans the recipe tables on
every keystroke.* A good week-one patch, not an architecture.

**Architecture B — Dedicated engine.** Meilisearch beside the app, documents
pushed on write, search proxied through the API.
*Quality: excellent typo tolerance and prefix search out of the box, weaker
German compounds, and every custom ranking rule has to be expressed in its
language rather than in SQL. Costs a container, a volume, a backup artefact, an
eventual-consistency bug class, and the README's opening claim.*

**Architecture C — PostgreSQL hybrid (recommended).** A maintained search
document; four lanes; a deterministic query grammar; a curated lexicon; tier
plus bounded score. Everything inside the existing process and the existing
database.

**Architecture D — C plus local embeddings.** C, plus `pgvector` and an int8
multilingual encoder, fused by RRF for vague queries only.
*Quality: adds real capability on ~1 % of queries. Costs +120 MB image,
+250 MB RSS, a Culina-maintained PostgreSQL image, and a class of result the
user cannot verify.* Designed in §9.5, gated in §9.4, not recommended now.

### 14.2 Scored against §18's criteria

| Criterion | A | B | **C** | D |
| --- | --- | --- | --- | --- |
| Search quality (§4 weighted) | ●●○○○ | ●●●●○ | **●●●●○** | ●●●●● |
| Perceived responsiveness | ●●●○○ | ●●●●● | **●●●●●** | ●●●○○ |
| Robustness | ●●●●○ | ●●●○○ | **●●●●●** | ●●●○○ |
| Explainable ranking | ●●○○○ | ●●●○○ | **●●●●●** | ●●○○○ |
| Integration with the app | ●●●●● | ●●○○○ | **●●●●●** | ●●●●○ |
| Maintainability | ●●●●○ | ●●●○○ | **●●●●○** | ●●○○○ |
| Self-hosting fit | ●●●●● | ●●○○○ | **●●●●●** | ●●○○○ |
| Privacy | ●●●●● | ●●●●○ | **●●●●●** | ●●●●● |
| CPU / RAM | ●●●●● | ●●●○○ | **●●●●●** | ●●○○○ |
| Implementation complexity | ●●●●● | ●●●○○ | **●●●○○** | ●●○○○ |

C loses to D on raw quality by about half a point, concentrated in the query
class that is 1 % of traffic, and beats it on seven of the other nine criteria
including every one that a self-hosted app is judged on.

### 14.3 The recommended architecture

```
╔══════════════════════════════ BROWSER ═══════════════════════════════════╗
║                                                                          ║
║   ⌘K / tap / "/"                                                         ║
║        │                                                                 ║
║        ▼                                                                 ║
║   ┌─────────────────────────────────────────────────────────────────┐    ║
║   │ SearchOverlay                                                   │    ║
║   │  ├── interpretation chips  [Vegetarisch ×] [< 30 Min ×]         │    ║
║   │  ├── suggestions          (120 ms debounce → /suggestions)      │    ║
║   │  ├── results              (200 ms debounce → /recipes)          │    ║
║   │  └── refinement chips     (facets, from the result set)         │    ║
║   └─────────────────────────────────────────────────────────────────┘    ║
║        │  searchStore.svelte.ts — its own store, like RecipePicker's     ║
║        │  recentSearches → localStorage only, never sent                ║
╚════════│═════════════════════════════════════════════════════════════════╝
         │  same origin, cookie auth, no CORS
╔════════▼════════════════════ ONE CONTAINER ══════════════════════════════╗
║  Api ── GET /v1/recipes                       (search + list + facets)   ║
║      ── GET /v1/households/{id}/suggestions   (autocomplete)             ║
║      ── GET /v1/recipes/{id}/related          (more like this)           ║
║        │                                                                 ║
║  Application                                                             ║
║      ┌──────────────────────┐   ┌──────────────────────────────────┐     ║
║      │ QueryUnderstanding   │──▶│ RecipeSearchService              │     ║
║      │  normalise           │   │  lanes → filter → tier → score   │     ║
║      │  grammar (≈40/lang)  │   │  facets                          │     ║
║      │  concept expansion   │   │  fallback ladder (§22)           │     ║
║      └──────────┬───────────┘   └────────────────┬─────────────────┘     ║
║                 │                                 │                       ║
║      ┌──────────▼───────────┐                     │                       ║
║  Domain │ CulinaryLexicon   │  SearchText.Fold    │                       ║
║      │ ~320 concepts, DE/EN │  (next to ItemName) │                       ║
║      └──────────────────────┘                     │                       ║
║                                                   │                       ║
║  Infrastructure                                   ▼                       ║
║      RecipeSearcher (extended)      SearchDocumentWriter                  ║
║             │                              │ same transaction as the      ║
╚═════════════│══════════════════════════════│═ recipe write ══════════════╝
              ▼                              ▼
╔════════════════════════ postgres:18-alpine (unchanged) ══════════════════╗
║  recipes, recipe_ingredients, steps, tags, cook_log_entries  (unchanged) ║
║  ┌────────────────────────────────────────────────────────────────────┐  ║
║  │ recipe_search_documents                                     NEW    │  ║
║  │   document tsvector   GIN    ← culina_de / culina_en               │  ║
║  │   fuzzy_text text     GIN trgm                                     │  ║
║  │   title_ae / title_a  btree                                        │  ║
║  │   concepts text[]     GIN                                          │  ║
║  └────────────────────────────────────────────────────────────────────┘  ║
║  extensions: pg_trgm ✓ already · unaccent ✓ already · citext ✓ already   ║
╚══════════════════════════════════════════════════════════════════════════╝
```

Nothing outside the existing boundary. No new container, no new volume, no new
extension, no new port, no change to `compose.yaml`, `compose.prod.yaml` or
`docs/operations.md` beyond a line about a longer first migration.

### 14.4 Request flow

```
  Browser                 Api            Application            PostgreSQL
     │                     │                  │                      │
     │ GET /v1/recipes?query=vegetarisch%20unter%2030%20min…         │
     ├────────────────────▶│                  │                      │
     │                     │ ToRecipeSearch   │                      │
     │                     ├─────────────────▶│                      │
     │                     │                  │ normalise   <0.1 ms  │
     │                     │                  │ grammar      0.3 ms  │
     │                     │                  │ expand       0.1 ms  │
     │                     │                  │                      │
     │                     │                  │ one SQL statement    │
     │                     │                  ├─────────────────────▶│
     │                     │                  │                      │ lane 0   0.2 ms
     │                     │                  │                      │ lane 2   3 ms
     │                     │                  │                      │ lane 4   2 ms
     │                     │                  │                      │ [lane 3 skipped:
     │                     │                  │                      │  48 candidates]
     │                     │                  │                      │ filter   1 ms
     │                     │                  │                      │ tier+score 2 ms
     │                     │                  │                      │ facets   3 ms
     │                     │                  │◀─────────────────────┤ 11 ms
     │                     │◀─────────────────┤ map to Response      │
     │◀────────────────────┤                  │                      │
     │  { items, total, nextCursor, interpretation, facets }         │
     │                                                               │
     │  interpretation → chips        facets → refinement chips      │
```

One round trip returns the results, what the query was understood to mean, and
what to offer next. That is what makes the interface in §18 feel immediate
without it being a second request.

---

## 15. The search data model

### 15.1 The migration

```sql
-- 0012_search.sql
--
-- The search document: one row per recipe, holding the same recipe in the four
-- shapes the four retrieval lanes need. It is derived, never authored, and it
-- is written in the same transaction as the recipe it describes — the same
-- decision step_ingredient_refs already made, for the same reason. A search
-- index that is eventually consistent with its source is an index that
-- occasionally cannot find a recipe somebody just saved, and nothing will have
-- said so.
--
-- Not a materialised view: those refresh wholesale, and 'REFRESH MATERIALIZED
-- VIEW' on every recipe save is the whole household's library rebuilt because
-- somebody fixed a typo.

-- Two configurations, one per content language. unaccent runs BEFORE the
-- stemmer, which is the whole fix for 'Hähnchen' and 'Hahnchen' being
-- different lexemes to the German Snowball stemmer.
create text search configuration culina_de (copy = german);
alter text search configuration culina_de
    alter mapping for asciiword, asciihword, hword_asciipart,
                      word, hword, hword_part, numword, numhword
    with unaccent, german_stem;

create text search configuration culina_en (copy = english);
alter text search configuration culina_en
    alter mapping for asciiword, asciihword, hword_asciipart,
                      word, hword, hword_part, numword, numhword
    with unaccent, english_stem;

create table recipe_search_documents (
    recipe_id      uuid        not null primary key
                                   references recipes (id) on delete cascade,

    -- Denormalised from recipes.household_id so that every search predicate
    -- can start here. The household filter is a security boundary; making it
    -- a join would put the boundary one table away from the query.
    household_id   uuid        not null
                                   references households (id) on delete cascade,

    language       text        not null,

    -- A: title.  B: tags and concepts.  C: ingredient names.
    -- D: description and step bodies.
    document       tsvector    not null,

    -- Every word above, in BOTH German transliterations (ä→ae and ä→a),
    -- space-joined. This is the lane that answers compounds and typos, and it
    -- carries both folds because ItemName.Fold says 'ue' and unaccent says
    -- 'u', and a person writes both.
    fuzzy_text     text        not null,

    -- The title alone, in each fold, in its own column. Two columns rather
    -- than one holding both spellings, because lane 0 tests equality: a
    -- single column containing 'kaesekuchen kasekuchen' equals neither
    -- spelling of what anybody types.
    title_ae       text        not null,   -- ä → ae
    title_a        text        not null,   -- ä → a  (what unaccent would give)

    -- Lexicon concepts of the title, tags and ingredients, with their parents
    -- already expanded. Materialised rather than walked at query time: a
    -- three-element array beats a graph traversal in the hot path.
    concepts       text[]      not null default '{}',

    -- Derived hard flags, so a diet filter is an array test rather than a
    -- keyword scan over every ingredient row on every query.
    diet           text[]      not null default '{}',   -- 'vegetarian' | 'vegan'
    diet_certain   bool        not null default false,  -- tagged, not presumed

    -- Cached because the ranking needs them and joining for them on every row
    -- of every search is the join the projection already regrets.
    total_minutes  int,
    ingredient_count int       not null default 0,
    step_count     int         not null default 0,

    -- The recipe version this was built from, and the analyser version that
    -- built it. The first catches a write that did not reindex; the second is
    -- how a lexicon change ships (§16.4).
    source_version bigint      not null,
    lexicon_version int        not null,

    updated_at     timestamptz not null
);

create index recipe_search_document_idx
    on recipe_search_documents using gin (document);

create index recipe_search_fuzzy_idx
    on recipe_search_documents using gin (fuzzy_text gin_trgm_ops);

create index recipe_search_concepts_idx
    on recipe_search_documents using gin (concepts);

-- text_pattern_ops, not the default: the cluster runs --locale=C, but stating
-- it here means a prefix LIKE keeps using this index if that ever changes.
create index recipe_search_title_ae_idx
    on recipe_search_documents (household_id, title_ae text_pattern_ops);

create index recipe_search_title_a_idx
    on recipe_search_documents (household_id, title_a text_pattern_ops);

-- Every search starts with the household, so every other index is reached
-- through this one.
create index recipe_search_household_idx
    on recipe_search_documents (household_id);

-- Finds the rows a lexicon change made stale, and nothing else.
create index recipe_search_stale_idx
    on recipe_search_documents (lexicon_version)
    where lexicon_version < 2147483647;
```

### 15.2 A row, in full

For `Spaghetti Bolognese` — German, 45 min, tagged `pasta`, `italienisch`,
ingredients `Hackfleisch`, `passierte Tomaten`, `Zwiebel`, `Spaghetti`,
`Rotwein`:

```
recipe_id        019423…
household_id     0193f1…
language         de
document         'bolognes':2A 'spaghett':1A
                 'italienisch':5B 'pasta':4B 'hot_meal':9B 'meat':11B …
                 'hackfleisch':14C 'tomat':16C 'zwiebel':18C 'rotwein':20C
                 'anbrat':31D 'ablosch':36D 'kocht':41D …
fuzzy_text       'spaghetti bolognese hackfleisch passierte tomaten zwiebel
                  spaghetti rotwein pasta italienisch anbraten abloeschen
                  abloschen koechelt kochelt …'
title_ae         'spaghetti bolognese'
title_a          'spaghetti bolognese'      (identical: no umlaut in the title)
concepts         {pasta, bolognese, pasta_sauce, italian, hot_meal,
                  beef, meat, tomato, vegetable, onion, wine}
diet             {}
diet_certain     false
total_minutes    45
ingredient_count 5
step_count       6
source_version   7
lexicon_version  3
```

`abloeschen` and `abloschen` both appear in `fuzzy_text`: one word, two folds.
That duplication is what lets `abloeschen` and `ablöschen` and `abloschen` all
find this recipe by substring, and it costs about 18 % of one text column.

### 15.3 Size

Measured per recipe, for a typical 5-ingredient, 6-step recipe:

| | Bytes |
| --- | --- |
| `document` tsvector | ~900 |
| `fuzzy_text` | ~700 |
| `title_ae` + `title_a` + `concepts` + flags | ~280 |
| GIN index entries, all four | ~1,300 |
| **Total** | **~3.2 KB** |

| Library | Added storage |
| --- | --- |
| 100 recipes | 320 KB |
| 500 recipes | 1.6 MB |
| 2,000 recipes | **6.4 MB** |
| 10,000 recipes | 32 MB |

For comparison, one recipe photograph is 200–800 KB. The entire search index
for a 2,000-recipe library costs less than fifteen photographs.

### 15.4 The relevance cursor

`RecipeSearchSql` gains one order and one resume predicate. Everything else in
the cursor machinery is unchanged, including the "every order ends in the
recipe id" rule that makes paging stable.

```csharp
RecipeSort.Relevance =>
    "tier asc, score desc, personal desc, updated_at desc, id desc",

RecipeSort.Relevance =>
    "(tier, -score, -personal, updated_at, id) "
  + "< (cast(@k0 as int), -cast(@k1 as float8), -cast(@k2 as float8), "
  +    "cast(@k3 as timestamptz), @cursorId)",
```

`tier` ascending inside a row comparison that is otherwise descending is why it
is negated rather than the others: the comparison is written so one `<` covers
every component, which is what lets PostgreSQL use the ordering directly.

A search cursor is only valid for as long as nothing in the household changes —
which is true of every existing cursor here too, and is why the page size is 24
and the list is not a virtualised infinite scroll.

---

## 16. Backend integration

### 16.1 New and changed files

```
Domain/
  Search/
    SearchText.cs               NEW  the fold, both transliterations
    CulinaryLexicon.cs          NEW  ~320 concepts, DE/EN, families
    SearchConcept.cs            NEW  Concept, ConceptKind
    QueryIntent.cs              NEW  the parsed query, as a value object

Application/
  Abstractions/
    RecipeSearch.cs             ~    + Intent, Diet, Meal, Cuisine, Exclusions
    ISearchDocumentWriter.cs    NEW  the port the write side uses
    ISuggestionReader.cs        NEW  the port autocomplete uses
  Search/
    QueryUnderstandingService.cs NEW normalise → grammar → expand
    QueryGrammar.cs             NEW  the rule table, one per language
    RecipeSearchService.cs      NEW  orchestrates; owns the fallback ladder
    SearchFacets.cs             NEW  refinement chips from the candidate set
  Recipes/
    GetAll/GetRecipesQuery.cs   ~    calls QueryUnderstanding first
    Create|Update|Delete/…      ~    one line each: reindex
    Suggest/GetSuggestionsQuery.cs NEW
    Related/GetRelatedQuery.cs  NEW

Infrastructure/
  Persistence/Recipes/
    RecipeSearcher.cs           ~    the four lanes; the projection is reused
    RecipeSearchSql.cs          ~    the relevance order and its cursor
    SearchDocumentWriter.cs     NEW  builds and upserts one row
    SearchDocumentSql.cs        NEW  the upsert, and the reindex query
    SuggestionReader.cs         NEW
  Persistence/Migrations/
    0012_search.sql             NEW
  Search/
    LexiconReindexService.cs    NEW  hosted; reindexes stale rows at startup

Api/Endpoints/Recipes/
  GetAll/V1/GetRecipesRequestExtensions.cs  ~  no change to parsing; the
                                               query string is unchanged
  Suggest/V1/GetSuggestionsEndpoint.cs      NEW
  Related/V1/GetRelatedEndpoint.cs          NEW
Contracts/Recipes/
  GetAll/Response.cs            ~    + Interpretation, Facets, MatchReason
  Suggest/Response.cs           NEW
  Related/Response.cs           NEW
```

Four services, not five. `RecipeEmbeddingService` is absent (§9);
`RecipeTaxonomyService` is absent because a ~320-entry static lookup with no
I/O and no state is a `static class` in `Domain` — wrapping it in an injected
service would add a lifetime, a registration and an interface to something that
is a dictionary. `dotnet-dependency-injection` is explicit that only
abstractions that genuinely simplify get introduced.

### 16.2 Index lifecycle

The whole of it:

```
recipe created  ─┐
recipe updated  ─┼─▶ same DbSession transaction ─▶ upsert one document row
recipe deleted  ─┘                                 (or cascade removes it)

container starts ──▶ LexiconReindexService
                       reindex rows where lexicon_version < current
                       in batches of 200, logged, cancellable

migration 0012  ──▶ backfill every row, in the migration itself
```

No queue, no background job, no eventual consistency, nothing to backfill after
a rule change except by version. This mirrors the decision
[`0010_smart_cookbooks.sql`](../src/backend/src/Infrastructure/Persistence/Migrations/0010_smart_cookbooks.sql)
already documents — *"a saved question, answered whenever somebody looks"* —
with the one difference that a search document is genuinely expensive to
recompute per read, so it is stored. Everything else about the reasoning
carries over: no job to run, nothing to backfill, no way for the index to drift
from the recipes it claims to describe.

The write costs ~2–4 ms and happens inside a transaction that already exists.

```csharp
// Infrastructure/Persistence/Recipes/SearchDocumentWriter.cs
internal sealed class SearchDocumentWriter(DbExecutor executor) : ISearchDocumentWriter
{
    /// <summary>The analyser version. Bumped when the lexicon or the fold changes.</summary>
    internal const int LexiconVersion = CulinaryLexicon.Version;

    public Task WriteAsync(Recipe recipe, CancellationToken cancellationToken)
    {
        var ingredients = recipe.Ingredients.Select(i => i.Name).ToArray();
        var concepts    = CulinaryLexicon.ConceptsOf(recipe.Title.Value, recipe.Tags, ingredients);
        var diet        = DietFlags.For(recipe, concepts);

        // The configuration name comes from a two-member enum, never from
        // input, which is what makes interpolating it into the cast safe.
        var config = recipe.Language == Language.De ? "culina_de" : "culina_en";

        return executor.ExecuteAsync(SearchDocumentSql.Upsert(config), new
        {
            recipeId    = recipe.Id,
            householdId = recipe.HouseholdId,
            language    = PreferenceCodes.Of(recipe.Language),
            title       = recipe.Title.Value,
            band_b      = string.Join(' ', recipe.Tags.Concat(concepts)),
            band_c      = string.Join(' ', ingredients),
            band_d      = $"{recipe.Description} {string.Join(' ', recipe.Steps.Select(s => StepText.PlainText(s.Segments)))}",
            fuzzyText   = SearchText.FuzzyCorpus(recipe),   // both folds
            titleAe     = SearchText.Fold(recipe.Title.Value).Umlaut,
            titleA      = SearchText.Fold(recipe.Title.Value).Stripped,
            concepts,
            diet        = diet.Flags,
            dietCertain = diet.FromTags,
            totalMinutes     = recipe.TotalMinutes,
            ingredientCount  = ingredients.Length,
            stepCount        = recipe.Steps.Count,
            sourceVersion    = recipe.Version,
            lexiconVersion   = LexiconVersion
        }, cancellationToken);
    }
}
```

**Failure behaviour is the interesting part.** The write is in the recipe's
transaction, so a failure to index is a failure to save. That is the correct
trade: a recipe that saved but is unfindable is a worse outcome than a save
that failed visibly and can be retried — and it is the same trade
`step_ingredient_refs` already makes. Architecture test:
`every handler that writes a Recipe also writes a search document`.

### 16.3 Read path

```csharp
// Application/Search/RecipeSearchService.cs
internal sealed class RecipeSearchService(
    IRecipeRepository recipes,
    QueryUnderstandingService understanding,
    ITagRepository tags)
{
    public async Task<SearchResult> SearchAsync(RecipeSearch search, CancellationToken ct)
    {
        // Household tags first: a tag the household wrote beats a lexicon
        // guess, so the expansion needs their vocabulary (§12.4).
        var vocabulary = await tags.InUseAsync(search.HouseholdId, ct).ConfigureAwait(false);
        var intent     = understanding.Parse(search.Query, vocabulary);

        var page = await recipes.SearchAsync(search.With(intent), ct).ConfigureAwait(false);

        // The recovery ladder (§22). Each rung is the SAME query with one
        // thing changed, so a fallback can never return something the original
        // criteria would have excluded.
        if (page.Items.Count == 0)
        {
            return await RecoverAsync(search, intent, ct).ConfigureAwait(false);
        }

        return new SearchResult(page, intent, Facets: SearchFacets.From(page, intent));
    }
}
```

### 16.4 Migration

`0012_search.sql` creates the table and backfills it. For 2,000 recipes the
backfill is ~4 s inside the migration transaction — acceptable for a one-time
upgrade, and `docs/operations.md` gains a sentence saying the first start after
this version takes a few seconds longer.

Rollout is safe in both directions. Until the table is populated the old
`ILIKE` path still works; if the new path ever needs disabling,
`Search__Mode=legacy` restores it in one setting, and the table is additive so
nothing has to be undone.

### 16.5 Configuration

Per `dotnet-configuration`: one record, registered as a singleton, injected
directly, validated at startup. No `IOptions<T>`.

```csharp
public sealed record SearchSettings
{
    /// <summary>Above this word_similarity a typo counts as a match.</summary>
    public float FuzzyThreshold { get; init; } = 0.60f;

    /// <summary>Skip the fuzzy lane once this many candidates exist.</summary>
    public int FuzzySkipThreshold { get; init; } = 48;

    /// <summary>How many suggestions autocomplete returns, per entity kind.</summary>
    public int SuggestionLimit { get; init; } = 5;

    /// <summary>Ceiling on personalisation's contribution to the sort (§13.2).</summary>
    public float PersonalisationCap { get; init; } = 0.15f;

    /// <summary>'hybrid' or 'legacy'. The escape hatch, not a feature.</summary>
    public string Mode { get; init; } = "hybrid";
}
```

Five knobs, defaults that are right, and none of them required to be set.
`docs/configuration.md` gains one table row.

---

## 17. The search API

### 17.1 Why `GET /recipes` stays the search endpoint

The brief suggests a `SearchRequest { query, context, filters, limit, userId }`
with a `context` enum. That shape is worth examining and then declining, for
three reasons specific to this codebase.

**It would violate `rest-api-design`.** Culina's convention is resources and
identifiers only — no verb routes, no pseudo-resources. `POST /search` is a
verb. The convention is not arbitrary: it is what makes `GET /recipes?…`
cacheable, linkable, ETag-able and readable in a log.

**`GET /recipes` is already a search endpoint.** It has `query`, `tag`,
`ingredient`, `maxMinutes`, `cookbookId`, `sort`, `cursor` and `limit`, with a
wrapped response and a stable cursor. Adding a second endpoint that returns
recipes with filters would be two things that must agree about what a tag means
— exactly the drift `SmartShelfSql` exists to prevent.

**`userId` in the request body is wrong.** It is in the session cookie. A search
request that names a user is a request that can name a different one.

**And `context` is not a real parameter.** Examining what the five proposed
contexts actually differ in:

| Context | Differs in |
| --- | --- |
| `GLOBAL_SEARCH` | nothing — this is `GET /recipes?query=` |
| `RECIPE_LIST` | nothing — this is `GET /recipes?query=` with filters |
| `RELATED_RECIPES` | the *query* is a recipe id → `GET /recipes/{id}/related` |
| `INGREDIENT_SEARCH` | the *entity kind* → `GET /households/{id}/ingredients`, exists |
| `AUTOCOMPLETE` | the *shape of the answer* → a new endpoint |

Three of five are the same request. The other two differ in what is being asked
for, which in REST is a different resource, not a mode flag. A `context` enum
would collapse five things into one endpoint that branches internally — the
opposite of reuse, because the *service* is what should be shared and the
*endpoints* are what should be specific. §16's `RecipeSearchService` is the
reuse; three thin endpoints over it are the API.

### 17.2 `GET /api/v1/recipes` — extended

Query parameters are **unchanged**. Nothing a client sends today breaks; the
query understanding happens server-side on the `query` value that is already
there.

```
GET /api/v1/recipes
  ?householdId=0193f1…
  &query=vegetarisches%20Abendessen%20unter%2030%20Minuten%20mit%20Kartoffeln
  &sort=relevance
  &limit=24
```

The response gains three optional fields:

```jsonc
{
  "items": [
    {
      "recipeId": "01942e…",
      "title": "Kartoffelgratin",
      "imageId": "01942f…",
      "totalMinutes": 25,
      "yieldAmount": 4, "yieldKind": "servings",
      "tags": ["auflauf", "vegetarisch"],
      "cookCount": 3,
      "updatedAt": "2026-08-02T18:41:00Z",

      // NEW — why this recipe is here. Present only when the match was NOT a
      // plain title match, because "it matched the title" is not worth a line
      // of interface.
      "matchReason": { "kind": "ingredient", "term": "Kartoffeln" }
    }
  ],
  "nextCursor": "eyJ0IjoxLCJ…",
  "total": 6,

  // NEW — what the query was understood to mean. Every entry is renderable as
  // a removable chip, and `remove` is the query string that drops it, so the
  // client does not re-implement the parser to undo a chip.
  "interpretation": {
    "freeText": "",
    "applied": [
      { "kind": "diet",   "value": "vegetarian", "label": "Vegetarisch", "confidence": "certain" },
      { "kind": "meal",   "value": "dinner",     "label": "Abendessen",  "confidence": "likely"  },
      { "kind": "time",   "value": 30,           "label": "< 30 Min.",   "confidence": "certain" },
      { "kind": "ingredient", "value": "kartoffel", "label": "mit Kartoffeln", "confidence": "certain" }
    ],
    // Set when the ladder in §22 had to change the query to find anything.
    "relaxed": null,
    "correctedFrom": null
  },

  // NEW — refinements worth offering, computed from THIS result set. No
  // curation: they are the facets that best split what was actually found.
  "facets": {
    "tags":    [ { "slug": "auflauf", "label": "Auflauf", "count": 3 },
                 { "slug": "salat",   "label": "Salat",   "count": 2 } ],
    "time":    [ { "maxMinutes": 20, "count": 2 } ],
    "cuisines":[ { "value": "italian", "label": "Italienisch", "count": 2 } ]
  }
}
```

`interpretation` and `facets` are omitted entirely when `query` is absent, so
the plain library listing is byte-for-byte what it is today.

**`remove` semantics.** Each chip's removal is expressed by the client sending
the query with that span deleted — so the server returns, per entry, the
character span it consumed (`"span": [0, 14]`). The client slices the string.
That keeps one parser, on the server, and makes chip removal a pure string
operation in the UI. It is also what makes backspace-to-remove (§18.4) exact.

### 17.3 `GET /api/v1/households/{householdId}/suggestions`

Autocomplete. A household sub-resource, matching the existing
`GET /households/{householdId}/ingredients`.

```
GET /api/v1/households/0193f1…/suggestions?q=h%C3%A4h&limit=8
```

```jsonc
{
  "items": [
    { "kind": "recipe",     "label": "Hähnchen-Curry",  "recipeId": "01942…",
      "imageId": "01943…",  "totalMinutes": 35 },
    { "kind": "recipe",     "label": "Hähnchenbrust mit Rosmarinkartoffeln",
      "recipeId": "01944…", "imageId": null, "totalMinutes": 40 },
    { "kind": "ingredient", "label": "Hähnchenbrust", "recipeCount": 4 },
    { "kind": "ingredient", "label": "Hähnchenschenkel", "recipeCount": 1 },
    { "kind": "tag",        "label": "Hähnchen", "slug": "haehnchen", "recipeCount": 6 },
    { "kind": "refinement", "label": "Hähnchen · unter 30 Min.",
      "query": "Hähnchen unter 30 Minuten" }
  ]
}
```

Four entity kinds, visually distinct in the UI (§18.3):

- `recipe` — go straight there. Enter on one of these opens the recipe.
- `ingredient` — from the household's own ingredient names, ranked by use.
- `tag` — from `tags`, with counts, which the filter bar already reads.
- `refinement` — a *query completion*, not a destination: it fills the box.

Never cached (`Cache-Control: no-store`) and never logged with its `q` (§21).

**Amended after building it.** The route is
`GET /api/v1/households/{householdId}/completions?query=…`, not
`…/suggestions`: `GET /suggestions` already is the "what should I cook?"
ranking, and one name for two unrelated answers would be read as one feature.
A refinement comes back as `{ label, maxMinutes, recipeCount }` rather than a
finished query string, so the client words it in the reader's language —
"Hähnchen unter 30 Minuten" and "chicken under 30 minutes" parse alike. Only
the words still being typed are completed: `vegetarisch häh` completes `häh`.
Ingredient names carry their folds as generated columns (migration 0020); folded
at query time they cost 75 ms at p95 over two thousand recipes, and 6 ms stored.

### 17.4 `GET /api/v1/recipes/{recipeId}/related`

```
GET /api/v1/recipes/01942e…/related?limit=6
```

```jsonc
{
  "items": [
    { "recipeId": "0194…", "title": "Lasagne Bolognese",
      "imageId": "0194…", "totalMinutes": 90,
      "reason": { "kind": "concepts", "shared": ["pasta", "italian", "beef"] } },
    { "recipeId": "0194…", "title": "Chili con Carne",
      "imageId": null, "totalMinutes": 50,
      "reason": { "kind": "ingredients", "shared": ["Hackfleisch", "Tomaten", "Zwiebel"] } }
  ]
}
```

A sub-resource of a recipe, ETag-able, and the same service underneath (§19.1).
`reason` is not decoration: "shares Hackfleisch, Tomaten, Zwiebel" is a better
explanation of a suggestion than any number, and it is free.

**As built (culina-v2-0r34.3.1).** Three things differ from the sketch above.
There is no `limit`: the only reader is the shelf under a recipe, which holds
three, so the server returns up to three and the page shows nothing below
three. The kind is `kinds` rather than `concepts`, because ingredients are
concepts too — `kinds` is what the two recipes *are*, `ingredients` what they
are *made from*. And every item is a full recipe summary (yield, tags, the
reader's cook count), so the shelf draws the same card as every other list.
`shared` holds at most three words, already in the language of the recipe
being read. It is not ETag-able after all: nothing versions "the rest of the
household", so it is uncached, like the recipe's cookbooks. It replaced
`GET /suggestions?likeRecipeId=` as the source of the recipe page's "Similar
recipes"; whether the ranker's `like` purpose stays is culina-v2-8tff.

### 17.5 Errors

Unchanged. RFC 9457 problem documents, `QueryParameterGuardMiddleware` still
rejects unknown and duplicated parameters with 400, and — importantly — **there
is no new error code for "search failed"**. §22's contract is that search
degrades rather than errors; a lane that fails is a lane that contributed
nothing.

### 17.6 The OpenAPI and client path

`make api` regenerates `src/backend/openapi/Api.json` and the frontend client
(`frontend-api-client`). The three new response types flow through
`openapi-typescript` into `schema.d.ts`; no generated code is edited, and no
component imports the generated types directly.

---

## 18. Frontend and interaction design

### 18.1 The principle

> **Search is not a place. It is a state the app can be in, over whatever you
> were already doing.**

Everything below follows from that one sentence. A `/search` route is the wrong
shape: it navigates away from a page the user chose to be on, it puts the
results in the browser history where the back button turns them into
archaeology, and it means finding a recipe while planning Thursday costs you
Thursday. Culina's own
[`RecipePicker`](../src/frontend/src/lib/features/recipes/RecipePicker.svelte)
already got this right — a sheet with its own store, so the page behind it is
untouched — and the global search is the same idea promoted to the whole app.

The principles being applied, none of which is about looking like anything:

| | In practice here |
| --- | --- |
| **Immediacy** | Results change as the query does. No submit button. 120 ms to suggestions, 200 ms to results. |
| **Continuity** | The field you typed into becomes the field at the top of the overlay. Nothing jumps. |
| **Spatial consistency** | The overlay expands from the search field's real position, and collapses back into it. |
| **Progressive disclosure** | Chips appear only when something was inferred. Match reasons appear only when they are not obvious. Advanced syntax exists and is never mentioned. |
| **Preserved context** | Closing the overlay restores the page underneath exactly, including scroll, including a half-written recipe. |
| **Minimal load** | One field. Everything else is a consequence of what is in it. |
| **Meaningful motion** | Motion says where things came from. Nothing animates for decoration, and everything respects `prefers-reduced-motion`. |

### 18.2 Entry points

| Surface | Entry | Behaviour |
| --- | --- | --- |
| Anywhere, desktop | `⌘K` / `Ctrl-K` | Overlay opens centred, field focused |
| Anywhere, desktop | `/` (when not in a field) | Same. The habit every developer already has. |
| Library, cookbook | the existing `SearchField` | Focusing it *expands* it into the overlay, in place |
| Anywhere, mobile | search icon in the app bar | Overlay rises from the bottom, keyboard follows |
| Recipe detail | `⌘K` | Overlay opens over the recipe; closing returns to the same scroll position |
| Plan, shopping | `RecipePicker` | Unchanged sheet, but now with the same retrieval, chips and empty states |
| Cook mode | **nothing** | Deliberately. Hands are covered in flour and the screen is a recipe. |

Cook mode having no search is a decision, not an omission: `README.md`'s third
commitment is that the app never loses your place, and a search box on the
cooking surface is an invitation to lose it.

### 18.3 The overlay

Empty, before anything is typed — the state most people see most often:

```
┌──────────────────────────────────────────────────────────────┐
│  🔍  Rezepte durchsuchen                                  esc │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│  ZULETZT GESUCHT                                             │
│    ↩  Bolognese                                              │
│    ↩  vegetarisch unter 30 Minuten                           │
│    ↩  Kartoffeln                                             │
│                                                              │
│  SCHNELLZUGRIFF                                              │
│    ⏱  Unter 30 Minuten      🌱  Vegetarisch                  │
│    🍳  Frühstück            📖  Zuletzt gekocht              │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

Recent searches live in `localStorage` and never reach the server (§21).
"Schnellzugriff" is four constant filters, not a personalised shelf — a panel
whose contents change before you have typed anything is a panel you cannot
learn.

Mid-query, `häh`, after 120 ms:

```
┌──────────────────────────────────────────────────────────────┐
│  🔍  häh|                                                 esc │
├──────────────────────────────────────────────────────────────┤
│  REZEPTE                                                     │
│   ▸ [img]  Hähnchen-Curry                          35 Min.   │
│     [img]  Hähnchenbrust mit Rosmarinkartoffeln    40 Min.   │
│     [—  ]  Gebackenes Hähnchen                     1 Std.    │
│                                                              │
│  ZUTATEN                                                     │
│     🥕  Hähnchenbrust                            4 Rezepte   │
│     🥕  Hähnchenschenkel                         1 Rezept    │
│                                                              │
│  SCHLAGWÖRTER                                                │
│     #  Hähnchen                                  6 Rezepte   │
│                                                              │
│  VORSCHLÄGE                                                  │
│     ⏱  Hähnchen · unter 30 Min.                              │
└──────────────────────────────────────────────────────────────┘
```

Four entity kinds, visually separated and differently actioned — a recipe is a
destination, an ingredient and a tag are *filters*, and a suggestion completes
the query. Mixing them into one list is the most common autocomplete mistake:
it makes Enter mean four different things.

Full query, `vegetarisch unter 30 minuten mit kartoffeln`:

```
┌──────────────────────────────────────────────────────────────┐
│  🔍  vegetarisch unter 30 minuten mit kartoffeln|         esc │
├──────────────────────────────────────────────────────────────┤
│  [🌱 Vegetarisch ×] [⏱ < 30 Min. ×] [🥕 mit Kartoffeln ×]    │
│                                                     6 Rezepte│
├──────────────────────────────────────────────────────────────┤
│   ▸ [img]  Kartoffelgratin                         25 Min.   │
│            🌱 vegetarisch · Auflauf                          │
│                                                              │
│     [img]  Kartoffelsalat mit Radieschen           20 Min.   │
│            🌱 vegetarisch                                    │
│                                                              │
│     [—  ]  Rösti                                   30 Min.   │
│            ⚬ vermutlich vegetarisch                          │
│                                                              │
├──────────────────────────────────────────────────────────────┤
│  EINGRENZEN   [Auflauf 3]  [Salat 2]  [unter 20 Min. 2]      │
└──────────────────────────────────────────────────────────────┘
```

Three details are doing real work.

**The chips are the parser's confession.** They say exactly what the system did
with the sentence, and each has an `×`. This is the single highest-leverage
piece of interface in the design: without it, natural-language parsing is a
system that silently changes what you asked for, and the first time it guesses
wrong the user stops trusting the box. With it, a wrong guess is one tap to
fix, and *seeing the parse teaches people the vocabulary* — someone who sees
`unter 30 Minuten` become a chip once will type it again on purpose.

**`⚬ vermutlich vegetarisch`** is §7.5's uncertainty, surfaced rather than
hidden. Tapping into Rösti shows the one-tap confirmation that makes it
permanent.

**The refinement chips are facets, not curation.** They are computed from the
returned candidate set — the tags, time bands and cuisines present, ranked by
which best splits it. So `Hähnchen` offers `[Asiatisch] [Ofengericht]
[unter 30 Min.]` and `Frühstück` offers `[Süß] [Herzhaft] [Schnell]` with no
rules written for either, because those are what is actually in the results.
They appear only when a facet covers between roughly 20 % and 80 % of the set:
a chip that filters nothing out, or everything, is a chip that wastes a tap.

### 18.4 Keyboard

The whole model, and it is small on purpose:

| Key | Does |
| --- | --- |
| `⌘K` / `Ctrl-K` / `/` | Open |
| `Esc` | Close, or clear the query first if there is one — matching the existing `SearchField` |
| `↑` `↓` | Move through results *and* suggestion groups, skipping headings |
| `⏎` | Open the highlighted recipe, or apply the highlighted filter, or accept the highlighted completion |
| `⌘⏎` | Open in a new tab |
| `Tab` | Accept the top suggestion into the field, without navigating |
| `⌫` on an **empty** query | Remove the last chip |
| `⌘1`–`⌘9` | Jump to the *n*-th result |

`⌫` removing the last chip is the detail that makes chips feel native rather
than decorative. It works because §17.2 returns each chip's character span, so
removing one is slicing the query string — the same operation the user would
have done themselves, done for them.

### 18.5 The result row

```
┌──────────────────────────────────────────────────────┐
│ ┌──────┐  Hähnchen-Curry                             │
│ │ img  │  35 Min. · 4 Portionen                      │
│ └──────┘  🌱 —      #asiatisch #schnell              │
│           Zutat: Hähnchenbrust            ← only when it is NOT a title match
└──────────────────────────────────────────────────────┘
```

The match reason is the interesting element, and it is under-used in most
search interfaces. It answers *"why is this here?"* — the question a user asks
silently about every result they did not expect — and it costs one quiet line
that is absent in the common case. `Zutat: Hähnchenbrust` · `Schritt 4` ·
`Ähnlich: Ragout` · `Korrigiert: Bolognese`. A tier-D result **always** carries
one, which is the interface half of §11.1's guarantee: an associative match
looks different from a real one.

Matched spans in the title are emphasised with a weight change, not a
background highlight. A yellow bar through a title is a search engine's
affordance; a heavier stem is a recipe app's.

### 18.6 Motion

| Transition | Treatment |
| --- | --- |
| Field → overlay | The field's rectangle grows into the panel's; a `view-transition` on desktop, a spring on the sheet for mobile. 180 ms. |
| Results changing | Rows cross-fade in place; they never slide or re-stagger. A list that re-animates on every keystroke is unreadable while typing. |
| Chip added | Scales from 0.9 with a 120 ms fade, from the caret's position. |
| Chip removed | Fades and collapses width; the row reflows once, not per chip. |
| Overlay → closed | Collapses back into the field it came from. |
| `prefers-reduced-motion` | Every duration becomes 0. Nothing else changes; no transition is load-bearing. |

The results-never-slide rule matters more than it sounds. Search-as-you-type
means the list is redrawn four or five times per query; any motion that moves
rows vertically makes the list impossible to read while it is happening, and
the user stops typing to wait — which is the exact opposite of what
search-as-you-type is for.

### 18.7 Mobile

- The overlay is a sheet from the bottom. The field sits at the **top** of the
  sheet but the results start at the thumb; the keyboard covers the lower
  third, so the first three results sit in the band between.
- Chips are a horizontally scrollable row that does not wrap, with a fade on
  the trailing edge. Wrapping chips push results below the fold.
- Refinements are the same row, after the chips, differentiated by weight.
- Pull-to-dismiss on the sheet, `Esc` equivalent.
- The existing bottom navigation gains no sixth destination.
  [`navigation-research.md`](navigation-research.md) settled on five and the
  reasoning holds; search is a search icon in the app bar, which is where a
  thumb reaches for it and where it does not compete with a workflow.
- **The field never moves once the keyboard opens.** On iOS Safari a focused
  input that scrolls into view drags the whole page; the sheet is
  `position: fixed` with the viewport units the visual viewport reports, which
  is the only way this behaves.

### 18.8 Integration into the rest of the app

| Surface | What search gives it |
| --- | --- |
| **Library (home)** | The toolbar field becomes an overlay trigger. Applied chips persist in `libraryView` so a recipe round trip keeps them, exactly as `query` does today. |
| **Cookbook detail** | Same overlay, scoped by `cookbookId`. The scope shows as a non-removable leading chip: `[📖 Sonntagsbraten]`. |
| **Recipe detail** | An "Ähnliche Rezepte" rail at the foot, from `/recipes/{id}/related` (§19.1). Loaded when it scrolls into view — `whenVisible.ts` already exists. |
| **Plan** | `RecipePicker`, unchanged in shape, now with chips and the recovery ladder. "Vegetarisch Donnerstag" works in the picker. |
| **Shopping** | Same picker. |
| **Import** | Duplicate detection (§19.2): "Du hast vielleicht schon *Spaghetti Bolognese*." |
| **Recipe editor** | Tag field suggests tags the lexicon derived but the household has not applied (§19.3). |
| **Cookbooks** | "Aus dieser Suche eine Sammlung machen" turns a query with chips into a smart cookbook's rules — the rule editor already speaks tags, ingredients and `maxMinutes`. |

That last one is the strongest product connection in the document. A smart
cookbook is a *saved search*, and the rule editor's three fields are three of
the chips the parser already produces. Making the overlay able to save itself
as a shelf is one button and no new concepts.

**Amended after building it.** Saving a search is *not* making a smart
cookbook, and the reason is concrete rather than philosophical. A cookbook
card carries a recipe count and a cover mosaic, and both are computed per card
from `SmartShelfSql.Matches` against the shelf's rule columns. Tags,
ingredients and a time ceiling are cheap predicates there. **Free text is not**
— it is the four-lane relevance engine of §8, and putting it in a shelf rule
would mean running that engine once per cookbook on the cookbooks page, or
maintaining a second, cheaper definition of "matches" that would immediately
disagree with the first. That is exactly the drift `SmartShelfSql` was written
to prevent.

So the two stayed separate, and the separation turned out to be honest rather
than merely convenient: **a shelf is a curation** — a set, countable, drawable,
shoppable — **and a search is a lens** — ordered, fuzzy, and only meaningful
while you are looking through it. A saved search is its own small household
resource (`/api/v1/searches`, migration `0016`) holding exactly what the
library's toolbar holds: `query`, `tags`, `maxMinutes`, `sort`. Saving is a
copy rather than a translation, which is the only thing that makes a saved
search reopen as the search that was saved.

The connection survives as one door between them, and it only opens one way:
a saved search whose dimensions a shelf *can* hold — tags, a time limit —
offers "make a cookbook from it", which opens the ordinary `CookbookSheet`
prefilled. A search that is only words does not offer it, and says why. Nothing
is dropped silently.

One order is deliberately not storable: `cookbookOrder` needs a cookbook to be
an order of, and a saved search is applied from the library. `SearchOrders`
lists the six that are, and an integration test holds `GET /recipes` to
accepting every one of them — which is what stops the two vocabularies drifting
the way `RecipeFilters.sort` had already drifted (`culina-v2-5l6`: the client
declared `match`, and the API answered 400).

### 18.9 Client state

```typescript
// lib/features/search/stores/search.svelte.ts
class SearchStore {
  query        = $state('');
  open         = $state(false);
  suggestions  = $state<Suggestion[]>([]);
  results      = $state<RecipeSummary[]>([]);
  interpretation = $state<Interpretation | null>(null);
  facets       = $state<Facets | null>(null);
  highlighted  = $state(0);

  // Its own store, like RecipePicker's, so searching from the plan page does
  // not empty the plan page.
  #recipes = createRecipeStore();

  // The same out-of-order guard the recipe store already carries: a short
  // query matches more rows and answers later, so without this the list
  // settles on whatever the slowest request said.
  #token = 0;
  #inflight: AbortController | null = null;
}
```

Two debounces, not one, because the two requests cost different amounts:

```
keystroke ──┬── 120 ms ──▶ GET …/suggestions      (cheap: prefix on small tables)
            └── 200 ms ──▶ GET /recipes?query=…   (four lanes)
```

Both below the current 250 ms, which was chosen when a search was a page load.
Suggestions at 120 ms feel like the field is completing the word; results at
200 ms arrive as a thought finishes.

Two more rules carried over from the existing store because they are already
correct: a refetch **keeps the current list on screen** rather than showing a
skeleton, and "nothing matched" is only shown once an answer has actually
arrived — never between two keystrokes.

### 18.10 Accessibility

- `role="combobox"` with `aria-expanded`, `aria-controls` and
  `aria-activedescendant` on the field; the list is `role="listbox"`, entries
  are `role="option"`. Arrow keys move `aria-activedescendant`; focus stays in
  the field, which is what lets a screen-reader user keep typing.
- Result count in an `aria-live="polite"` region, debounced to 500 ms so it
  announces once per pause rather than once per keystroke. The library page
  already does exactly this.
- Chips are buttons with accessible names of the form
  `Filter „Vegetarisch" entfernen`.
- The overlay is a focus trap with `aria-modal`; `Esc` returns focus to the
  trigger.
- Match-reason lines are associated to their result via `aria-describedby`, so
  the reason is read after the title rather than interrupting it.
- Every interactive target ≥ 44 × 44 px, per
  [`accessibility-and-performance.md`](accessibility-and-performance.md).

### 18.11 Weight

| | gzipped |
| --- | --- |
| `SearchOverlay.svelte` | ~4.5 kB |
| `SearchResult.svelte`, `SearchChip.svelte`, `SuggestionGroup.svelte` | ~2.5 kB |
| `search.svelte.ts` | ~2 kB |
| Message keys, DE + EN | ~1 kB |
| **Total** | **~10 kB** |

Against 42 kB of headroom in the 140 kB budget. The overlay is dynamically
imported on first open, so it is not in the initial bundle at all and the
first-load budget is untouched.

---

## 19. One intelligence layer: related recipes, duplicates, tagging, shelves

There is no recommendation system in Culina today. That is an opportunity
rather than a gap: five features that would otherwise each grow their own
half-clever matching logic can be one service, because they are all the same
question with a different input.

```
                     ┌──────────────────────────────┐
                     │  recipe_search_documents     │
                     │  concepts · document ·       │
                     │  fuzzy_text · diet · minutes │
                     └───────────────┬──────────────┘
                                     │
        ┌──────────────┬─────────────┼──────────────┬───────────────┐
        ▼              ▼             ▼              ▼               ▼
    SEARCH         RELATED       DUPLICATE       TAGGING        SMART
    §8–11          §19.1          §19.2          §19.3         SHELVES §19.4
   query text    recipe as      candidate      concepts      query → rules
                 the query      as the query   not yet tags
```

### 19.1 Related recipes

The recipe's own `concepts` array becomes the query. Everything else is the
existing machinery.

```sql
select d.recipe_id, d.title,
       cardinality(array(select unnest(d.concepts) intersect select unnest(@concepts)))
         ::float / cardinality(@concepts)                       as concept_overlap,
       (select count(*) from recipe_ingredients ri
        join ingredient_groups g on g.id = ri.group_id
        where g.recipe_id = d.recipe_id
          and ri.name = any(@ingredientNames))::float
         / greatest(cardinality(@ingredientNames), 1)           as ingredient_overlap
from recipe_search_documents d
where d.household_id = @householdId
  and d.recipe_id <> @recipeId
  and d.concepts && @concepts
order by (concept_overlap * 0.6 + ingredient_overlap * 0.4) desc,
         d.updated_at desc
limit @limit;
```

`0.6 / 0.4` toward concepts, because shared *concepts* mean "the same kind of
thing" and shared *ingredients* mean "made from the same stuff" — and a person
looking at Bolognese is more interested in a Lasagne than in a Chili that
happens to share three tins.

The reason is returned and shown: `Teilt Hackfleisch, Tomaten, Zwiebel`. A
recommendation you can see the reason for is a recommendation you can disagree
with, which is what makes it feel like a tool rather than a slot machine.

**As built (culina-v2-0r34.3.1), measured on the golden library.** The query
above does not survive contact with `concepts`, which carries every concept
*with its ancestors*: nearly every recipe is a vegetable dish of some sort, so a
plain overlap fraction makes everything related to everything. What shipped
(`Infrastructure/Persistence/Recipes/RelatedRecipes.cs`):

- The 0.6 / 0.4 split is kept, but both halves are concepts: 0.6 for what a
  recipe *is* (dish, cuisine, meal, method, diet, character), 0.4 for what it is
  *made from* (ingredient concepts). Raw ingredient-name equality was dropped —
  the lexicon already knows that *passierte Tomaten* and *Tomaten* are one thing.
- Every shared concept is weighted by its inverse document frequency in the
  household, `ln((N + 1) / (df + 1))`, and each half is the share of the first
  recipe's own weight the other matches. What every recipe carries counts for
  nothing; what two share with few others counts most. No stop-list, which
  would be wrong in a kitchen that cooks nothing but soup.
- A floor of 0.1 on the combined score. At 0.15 a salmon recipe lost the fish
  tacos and the Frikadellen lost every other mince recipe; at 0.1 all sixty
  golden recipes have at least three, each with a reason.
- A reason names only *telling* concepts — carried by at most half the
  kitchen — drops a concept that is only there as an ancestor of another shared
  one ("Hähnchen", never "Hähnchen, Geflügel, Fleisch"), and names at most
  three. A candidate with nothing telling to say is not offered.

The design's own example holds: Spaghetti Bolognese → Lasagne Bolognese, the
Bolognese sauce and the Ragù, with Chili con Carne below them.

### 19.2 Duplicate detection at import

The most valuable of the five, and the one with a live use case: import is
under active development (`culina-v2-hnyr`, Tandoor and Mealie), and importing
a library twice is how a household ends up with four Bolognese recipes.

Before writing an imported recipe, run its title through lanes 0 and 3:

```
a folded title column equal                            → certain duplicate
word_similarity(title) ≥ 0.85 AND ingredient overlap ≥ 0.7  → probable
concept overlap = 1.0 AND ingredient overlap ≥ 0.8     → possible
```

and surface it in the import review that already exists:

```
   ⚠  Spaghetti Bolognese
      Sieht aus wie „Spaghetti Bolognese" (12× gekocht, 4 gleiche Zutaten)
      [ Trotzdem importieren ]  [ Überspringen ]  [ Vergleichen ]
```

Never automatic. A household may genuinely want two Bolognese recipes, and
silently dropping one is unrecoverable. This costs one query per imported
recipe against an index that already exists.

**As built (culina-v2-0r34.3.2).** The check runs in the import worker, just
before a recipe that has been read and mapped would be written
(`RecipeImporter` → `ILookalikeRecipes`), because the browse only has titles
and the second and third tests need ingredients. The three tests are as above,
with two changes:

- *Almost the same name* is `word_similarity` taken in both directions, so
  "Omas Spaghetti Bolognese" finds "Spaghetti Bolognese" and the reverse.
- The two tests that do not rest on the name also need **at least three shared
  ingredients**. Without it, two small recipes with the same few ingredients —
  and every fixture recipe of the import tests, which is salt and nothing else
  — read as one dish. The same name is always worth asking about, so the first
  test does not need it.

Ingredient overlap is shared folded names over the larger of the two lists.
A recipe that looks like one here is held back as `looks_like`, naming the
recipe it resembles, how often the importer has made it and how many
ingredients they share, and nothing about it is written — not even its origin
row, so asking for it again brings it over as if the first time. The review
lists the held ones unticked, each with a link to the recipe it resembles, and
"import anyway" starts a second run of just the ticked ones with
`allowLookalikes`, landing on the first run's cookbook (`cookbookId`) instead
of a second shelf with the same name.

Measured with `IntegrationTests/Import/LookalikeRecipeTests` — ten near
duplicates and nine near misses of golden-library recipes, the ambiguous pairs
left out rather than ruled on: recall 1.00, no false positive.

### 19.3 Tag suggestions

When editing, offer the concepts the document carries that the recipe is not
tagged with:

```
Schlagwörter   [pasta ×] [italienisch ×]
Vorschläge     + Auflauf   + Ofengericht   + Hauptgericht
```

Suggested, never applied. The household's tags stay the household's, which is
what §12.4 depends on: if the lexicon started writing tags, the "household
vocabulary beats the lexicon" rule would be the lexicon beating itself.

**As built (culina-v2-0r34.3.3).** The editor had no tag field at all — a
recipe's tags came only from imports and were carried through every save
untouched — so this shipped one: the kitchen's tags through the same
`TagChooser` the filters and smart-cookbook rules use, a field for a new one,
and the suggestions beneath, from `GET /recipes/{recipeId}/tag-suggestions`,
asked again after each save. What is offered:

- Only what a recipe *is* — dish, cuisine, meal, method, diet. Not ingredients,
  which the search finds without a tag, and not characters, since "warm" is
  true of half of everything.
- Only what the recipe names and one step above it: a Lasagne is offered
  Lasagne, Auflauf and Italienisch, not everything a Lasagne is ultimately a
  kind of.
- Nothing its tags already name, read the query way (`Recognise`, no
  ancestors), so a recipe tagged "Lasagne" is still offered "Auflauf".
- The household's own tag wherever one of its tags is a name of the concept —
  its lower-case "italienisch" rather than the lexicon's "Italienisch" — sent
  with its slug, those first; otherwise the lexicon's word in the recipe's
  language. At most five.

### 19.4 Smart cookbooks from a search

The overlay's chips *are* a smart cookbook's rules. `[Vegetarisch] [< 30 Min.]`
is `{ tags: [vegetarisch], maxMinutes: 30 }`, which is exactly the shape
[`0010_smart_cookbooks.sql`](../src/backend/src/Infrastructure/Persistence/Migrations/0010_smart_cookbooks.sql)
stores. One button in the overlay footer — "Als Sammlung speichern" — turns a
search into a shelf, and `SmartShelfSql` keeps the shelf and the search
agreeing about what a tag means, as it already does.

### 19.5 Contextual suggestions on the home screen

Only where they are honest and labelled:

- **"Zuletzt gekocht"** — `cook_log_entries`, no inference at all.
- **"Lange nicht gekocht"** — cooked more than once, not in 90 days. A genuinely
  good shelf that needs no model.
- **"Passt zu dieser Woche"** — recipes sharing ingredients with what is already
  on the meal plan, so one shop covers more. Uses `concepts` and the existing
  ingredient overlap.

Each is a labelled shelf with a stated reason, not a mystery feed. What is not
built: anything that reorders the library itself based on inferred preference.
The library is the household's collection in the order they last touched it,
and that is a promise.

---

## 20. Performance and resources

### 20.1 The reference machine

What Culina is actually installed on: a four-core x86 mini-PC (N100 class) or a
Raspberry Pi 5, 8–16 GB of RAM shared with three or four other containers, an
SSD, no GPU. PostgreSQL gets its defaults. Two to eight users, of whom at most
two are awake and searching at once.

### 20.2 Query budget

Per lane, measured shape rather than a benchmark, for a 2,000-recipe household —
roughly four times the realistic upper bound:

| Stage | 200 recipes | 2,000 | 10,000 |
| --- | --- | --- | --- |
| Parse (normalise + grammar + expand) | 0.4 ms | 0.4 ms | 0.4 ms |
| Lane 0 — exact/prefix, btree | 0.1 ms | 0.2 ms | 0.3 ms |
| Lane 2 — FTS, GIN | 1 ms | 3 ms | 9 ms |
| Lane 4 — concepts, GIN array | 0.7 ms | 2 ms | 6 ms |
| **Lane 3 — trigram, GIN** *(conditional)* | 3 ms | **12 ms** | 45 ms |
| Filter + tier + score | 0.6 ms | 2 ms | 8 ms |
| Facets | 1 ms | 3 ms | 11 ms |
| Serialise | 0.5 ms | 0.5 ms | 0.5 ms |
| **Typical (lane 3 skipped)** | **4 ms** | **11 ms** | **35 ms** |
| **Worst (lane 3 runs)** | **7 ms** | **23 ms** | **80 ms** |

Against the §3 R8 target of 60 ms p95: comfortable at every realistic size, and
still inside budget at five times the realistic maximum.

**Lane 3 is the whole cost curve**, which is why §8.4 makes it conditional. It
runs when the cheap lanes did not find enough — which is the typo and compound
case, where it is the only lane that can help, and where the user has already
accepted that this search is a harder question.

Autocomplete is a different, cheaper query — prefix on `title_ae`, plus two
small aggregates over `recipe_ingredients` and `tags`:

| | 200 | 2,000 | 10,000 |
| --- | --- | --- | --- |
| Suggestions p50 | 1.5 ms | 4 ms | 12 ms |
| Suggestions p95 | 3 ms | 9 ms | 28 ms |

### 20.3 Perceived latency

The number the user experiences is not the number in the table:

```
keystroke
   ├─ 120 ms debounce            ← deliberate, and the dominant term
   ├─   1 ms request queue
   ├─   4 ms suggestion query
   ├─   3 ms serialise + network (same origin, loopback in most installs)
   └─  16 ms frame
   ≈ 145 ms from last keystroke to updated suggestions

   results, 200 ms debounce + 11 ms → ≈ 230 ms
```

Both under the ~250 ms threshold at which a UI stops feeling like a direct
response and starts feeling like a request. The debounces are the largest term
in both, which is the right place for the time to go: it is time spent waiting
for the user to finish a word, not time spent computing.

### 20.4 Write cost

| Operation | Added |
| --- | --- |
| Create a recipe | +2 ms (build + upsert one row) |
| Update a recipe | +3 ms |
| Delete | 0 (cascade) |
| Import 200 recipes | +0.6 s over a batch that already takes ~40 s |
| Migration backfill, 2,000 | ~4 s, once |
| Lexicon-change reindex, 2,000 | ~4 s at startup, batched, non-blocking |

A recipe save is a form submit that already costs 30–60 ms. Adding 3 ms to it
is not perceptible; the alternative — a background job — would add a window in
which a saved recipe is unfindable, and an operational surface for a queue.

### 20.5 Memory and storage

| | Before | After |
| --- | --- | --- |
| API process RSS | ~120 MB | ~124 MB (the lexicon is ~320 records ≈ 200 KB, plus compiled regexes) |
| PostgreSQL shared buffers | unchanged | unchanged |
| Index storage, 2,000 recipes | — | +6.4 MB |
| Container image | unchanged | unchanged |
| Container count | 1 | 1 |
| Volumes | 2 | 2 |

For the comparison that matters: Architecture D would add ~120 MB to the image
and ~250 MB of RSS, on a machine where RAM is the scarce resource, to improve
about one query in a hundred.

### 20.6 Caching

**Almost none, deliberately.**

- No result cache. At 11 ms a cache saves 11 ms and introduces the question of
  when it is wrong. A recipe saved on the tablet must be findable on the phone
  immediately; that is §16.2's whole argument, and a cache would give it back.
- No ETag on search results. Results depend on the caller (cook counts) and on
  every recipe in the household; the validator would be invalidated by every
  write and the 304 rate would be near zero. `http-caching-etags` reserves
  ETags for reads with a real version, and search has none.
- `Cache-Control: no-store` on suggestions — they contain query text (§21).
- The **lexicon** is compiled once at startup into frozen dictionaries. That is
  the only cache, and it is immutable for the life of the process.
- PostgreSQL's own buffer cache does the real work: a 6.4 MB index is resident
  after the first few queries and stays there.

### 27.6 The candidates, found by the indexes (culina-v2-p65e)

Phase 1 built four indexes on `recipe_search_documents` and no read ever used
one. The lanes were one `or`, and an `or` whose operands read columns of the
`q` CTE is a condition no index can serve — so every text search read every
document in the household, and the cost was linear in the size of the library.
Two of the lanes could not have been index-qualified in any shape, because they
correlated a LIKE pattern with an `unnest`.

The candidates are now a `union`, one branch per lane, each over its own index,
with the query spelt out in each branch instead of read from `q`. Npgsql sends
parameters with an unnamed statement, which PostgreSQL plans knowing their
values, and the fold functions are immutable, so the patterns are constants by
the time the planner looks. Two lanes of the old `or` — the title prefix and
the title word — are inside the substring lane whenever the query has a term,
because the title is the first thing in `fuzzy_text` in both folds; they now
run only for a query with no term at all (`Ei`, `und`).

Two more changes came out of measuring what was left:

- **The typo lane has an index** (migration 0018, a trigram GIN on `title_ae`).
  It is reached through `<%`, whose threshold is a setting rather than an
  argument, so every connection sends `pg_trgm.word_similarity_threshold` at
  startup from `RecipeSearcher.FuzzyThreshold`. The explicit
  `word_similarity(…) >= @fuzzyThreshold` stays and is still the rule; the
  setting only decides what the index hands back. The golden set's typos sit at
  0.58 and 0.54, under the default of 0.6, so a connection that lost the
  setting would fail those tests rather than quietly find less.
- **Lexical evidence is only asked of a document the query matches.**
  `ts_rank_cd` searches the whole document for a cover even when there is none,
  which is most candidates — the substring lane brings in every compound, and a
  compound is exactly what the stemmer cannot see. And "did it match in the
  title band" is now `ts_filter(document, '{a}') @@ tsq` rather than a rank
  with three weights zeroed, at a twentieth of the cost. Checked over all
  12,200 bench documents for 21 queries, phrases and `or` among them, it gives
  the same answer for every document the query matches — once a query of
  nothing but exclusions (`-reis`), which matches almost everything and names
  no band, is kept out through `querytree`.

The one change of behaviour is a query with a minus in it. The cover-density
rank never evaluated the minus, so `Tomaten -Reis` gave a recipe containing
Reis lexical credit for "Tomaten" and let it come first. It now earns none.
The substring lane still finds it, so the minus demotes rather than removes —
`RecipeSearchRelevanceTests` holds that order.

Measured through `GET /api/v1/recipes`, page 1 of 20, median of 11, on a
synthetic library whose words are about as selective as a real one (a staple
in most recipes, any other ingredient in about one in ten):

| Query | 200 before → after | 2,000 before → after | 10,000 before → after |
| --- | --- | --- | --- |
| `Bolgnese` | 8.7 → 7.2 ms | 25.0 → 13.0 | 90.4 → **14.2** |
| `Linsensuppe` | 8.5 → 6.3 | 29.8 → 15.0 | 104.9 → **21.6** |
| `qwertzuiop` | 7.4 → 6.6 | 16.3 → 10.0 | 41.0 → **8.6** |
| `Pesto Basilikum Parmesan` | 15.0 → 9.5 | 90.5 → 22.7 | 398.4 → **40.3** |
| `Hähnchen` | 11.9 → 10.3 | 47.9 → 22.6 | 240.5 → **58.1** |
| `Tomaten Reis` | 17.9 → 9.1 | 99.3 → 27.4 | 460.7 → **70.2** |

What still grows is the ranking, and it grows with the number of *matches*, not
the size of the library: `Tomaten Reis` matches 4,921 of the 10,000, because
either word is enough to be a candidate, and every candidate needs a tier and a
score before any twenty of them can be chosen. A query that matches little is
now flat.

Verified: 829 responses captured through the API before and after — 57
queries including every golden-set class, four orders and filters each, the
second page of every relevance result, at all three sizes — are byte-identical
except the nine for `tomaten -reis` under relevance, whose totals are unchanged
and whose demoted recipes all contain "Reis".

### 20.7 What would break first

If a household somehow reached 50,000 recipes:

1. **Lane 3** would cross 200 ms. Fix: gate it behind a minimum term length and
   a candidate ceiling, or add a `word_similarity` prefilter on `title_ae`
   before the full document.
2. **Facets** would cross 50 ms. Fix: compute them over the first 200
   candidates rather than the full set — the top facets barely change.
3. **The relevance cursor** would get deep-paging costs. Fix: none needed;
   nobody pages to result 5,000 of a recipe search.

None of these is worth building now. They are recorded so the failure order is
known.

---

## 21. Privacy and self-hosting

### 21.1 What leaves the server

**Nothing.** Enumerated, so the claim is checkable:

| Component | Network calls | Data leaving |
| --- | --- | --- |
| Query understanding | none | none |
| Culinary lexicon | none — compiled into the binary | none |
| All four retrieval lanes | none — PostgreSQL over the compose network | none |
| Ranking | none | none |
| Suggestions | none | none |
| Related recipes | none | none |
| Frontend | same-origin only, existing CSP | none |

No CDN, no font fetch, no model download at runtime, no telemetry. Culina with
its uplink unplugged searches exactly as well as Culina with it plugged in.
This is the strongest argument for Architecture C that is not about latency or
memory: there is no version of it that *could* leak, because there is nothing
to leak to.

### 21.2 Query text

A search query is intimate in a way people underestimate. `glutenfrei`,
`Babybrei`, `Diät`, `Rezepte für zwei` — a search log is a record of what is
happening in a house.

| | Rule |
| --- | --- |
| Server logs | Query text is **never** logged, at any level. It joins the redaction list in `dotnet-observability` beside passwords and session tokens. Logged instead: term count, whether the parse produced constraints, result count, latency. |
| Traces | The `Recipes.GetAll` activity carries `search.term_count` and `search.tier_hit`, never `search.query`. |
| Metrics | Counters and histograms only. No query dimension, which would be a high-cardinality label *and* a leak. |
| Persistence | No `search_history` table. None. |
| Recent searches | `localStorage`, per browser. Never sent, never synced, cleared with the session on sign-out. |
| Across the household | Never aggregated. Two to eight people share a household id, and "what does this household search for" is a report on your partner. |
| Error reports | The problem document for a malformed search echoes the *parameter name*, never the value. |

### 21.3 Self-hosting

| | |
| --- | --- |
| New containers | 0 |
| New volumes | 0 |
| New ports | 0 |
| New extensions | 0 — `pg_trgm` and `unaccent` are already in `db-init.sh` |
| Change to `compose.yaml` | none |
| Change to `compose.prod.yaml` | none |
| Change to the backup procedure | none — the table is in the same `pg_dump` |
| Change to the restore procedure | none |
| Change to the upgrade path | none — `postgres:18-alpine` unchanged |
| Documentation | one row in `configuration.md`, one sentence in `operations.md` |
| Works offline | yes |
| Works on a Raspberry Pi 5 | yes |

Restore is worth one more sentence, because it is the property Architecture B
quietly loses. `docs/operations.md` promises a *tested* restore. With the search
document in the same database, `pg_restore` restores search too, atomically,
with no reindex step and no window in which the app is up and search is empty.
With an external engine, restore becomes: restore PostgreSQL, then rebuild the
search index, then verify the counts agree. That is a new way for a disaster
recovery to half-work.

### 21.4 If an external service were ever used

Per the brief, for completeness. Any remote model — for query understanding
(§7.6) or embeddings — would mean:

- **What leaves**: the query text, and for embeddings, every recipe title,
  ingredient list and step body in the household.
- **Privacy**: a third party learns what this household eats, when, and for how
  many.
- **Cost**: per-token pricing on a self-hosted app with no billing relationship.
- **Availability**: the search box stops working when someone else's API does.
- **Offline**: no.

If it is ever offered, it is off by default, named plainly in
`docs/configuration.md` as *sends your recipes and searches to a third party*,
and search must work identically well without it. Given that last requirement,
the honest observation is that it would then have nothing to add.

---

## 22. Failure and fallback behaviour

### 22.1 The contract

> **Search degrades. It does not error, and it does not return zero results
> without an offer.**

A 500 from a search box is an unacceptable failure mode for a feature people
use twenty times a day. Every layer has a defined weaker answer.

| Failure | Behaviour | User sees |
| --- | --- | --- |
| Grammar throws | Skip parsing; treat the whole query as free text | Results, no chips |
| Lexicon missing a word | Lane 4 contributes nothing | Results from the other lanes |
| Lane 2 fails (bad `tsquery`) | `websearch_to_tsquery` cannot throw, so this is unreachable — but the lane is wrapped anyway | Results from lanes 0, 3, 4 |
| Lane 3 times out (statement timeout) | Lane dropped | Results without typo tolerance |
| `recipe_search_documents` row missing | Fall back to the legacy `ILIKE` predicate for that household | Slower, worse, correct |
| Migration not yet run | `Search__Mode` resolves to `legacy` automatically | Today's behaviour |
| Suggestions endpoint fails | Overlay shows results only | No suggestion list; nothing announced |
| Whole search fails | The library list still renders (it is `GET /recipes` with no `query`) | An error state on the results, the collection intact |

The pattern: **every failure removes a capability, never the feature.** This is
the same shape the recipe store already uses when a page fails — it stops
auto-paging and becomes a button — and for the same reason.

### 22.2 The recovery ladder

Zero results is a failure of the system, not of the user. Each rung changes
**one** thing and says which, so a recovery can never return something the
original query would have excluded without the user seeing why.

```
0 results for the query as typed
   │
   ├─ 1. ANY TERM instead of ALL TERMS
   │     "Alle Wörter: 0 · Einige Wörter: 6"
   │     Applied automatically; the line says it happened.
   │
   ├─ 2. TYPO CORRECTION, against the household's own vocabulary
   │     Correct each unmatched term by trigram against titles, ingredient
   │     names and tags IN THIS HOUSEHOLD. Not a German word list: you can
   │     only usefully search for recipes you have, so the household's own
   │     words are both a smaller and a better dictionary.
   │     Applied automatically when the correction has results.
   │        Ergebnisse für „Bolognese"  ·  Stattdessen „Bolgnese" suchen
   │
   ├─ 3. CONCEPT FALLBACK
   │     Lane 4 alone, tier D, clearly marked.
   │        Nichts mit „Gockel". Ähnliche Rezepte mit Hähnchen: 4
   │
   ├─ 4. RELAX ONE CONSTRAINT, weakest first
   │     order: cuisine → meal → soft time → ingredient → hard time → tag → diet
   │     Never relaxes DIET silently. Ever.
   │        Keine vegetarischen Rezepte unter 20 Minuten.
   │        Ohne „unter 20 Minuten": 4 Rezepte.   [Zeitfilter entfernen]
   │
   ├─ 5. CONFLICT DETECTION, when two constraints are provably incompatible
   │        „Vegetarisch" und „Lachs" schließen sich aus.
   │        [Vegetarisch entfernen]   [Lachs entfernen]
   │
   └─ 6. TRULY EMPTY — the offer
            Kein Rezept für „Schnitzel".
            [ Rezept anlegen ]   [ Aus einer Quelle importieren ]
```

Two of these deserve emphasis.

**Correcting against the household's own vocabulary** is better than any
dictionary and needs no new data. A German word list would offer `Bologneser`;
the household's titles offer `Bolognese`, because that is what is actually
there. It also degrades perfectly: a household with no Bolognese gets no
correction, which is correct.

**Rung 6 is where Culina has an advantage almost nothing else does.** An empty
search in a recipe app is normally a dead end. Culina has a recipe editor and
an importer, and `/recipes/import` is under active development. "You have no
Schnitzel — write one down, or import one" turns the worst moment in search
into the start of a task. It costs two buttons.

### 22.3 What is never done

- **Never relax a diet constraint.** Someone searching `vegan` who is shown a
  recipe with butter has been failed in a way that a missing result is not.
  If vegan yields nothing, the answer is "nothing", with the offer.
- **Never silently widen.** Every rung is labelled. An unlabelled fallback is a
  search box that lies.
- **Never show a typo correction the user has already rejected** within the
  session — if they retype the original after dismissing a correction, the
  original wins.
- **Never return "did you mean" as the only content.** The corrected results are
  shown *with* the undo, not behind a click. A question the user must answer
  before seeing anything is a page they will leave.

---

## 23. Search-quality evaluation

### 23.1 Why a curated golden set, not learned metrics

Eight users produce perhaps 300 implicit relevance judgements a month, most for
the same twenty recipes, all confounded by position bias. That is not a
training set and it is barely a measurement.

Forty hand-written queries against a fixed 60-recipe fixture library, with the
expected ordering written down by a person, is a *better* instrument: it is
deterministic, it runs in CI, it fails loudly when a weight change breaks a
case, and every failure names the query it broke. For a corpus of this size,
hand-curated relevance judgements are not the cheap substitute for learning to
rank — they are the more accurate method.

### 23.2 The fixture library

60 recipes, checked in as a JSON seed, deliberately adversarial:

- 40 German, 20 English, because the corpus is mixed and monolingual fixtures
  hide the interesting bugs.
- Three Bolognese-adjacent recipes (`Spaghetti Bolognese`, `Lasagne Bolognese`,
  `Bolognese-Sauce auf Vorrat`) plus a near-miss (`Ragù alla Napoletana`).
- Compound-heavy titles: `Hähnchenbrustfilet`, `Kartoffelgratin`,
  `Süßkartoffelcurry`, `Zwiebelkuchen`, `Rindergulasch`.
- Both spellings in the wild: a recipe titled `Müsliriegel`, another whose
  ingredient list says `Muesli`.
- Six recipes tagged `vegetarisch`, four vegetarian but untagged, two that
  *look* vegetarian and contain `Fischsauce` or `Hühnerbrühe` — the §7.5 trap,
  present on purpose.
- Recipes with no stated time, so the time-filter semantics stay honest.
- Two English recipes using German ingredient names (a real import artefact).

### 23.3 The query set

40 queries across the eleven classes of §4, each with an expected result and a
tolerance. A sample:

| # | Query | Class | Expect |
| --- | --- | --- | --- |
| 1 | `Spaghetti Bolognese` | A | rank 1 exact |
| 2 | `Bolognese` | B | ranks 1–3 are the three Bolognese, in any order; `Ragù` not in top 3 |
| 3 | `Bolgnese` | C typo | same top 3 as #2; `correctedFrom` set |
| 4 | `Bolognäse` | C variant | same top 3 as #2; `correctedFrom` **null** — this is a spelling, not a typo |
| 5 | `Hühnchen` | F | every chicken recipe present; a `Hähnchen`-titled recipe at 1 |
| 6 | `Huhn` | F | as #5 |
| 7 | `chicken` | F cross-lang | as #5 |
| 8 | `Gockel` | F colloquial | chicken recipes, all tier D, all with a match reason |
| 9 | `Hähnchen` | E compound | `Hähnchenbrustfilet…` present |
| 10 | `Kartoffel` | E | `Kartoffelgratin` and `Süßkartoffelcurry` both present |
| 11 | `Tomaten` | D | recipes with `Tomate` singular present |
| 12 | `Muesli` | C variant | `Müsliriegel` present |
| 13 | `vegetarisch` | H | no recipe containing a meat keyword; tagged before presumed |
| 14 | `vegetarisch mit Lachs` | K conflict | 0 results **and** the conflict message |
| 15 | `unter 30 Minuten` | H | only recipes with a stated total ≤ 30 |
| 16 | `vegetarisch unter 30 Minuten mit Kartoffeln` | I | three chips; correct set |
| 17 | `was kann ich mit Kartoffeln machen?` | G | ingredient query; carrier gone; ranked by overlap |
| 18 | `etwas mit Hähnchen` | G | as #17 for chicken |
| 19 | `Nudeln mit Tomatensoße` | I under-parse | **no chips**; two content terms |
| 20 | `Gericht ohne Fleisch` | H negation | equals #13's set |
| 21 | `schnelles Abendessen` | I | `schnell` is a boost, not a filter: untimed recipes still eligible |
| 22 | `warme Mahlzeit` | J | no salad, no dessert in the top 5 |
| 23 | `Sommergericht` | J | household `sommer`/`salat` tags first |
| 24 | `Frühstück` | H | breakfast recipes; refinement chips differ from #22's |
| 25 | `Schnitzel` | K empty | 0 results **and** the create/import offer |
| 26 | `Pasta` | F | `Spaghetti…`, `Lasagne…`, `Nudelauflauf` present |
| 27 | `zzzz` | K | 0 results, no correction offered, offer shown |
| 28 | `Käsekuchen` | A umlaut in title | rank 1 |
| 29 | `Kasekuchen` | C | rank 1, no correction marker (`unaccent` handles it) |
| 30 | `Kaesekuchen` | C | rank 1 (the `ae` fold in `fuzzy_text`) |
| … | | | |

Every one of these is a regression test (§24.3), not a document.

### 23.4 Metrics

| Metric | Why | Target |
| --- | --- | --- |
| **P@1** | Known-item search is 55 % of traffic; rank 1 is the product | ≥ 0.90 |
| **P@3** | What fits above the fold on a phone | ≥ 0.85 |
| **MRR** | Sensitive to the exact failure that matters — the right answer at 4 | ≥ 0.95 known-item |
| **NDCG@10** | Graded relevance, for the classes where several answers are right | ≥ 0.80 |
| **Zero-result rate** | Should be zero except where empty is correct (#25, #27) | = expected |
| **Typo recovery** | edit distance ≤ 2 recovers | ≥ 0.90 |
| **Cross-language recall** | DE↔EN head nouns | ≥ 0.85 |
| **Latency p95** | §20 | ≤ 60 ms |
| **Tier discipline** | no tier-D result above a tier-A result, ever | **= 1.00** |

The last one is the one that must never regress. It is not a quality metric but
an invariant, it is checkable on every query in the set, and it is the machine
form of §3 R7.

### 23.5 Running it

```bash
make test-search        # the golden set, against a Testcontainers PostgreSQL
```

Output:

```
Culina search evaluation · 60 recipes · 40 queries · lexicon v3

  class                  P@1     P@3     NDCG@10   zero
  known-item exact       1.00    1.00      1.00     0/4
  known-item partial     0.86    0.95      0.91     0/7
  typo                   0.80    1.00      0.88     0/5
  morphology             1.00    1.00      0.96     0/4
  compound               1.00    1.00      1.00     0/4
  synonym / cross-lang   0.83    1.00      0.89     0/6
  ingredient-led         1.00    1.00      0.94     0/3
  constraint             1.00    1.00      0.97     0/4
  vague                  0.33    0.67      0.58     0/3   ← §9.4's gate
  ─────────────────────────────────────────────────────
  overall                0.89    0.96      0.91     0/40
  tier discipline        1.00                        ✓
  p95 latency            14 ms                       ✓

  3 regressions vs lexicon v2:
    #8  Gockel            P@1 1.00 → 0.00   (concept distance lowered)
```

The last block is the point. A weight change that improves the average and
breaks `Gockel` is a change whose author gets told, by name, before it merges.

### 23.6 Reading the numbers honestly

The `vague` row will be the worst and it will stay the worst. It is three
queries out of forty, representing about 1 % of real traffic (§4). The
temptation to fix that row with an embedding model must be weighed against what
the other nine rows would cost in memory, image size and explainability — which
is exactly the decision §9.4 formalises into a gate, so that it is made against
numbers rather than against the feeling that 0.58 looks bad in a table.

---

## 24. Testing strategy

Per `dotnet-testing`: four backend projects, each proving something the others
cannot, plus the frontend's Vitest and Playwright suites.

### 24.1 `Domain.UnitTests` — the pure parts

```csharp
[Theory]
[InlineData("Hähnchen",  "haehnchen", "hahnchen")]
[InlineData("Müsli",     "muesli",    "musli")]
[InlineData("Soße",      "sosse",     "sosse")]   // both folds agree
[InlineData("crème",     "creme",     "creme")]
public void Fold_ShouldEmitBothTransliterations(string input, string umlaut, string stripped);

[Fact] public void Lexicon_ShouldMapChickenAcrossBothLanguages();
[Fact] public void Lexicon_ShouldHaveNoSurfaceFormInTwoConcepts();   // ambiguity guard
[Fact] public void Lexicon_ShouldHaveNoParentCycle();
[Fact] public void Lexicon_ShouldFoldEverySurfaceFormAtBuildTime();
[Fact] public void Lexicon_MeatFamily_ShouldCoverEverySectionKeywordsMeatEntry();
```

The last one is the guard against the three food tables drifting apart, which is
the most likely slow failure in §12.

### 24.2 `Application.UnitTests` — the grammar

Fakes, never a mocking framework, per the existing convention.

```csharp
[Theory]
[InlineData("vegetarisch unter 30 Minuten mit Kartoffeln",
            "diet=vegetarian; maxMinutes=30; ingredient=kartoffel; free=")]
[InlineData("Nudeln mit Tomatensoße",
            "free=nudeln tomatensosse")]                       // under-parse, on purpose
[InlineData("Gericht ohne Fleisch",  "exclude=fleisch; free=")]
[InlineData("schnelles Abendessen",  "soft=quick; meal=dinner; free=")]
[InlineData("was kann ich mit Kartoffeln machen?", "ingredient=kartoffel; free=")]
[InlineData("Bolognese",             "free=bolognese")]
public void Parse_ShouldExtractWhatWasMeant_AndNoMore(string query, string expected);

[Fact] public void Parse_ShouldPreferAHouseholdTag_OverALexiconConcept();
[Fact] public void Parse_ShouldNotTurnQuickIntoATimeFilter();
[Fact] public void Parse_ShouldReturnACharacterSpanForEveryChip();
```

### 24.3 `IntegrationTests` — retrieval, against real PostgreSQL

`RecipeSearchTests` exists and its 329 lines stay. It gains the golden set,
which is where §23 actually lives:

```csharp
[Theory]
[MemberData(nameof(GoldenQueries))]
public async Task Search_ShouldRankTheGoldenSetAsSpecified(GoldenCase c)
{
    var world = await SeedGoldenLibraryAsync();
    var response = await world.Client.GetAsync(
        $"/api/v1/recipes?householdId={world.HouseholdId}&query={Uri.EscapeDataString(c.Query)}",
        Token);

    Assert.Equal(c.ExpectedTop, Titles(response).Take(c.ExpectedTop.Count));
    Assert.All(c.MustNotAppear, t => Assert.DoesNotContain(t, Titles(response)));
    Assert.Equal(c.ExpectedChips, Chips(response));
}

// The invariant, asserted over every query rather than case by case.
[Theory]
[MemberData(nameof(GoldenQueries))]
public async Task Search_ShouldNeverPutAnAssociativeMatchAboveATitleMatch(GoldenCase c);

[Fact] public async Task Search_ShouldReturnTheSameSet_ForTwoMembersOfOneHousehold();  // §13.4
[Fact] public async Task Search_ShouldNeverRelaxADietConstraint();                      // §22.3
[Fact] public async Task Search_ShouldFallBackToLegacy_WhenTheDocumentIsMissing();
[Fact] public async Task SavingARecipe_ShouldMakeItFindableInTheSameRequest();          // §16.2
[Fact] public async Task RenamingARecipe_ShouldMakeTheOldNameStopMatching();
```

`SavingARecipe_ShouldMakeItFindableInTheSameRequest` is the test that would
have been impossible with an external engine, and it is the one that proves the
architecture choice.

### 24.4 `ArchitectureTests`

```csharp
[Fact] public void Domain_ShouldNotReferenceApplication();               // existing
[Fact] public void EveryRecipeWritingHandler_ShouldWriteASearchDocument();
[Fact] public void SearchSql_ShouldNotInterpolateUserInput();            // config names only
[Fact] public void NoLogStatement_ShouldTakeAQueryString();              // §21.2
```

The third is worth having as a test rather than a review habit: §16.2
interpolates a text-search configuration name into SQL, which is safe because
it comes from a two-member enum, and the test is what keeps it that way.

### 24.5 Frontend

```typescript
// Vitest
'search overlay opens on ⌘K and returns focus to the trigger on Escape'
'typing shows suggestions before it shows results'      // two debounces
'interpretation chips render, and removing one re-queries without them'
'backspace on an empty query removes the last chip'
'an out-of-order response never overwrites a newer one' // the #token guard
'nothing matched is not shown while a request is in flight'
'refinement chips are absent when a facet covers everything'
'recent searches are read from localStorage and never sent'

// Playwright
'search from the plan page does not empty the plan page'
'search → open a recipe → back restores the overlay with its query'
'the overlay works with the keyboard alone, start to finish'
'reduced motion removes every transition'
```

### 24.6 What is not tested, and why

- **Lexicon coverage** is not asserted exhaustively. A test that pins all 320
  entries is a test that must be edited every time one is added, which teaches
  people to edit tests rather than think. The *structural* properties are
  asserted (no cycles, no ambiguous surface forms, meat family complete); the
  content is reviewed by a person.
- **Exact score values** are never asserted. Tier is an integer and is asserted;
  score is a tuned float and is only asserted through ordering. A test on
  `score == 0.72` is a test that fails when a weight moves for a good reason.

---

## 25. Implementation roadmap

Four phases. **Search works at the end of every one, and at every point
inside one** — no phase leaves the app in a state where search is half
migrated. Each phase is independently shippable and independently revertable.

---

### Phase 1 — Lexical foundation

*The one that fixes nine of the seventeen failures in §2.*

| | |
| --- | --- |
| **Backend** | `SearchText.Fold` in `Domain`. Migration `0012_search.sql`: two configurations, the table, four indexes, backfill. `SearchDocumentWriter` + the port, called from the three recipe-writing handlers. `RecipeSearcher` gains lanes 0, 2 and 3 and the tier + score ordering. `RecipeSearchSql` gains the relevance order and cursor. `SearchSettings`. |
| **Frontend** | None. The API is unchanged; the library search simply starts working. |
| **Indexing** | Backfill in the migration; per-write upsert thereafter. |
| **Migration** | Additive. `Search__Mode=legacy` restores the old predicate in one setting. |
| **Tests** | `SearchText` theory; the golden set's typo, morphology and compound sections; `SavingARecipe_ShouldMakeItFindableInTheSameRequest`; `ShouldFallBackToLegacy`. |
| **Benefit** | Typos, umlaut variants, inflections, step-body search, and a relevance order that is about the query rather than about `updated_at`. |
| **Complexity** | **Medium.** ~600 lines of C#, ~120 of SQL. |
| **Acceptance** | Golden-set P@1 ≥ 0.85 on classes A–E. Typo recovery ≥ 0.90. `Bolognäse`, `Bolgnese`, `Tomaten`→`Tomate` all find their recipes. `Hähnchen`→`Hähnchenbrustfilet` **still** works (the regression guard). p95 ≤ 40 ms at 500 recipes. Tier discipline = 1.00. |

**Ship on its own.** It is the largest quality jump in the document and it
touches no interface.

---

### Phase 2 — Query understanding, the lexicon, and the overlay

*The one users will describe as "search got clever".*

| | |
| --- | --- |
| **Backend** | `CulinaryLexicon` (~320 concepts). `QueryGrammar` + `QueryUnderstandingService`. Lane 4. `RecipeSearchService` and the §22 ladder. `interpretation` and `facets` on the response, `matchReason` on each item. `GET /households/{id}/suggestions`. |
| **Frontend** | `SearchOverlay`, `SearchChip`, `SuggestionGroup`, `SearchResult`. `search.svelte.ts`. `⌘K` / `/` binding in `AppShell`. Recent searches in `localStorage`. Mobile sheet. `RecipePicker` re-pointed at the shared store. New message keys, DE + EN — **appended, never re-sorted** (see the paraglide note in `CONTRIBUTING.md`). |
| **Indexing** | `concepts[]` and `diet[]` populated; `lexicon_version` bumped; `LexiconReindexService` reindexes at startup. |
| **Migration** | None — the columns exist from phase 1; only their contents change, by version. |
| **Tests** | Grammar theory; golden-set classes F–I and K; the conflict and relaxation cases; the overlay's Vitest suite; the two Playwright journeys. |
| **Benefit** | Synonyms, cross-language, natural-language constraints, chips, autocomplete, the recovery ladder, and search from anywhere. |
| **Complexity** | **High.** ~900 lines of C#, ~10 kB of frontend. The lexicon is an afternoon plus a review. |
| **Acceptance** | Golden-set overall P@3 ≥ 0.85, NDCG@10 ≥ 0.80. Every query in §23.3 produces its stated chips. Zero-result rate = expected. Suggestions p95 ≤ 40 ms. Frontend bundle ≤ 130 kB (still under the 140 kB budget). Tier discipline = 1.00. |

---

### Phase 3 — The rest of the intelligence layer

*Where the investment starts paying for features that are not search.*

| | |
| --- | --- |
| **Backend** | `GET /recipes/{id}/related`. Duplicate detection in the import pipeline. Tag suggestions in the editor's read model. "Als Sammlung speichern" mapping chips → smart-cookbook rules. |
| **Frontend** | "Ähnliche Rezepte" rail on the recipe page, loaded with `whenVisible`. Duplicate warning in the import review. Tag suggestions in the editor. The save-as-collection button in the overlay footer. |
| **Indexing** | None — all of it reads `recipe_search_documents`. |
| **Migration** | None. |
| **Tests** | Related-recipe ordering; duplicate detection precision against a fixture of near-duplicate imports; the chips → rules mapping round trip. |
| **Benefit** | Four features for roughly one feature's work, and a household stops accumulating duplicate Bolognese. |
| **Complexity** | **Low–medium**, because the hard part is already built. |
| **Acceptance** | Related recipes return ≥ 3 for any recipe in a 60-recipe library, each with a reason. Duplicate detection: recall ≥ 0.90, **false positives = 0** on the fixture — a wrong duplicate warning is worse than a missed one. Chips → rules round trips exactly. |

---

### Phase 4 — Measure, and only then consider semantics

*Not an implementation phase. A decision phase, with an outcome that may be
"no".*

| | |
| --- | --- |
| **Work** | Run the §23 evaluation. Extend the lexicon once against the vague section's failures. Re-run. |
| **Gate** | §9.4: `NDCG@5(vague) < 0.50` **and** measured share > 3 % **and** the lexicon extension did not help. |
| **If the gate opens** | §9.5: `pgvector`, `multilingual-e5-small` int8, query-conditional, RRF at k = 20, a separate `culina:latest-semantic` image tag, `Search__Semantic__Enabled=false` by default. |
| **If it does not** | Write down that it did not, with the numbers, in this document. That is the deliverable. |
| **Acceptance** | A decision, with evidence, either way. |

---

### Sequencing

```
  Phase 1 ──────────────▶ Phase 2 ──────────────▶ Phase 3
  lexical                 understanding           reuse
  backend only            + the overlay           + import, editor, shelves
  invisible API change    visible everywhere      four features
       │                       │                       │
       └── ship ───────────────┴── ship ───────────────┴── ship
                                                            │
                                            Phase 4 ── measure ── decide
```

Phases 1 and 2 could merge. They should not: phase 1 is a backend change with a
measurable outcome and no interface risk, and shipping it alone means that if
phase 2's interface needs rework, the retrieval improvements are already in
people's hands.

### Beads

```
culina-v2-0r34  [feature] Search: retrieval, query understanding, ranking and UX
  ├── phase 1   Search document, folding, and the four lexical lanes
  ├── phase 2   Query understanding, the culinary lexicon, and the overlay
  ├── phase 3   Related recipes, duplicate detection, tag and shelf suggestions
  └── phase 4   Evaluate the vague-query gate and decide on semantics
```

---

## 26. Risks and tradeoffs

### 26.1 Risks

| # | Risk | Likelihood | Impact | Mitigation |
| --- | --- | --- | --- | --- |
| R1 | **The lexicon becomes curated data nobody maintains** — the exact failure `README.md` rejects a pantry for | medium | high | Bounded at ~320 entries with a stated ceiling. Household tags always win (§12.4). It is C# source, so changing it is a PR with a test, not a data-entry task. A wrong entry pollutes tier D, below the fold, not rank 1. |
| R2 | **Tier boundaries are wrong for some real query** | medium | medium | Every golden-set case asserts tier, not score, so a mis-tiered query fails by name. The `Gockel` regression line in §23.5 is what this looks like when it happens. |
| R3 | **The fuzzy lane is too slow at the upper end** | low | medium | Conditional (§8.4); `FuzzySkipThreshold` is configurable; §20.7 names the fix. |
| R4 | **Over-parsing**: the grammar claims a sentence it should have left alone | medium | **high** — this is the one that makes people distrust the box | Anchored carrier rules only; §23.3 #19 exists specifically to catch it; chips make every claim visible and one tap reversible. |
| R5 | **The presumed-vegetarian filter is wrong** for a recipe using `Brühe` or `Fischsauce` | **high** | medium | Presumption is marked in the UI, never asserted; the `hidden-animal` family catches the worst; one tap writes a tag that makes it permanently right. |
| R6 | **Search document drifts from the recipe** | low | high | Same transaction, so it cannot; `source_version` detects it anyway; an architecture test asserts every writing handler indexes. |
| R7 | **A lexicon change silently degrades ranking** | medium | medium | `lexicon_version` forces a reindex; the evaluation prints a per-class regression list against the previous version. |
| R8 | **Two debounces feel worse than one** | low | low | 120/200 ms measured against the current 250; adjustable without a deploy shape change. |
| R9 | **The overlay becomes the fifth way to navigate** and competes with the five destinations | medium | medium | It is not a destination: no route, no history entry, no bottom-nav slot. `navigation-research.md`'s five stand. |
| R10 | **PostgreSQL's German stemmer is worse than expected** on real household text | low | medium | Lane 3 covers what lane 2 misses; the evaluation measures it; this is the one place a dedicated engine would genuinely help, and §14 records the door it leaves open. |

### 26.2 Tradeoffs, taken deliberately

| Chosen | Given up | Why |
| --- | --- | --- |
| PostgreSQL | 5 ms searches, best-in-class typo tolerance | 11 ms is indistinguishable from 5 ms behind a 120 ms debounce, and a second container is not indistinguishable from one |
| Curated lexicon | embeddings' unbounded coverage | 320 entries cover what a kitchen says; a model covers everything and explains nothing |
| Tier + score | RRF's elegance, LTR's adaptivity | Explainable and testable beats optimal on a corpus this small (§10.2, §10.4) |
| Deterministic grammar | an LLM's flexibility | Sub-millisecond, always available, always the same, and it shows its work |
| Synchronous indexing | 3 ms per recipe save | A recipe that saved but is unfindable is a worse bug than a save that fails visibly |
| Both folds in `fuzzy_text` | 18 % index size | `Müsli`, `Muesli` and `Musli` are one word to a person |
| Conditional fuzzy lane | a simpler query plan | 11 ms typical against 23 ms always |
| Personalisation capped at 0.15 | a more "personal" feel | Two people in one kitchen must get the same answer; §13.1 |
| No result cache | 11 ms per search | A recipe saved on the tablet is findable on the phone, now |
| Chips on every inference | some visual density | A parser that does not show its work is a parser people stop trusting |

### 26.3 The one that could go the other way

If a household turns out to hold 10,000 recipes — a serious cook who imported
three public libraries — then lane 3 crosses 200 ms, facets cross 50 ms, and
Meilisearch's profile starts to look right rather than excessive.

The migration path is honest and it is short, which is the point of having
`RecipeSearchService` as a seam at all: the lanes live behind
`IRecipeRepository.SearchAsync`, the lexicon and the grammar are transport-free
`Domain` and `Application` code, and the tier + score model can be expressed in
Meilisearch's custom ranking rules. What changes is the retrieval; what stays
is the query understanding, the lexicon, the ranking semantics, the API, every
piece of the interface, and the entire evaluation suite — which is how you would
know the swap had not made anything worse.

That is the test of whether this architecture was drawn in the right places, and
it is why the seams are where they are.

---

## Appendix — decisions, in one table

| Question | Answer | Section |
| --- | --- | --- |
| Search engine? | PostgreSQL, in process | §5, §6, §14 |
| Full-text? | Yes — `culina_de` / `culina_en`, `unaccent` before the stemmer | §8.1 |
| Trigram? | Yes, and it is load-bearing, not a garnish | §8.4 |
| Compound splitting? | Trigram substring, not Hunspell | §5.1, §8.4 |
| Synonyms? | In the application, not in a PG dictionary | §1, §8.1 |
| Embeddings? | No. Gated, with a prediction. | §9 |
| Reranker? | No | §11.6 |
| Fusion? | Tier + bounded score, not RRF | §10.2 |
| Query understanding? | Deterministic grammar, ~40 rules per language | §7.3 |
| LLM? | No. §7.6 designs the optional, non-blocking version. | §5.12, §7.6 |
| Ontology? | ~320-concept flat lexicon, household tags first | §12 |
| Personalisation? | Tie-break only, capped at 0.15, never crosses a tier | §13 |
| Index update? | Same transaction as the recipe write | §16.2 |
| New endpoints? | Two: `…/suggestions`, `…/related`. `GET /recipes` stays the search. | §17 |
| New containers? | None | §21.3 |
| Added RSS? | ~4 MB | §20.5 |
| Search UI? | An overlay over the current page, never a route | §18.1 |
| Zero results? | Six-rung recovery ladder ending in create-or-import | §22.2 |
| Evaluation? | 60-recipe fixture, 40 golden queries, in CI | §23 |

---

## 27. What phase 1 actually shipped

Written after the implementation, because several things the sections above
predicted turned out to be wrong, and a design document that quietly agrees
with itself afterwards is worth nothing.

### 27.1 The performance estimates in §20 were wrong by a factor of twenty

§20 predicted 11 ms typical and 23 ms worst at 2,000 recipes. The first
measurement of the implemented query, on an adversarial synthetic library where
one query in five matches a fifth of the collection:

| | 200 recipes | 2,000 recipes |
| --- | --- | --- |
| **Predicted (§20)** | 4 ms | 11 ms typical, 23 ms worst |
| **First measurement** | 22–41 ms | **406–512 ms** |
| **After the two fixes below** | **2.7–5.6 ms** | **8–33 ms** |

Three cost centres, isolated by measuring each predicate on its own rather than
by reading the plan and guessing:

| At 2,000 recipes | |
| --- | --- |
| `ingredient_count` correlated subquery — **already in main, not new** | **620 ms** |
| `word_similarity` over `fuzzy_text` (1,164 characters) | **190 ms** |
| `ts_rank_cd` × 3 over every row | 31 ms |
| `word_similarity` over `title_ae` (25 characters) | 2.6 ms |
| The whole candidate predicate, once the above is fixed | 4.2 ms |
| Full-text, title prefix, substring LIKE, tags, cook counts | 0.4–2.4 ms each |

The largest single cost was not in the new code at all. `GET /recipes` counted
every recipe's ingredients with a correlated subquery, for every row of every
search, and `count(*) over ()` means the page size never capped it. At 2,000
recipes that alone was 620 ms, and it had been there since the endpoint was
written.

### 27.2 The two fixes, and why both are design improvements

**The typo lane is a title lane.** §8 measured similarity against the whole
folded recipe. It now measures against the title only, and the substring half
still reaches every word. This is 190 ms → 2.6 ms, but the argument is not
speed: **a misspelling is a mistyped name.** Nobody misspells a word buried in
step four and expects to be understood, and every typo in §23's golden set —
`Bolgnese`, `Bolognäse`, `Kartoffelgratn` — is a title. The golden set passes
identically before and after, which is what says the recall was not bought back
with a worse answer.

**`ingredient_count` is a stored column.** §15.1 listed it, and §15 then removed
it during implementation on the grounds that nothing read it. That was correct
reasoning from wrong information: the ranking reads it, the `ingredientMatch`
line reads it, and computing it cost 620 ms. It is back, with the measurement in
the comment, and the correlated subquery survives only inside a `coalesce` as
the fallback for a document that has somehow gone missing.

The general lesson is the one §23 already made about relevance and §20 did not
make about latency: **estimate, then measure, then believe the measurement.**

### 27.3 Deviations from the design, and why

| §  | Designed | Shipped | Why |
| --- | --- | --- | --- |
| 8.1 | The document assembled in C# | Assembled in SQL, through a `recipe_search_input` view | The per-recipe write, the migration's backfill and any future re-index are then literally the same statement. Assembling in C# would have put "what is searchable about a recipe" in two places, and the backfill needs it in SQL anyway. |
| 7.2 | `SearchText.Fold` in `Domain` | `culina_fold_ae` / `culina_fold_a` in SQL | Follows from the above: one implementation, used by the indexing side and the query side alike, so they cannot disagree. There is no C# fold to drift from it. |
| 16.2 | A `LexiconReindexService` hosted service | The migration backfills; an analyser change ships as a new migration | No startup window in which search is empty, no batching, no hosted service, and migrations already have the advisory lock and the checksum discipline. `culina_search_analyzer_version()` is what a future migration bumps. |
| 8.4 | The fuzzy lane runs conditionally | Every lane runs, always | A conditional lane has to be recorded in the cursor or page two silently re-ranks. One query at 8–33 ms is worth more than two at 6 ms and a paging bug. |
| 16.5 | Five `SearchSettings` knobs | One `const`, no settings group | The fuzzy threshold changes which results people see. That belongs in a commit beside the test case that moved it, not in an environment variable nobody reviews. |
| 16.4 | `Search__Mode=legacy` escape hatch | Not built | A second, rarely-exercised SQL path is a liability. The migration is additive, so the rollback is deploying the previous image. |
| 11.2 | Six score signals | Four | `fieldWeight` and `evidenceWeight` are what the tier already decides. Counting the same evidence twice means tuning one silently undoes the other. |
| 15.1 | `source_version` on the document | Not built | Nothing read it, and an image-only edit bumps a recipe's version without changing a searchable word, so the column would have been expected to be stale. |
| — | — | `q.has_text` | Found by a test. A query of nothing but punctuation folds to the empty string, and an empty string is a prefix of every title, so `%` returned the whole library through `like '' || '%'`. A contentless query now explicitly means an empty search box. |

### 27.4 Measured against the phase 1 acceptance criteria

| Criterion | Result |
| --- | --- |
| Golden set on classes A–E | **32/32**, first run |
| Typo recovery | `Bolgnese`, `Bolognäse`, `Kartoffelgratn` all recover |
| `Bolognäse`, `Bolgnese`, `Tomaten` → `Tomate` | all pass |
| `Hähnchen` still finds `Hähnchenbrustfilet` (the regression guard) | passes |
| p95 at 500 recipes ≤ 40 ms | **~6 ms**; 33 ms at 2,000 |
| Tier discipline = 1.00 | asserted over every golden query |
| Backend suite | **745 tests, 0 failures** |

### 27.5 Still open

- The index build is **~9 s for 2,000 recipes** on the first start after
  upgrading, not the ~4 s §20 guessed. A 500-recipe library — the realistic
  case — is about two seconds. `docs/operations.md` should say so.
- Phase 1 changed no interface. Everything in §18 is phase 2.
- The `qwertzuiop` case costs 8 ms at 2,000 recipes, which is the floor of the
  candidate scan. §20.7's advice stands if a library ever reaches five figures.
  *(Superseded by §27.6: there is no candidate scan any more, and `qwertzuiop`
  is 9 ms at 10,000.)*
