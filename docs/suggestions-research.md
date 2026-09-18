# Culina — what should I cook? (research and proposed architecture)

| | |
| --- | --- |
| Status | Proposal. Nothing here is built. |
| Written | 18 September 2026 |
| Decides | Whether Culina gets a recommendation subsystem, and what it is |

The objective is not "add a recommender". It is to make the app better at
answering the one question a person standing in their kitchen at 18:40 actually
has: **what would I like to cook right now?** Every choice below is judged
against that sentence and against the constraint that this is a 2–8 person
self-hosted app.

Three kinds of claim appear in this document and are marked where it matters:

- **Fact** — read out of this repository, or out of a primary source, cited.
- **Reasoning** — my argument from those facts.
- **Assumption** — something I could not verify; stated so it can be checked.

---

## A. Existing-system analysis

### A.1 What the product already says about itself

`README.md` states the thesis: a cook moves through **finding → deciding →
shopping → cooking → remembering**, and "all the pain lives in the transitions
between them."

Culina has built four of those five. *Deciding* is the one with no product
support at all. `GET /recipes` defaults to `sort=-updatedAt`
(`Application/Abstractions/RecipeSearch.cs:39`), so the front page of the app
answers **"what did I edit most recently?"** — a question nobody has ever asked
themselves while hungry. The library page even labels it, honestly, with
`recipes.list.recent`.

That is the gap. It is not a missing widget; it is a missing answer to the
product's own stated middle step.

### A.2 What the README rules out, and why it constrains this work

Four exclusions, all load-bearing:

> No pantry inventory (nobody maintains one, so it goes stale and poisons
> everything built on it). No calorie calculation (a wrong number is worse than
> none). No social feed, ratings or comments — this is your kitchen, not a
> network. No AI assistant.

And `domain-model.md`, on the cook log:

> Append-only. Powers "you've made this 7 times, last in March" and the
> "most cooked" sort, **which is why Culina needs no star ratings: what you
> actually cook is a better signal than what you claim to like.**

**Reasoning.** That last sentence is already a recommender-systems position,
and a good one: Culina has *committed to implicit feedback* and has been
collecting it since the cook log shipped. The dataset this work needs already
exists and is already argued for. Nothing in this proposal needs to relitigate
ratings.

The exclusions also pre-reject several standard moves: no ratings matrix, no
nutrition-based filtering, no cross-household signal, no LLM re-ranking, and no
pantry (so "what can I cook from what I have" must stay the *ask-me* form it
already has — `?ingredient=` repeated, ranked, never stale).

### A.3 The existing bead epic already ruled on part of this

`culina-v2-erv` ("Ten capabilities that compound on what the schema already
knows") records, under REJECTED:

> **seasonality ranking** (needs a curated ingredient→season table; same failure
> mode as the pantry inventory README.md rejects)
>
> **difficulty scores, trust scores, nutrition estimates** — because they invent
> a number users would rightly distrust

**Reasoning.** Both rejections are right *as stated*, and this proposal honours
them — but note precisely what was rejected: **curated** seasonality. Seasonality
*observed from the household's own cook log* has none of the failure modes of a
curated table: it cannot be wrong about your kitchen, it needs no maintenance,
it is empty on a fresh install rather than confidently wrong, and it is
falsifiable by the person reading it ("we do cook that in November"). §G.6
designs it that way, and gates it behind an evidence threshold so it stays
silent until it has something true to say. Difficulty and nutrition stay
rejected outright.

The same epic states the thesis this proposal extends: *"Culina already records
more than it spends."*

### A.4 Backend architecture

**Fact**, from `docs/deployment.md`, `Dockerfile` and the source:

- .NET 10, minimal APIs, **no MVC, no mediator**. `IEndpoint` implementations are
  mapped explicitly by name; `ICommandHandler<T>` / `IQueryHandler<T,R>` are
  injected straight into the route delegate (`Application/Abstractions/Messaging/`).
- Five projects: `Api → Application → Domain`, with `Contracts` for wire types
  and `Infrastructure` for adapters. An architecture test enforces the
  references.
- Failures are `Result` / `Result<T>` values, never exceptions
  (`dotnet-result-pattern`).
- **Npgsql + Dapper, no EF Core.** SQL lives in
  `Infrastructure/Persistence/<Domain>/`. Rows map to Domain through
  hand-written `<Entity>RowMappings`.
- One container in production: `mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled`,
  `read_only: true`, `cap_drop: ALL`, writable only at `/data/images` and
  `/data/keys`. `InvariantGlobalization=true` (`Directory.Build.props:51`).
- A `BackgroundService` precedent exists: `ExpiredSessionSweeper`.
- OpenTelemetry is wired: one `ActivitySource` and one `Meter` named `Culina`,
  with `UseCaseDuration` already tagged per handler
  (`Application/Telemetry/CulinaTelemetry.cs`).

**Reasoning.** The shape of the codebase is unusually favourable to this work.
There is no ORM to fight, the search is already one hand-written SQL statement,
and there is already a precedent for a background job. A scoring pass is a CTE
away.

The container, on the other hand, is hostile to ML dependencies: `chiseled`
images have no shell and a minimal library set, and the process is read-only.
Anything needing native binaries or a model file on disk is a real change to the
deployment contract, not a NuGet reference. This matters in §D.

### A.5 Database and data model

**Fact.** PostgreSQL 18 (`compose.yaml`: `postgres:18-alpine`). Extensions are
created by `scripts/db-init.sh` **as superuser, once, at first container start**:
`citext`, `pg_trgm`, `unaccent`. The application role deliberately cannot create
extensions:

> The app never connects as a superuser: a SQL injection that reached the
> database should not also be able to create extensions…

Twenty-six tables exist. The ones that matter here:

```
recipes(id, household_id, title, description, language, yield_amount, yield_kind,
        prep_minutes, cook_minutes, image_id, created_by, created_at, updated_at, version)
recipe_ingredients(id, group_id, sort_order, quantity, unit, name, note)
ingredient_groups(id, recipe_id, name, sort_order)
steps(id, recipe_id, sort_order, body, duration_seconds)
step_ingredient_refs(step_id, recipe_ingredient_id)
tags(id, household_id, name, slug)          recipe_tags(recipe_id, tag_id)
recipe_images(id, recipe_id, content_hash, width, height, byte_size, content_type, created_at)
recipe_origins(recipe_id, household_id, kind, source_id, external_id, source_url, imported_at)

cook_log_entries(id, recipe_id, user_id, household_id, made_at, servings, note,
                 image_hash, image_width, image_height)
cook_sessions(id, recipe_id, user_id, household_id, servings, current_step_index,
              started_at, last_active_at, completed_at, abandoned_at, version)
personal_notes(id, recipe_id, user_id, step_id, body, updated_at, version)

meal_plan_entries(id, household_id, on_date, recipe_id, servings, slot, sort_order)
cookbooks(id, household_id, name, description, kind, rule_tags, rule_ingredients,
          rule_max_minutes, created_by, created_at, updated_at, version)
cookbook_recipes(cookbook_id, recipe_id, added_at, added_by)

shopping_lists / shopping_list_items / shopping_section_overrides
```

**Two findings worth recording separately:**

1. **`shopping_list_item_sources` does not exist.** `domain-model.md` specifies
   it, and `api.md` documents `DELETE /shopping-lists/{id}/recipe-additions/{recipeId}`,
   but no migration creates the table and the actual route is
   `POST /households/{id}/shopping-list/recipes`. So **"added to the shopping
   list" is not currently a recoverable signal**, and the documented "subtract
   exactly that recipe's contributions" behaviour has nothing to subtract from.
   (Documentation drift, not a recommendation problem — but it removes a signal
   the brief assumes exists. See §C.4.)
2. **Every aggregate carries `version bigint`**, and writes are
   `where id = @id and version = @version` in SQL. ETags derive from it. Any new
   table must decide whether it is an aggregate (it mostly is not — events have
   no ETag).

### A.6 Recipe metadata actually available

This is the single most important constraint in the document, and it is worth
stating flatly.

| The brief assumes | Culina has |
| --- | --- |
| cuisine | ✗ — nothing. Tandoor's `Cuisine > Italian` keyword is flattened to a plain tag on import (`TandoorMapping.cs:74`) |
| meal type | ✗ on the recipe. ✓ **on the plan**: `meal_plan_entries.slot ∈ {breakfast, lunch, dinner}` |
| difficulty | ✗ — and explicitly rejected (§A.3) |
| seasonality | ✗ — and a *curated* table explicitly rejected |
| nutrition | ✗ — explicitly rejected in `README.md` |
| ingredients | ✓ name, quantity, unit, note — free text, no ingredient table at all |
| tags | ✓ **household-authored free text**, `(household_id, slug)` unique |
| prep/cook time | ✓ both nullable; `total_minutes` derived in SQL, never stored |
| textual description | ✓ nullable, ≤ 2000 chars |
| cooking method | ✗ — but implicit in step text and `steps.duration_seconds` |
| servings | ✓ `yield_amount` + `yield_kind ∈ {servings, pieces}` |

**Reasoning.** There is no taxonomy. Tags are whatever these particular people
typed, per household, so `vegetarisch` and `veggie` and `ohne Fleisch` can all
exist and mean one thing, and nothing outside the household can interpret them.
That kills any design that depends on a shared vocabulary — including every
managed service, all of which want categorical item metadata.

It also means the *only* content features available are **tags** and
**ingredient names**, both household-local free text. That turns out to be
enough (§F), but only because the ranking is household-local too.

### A.7 The search that already exists

`Infrastructure/Persistence/Recipes/RecipeSearcher.cs` is one SQL statement that
does filtering, ranking, counting and paging together, deliberately:

> Splitting it would mean the count and the page could disagree when something
> changes between them.

It already computes, per row: `total_minutes`, the tag array, **`cook_count` for
the asking user**, `ingredient_count`, `matched_ingredients`, and the cookbook
join. It already supports six sorts including `relevance`, and cursor paging via
row-comparison predicates in `RecipeSearchSql.cs`.

**Reasoning.** A seventh sort is a small, idiomatic change to a file that was
built for exactly this. `cook_count` per user is already in the projection —
the first personalisation term is *already computed on every list request and
thrown away*.

### A.8 Cookbooks are the architectural precedent

`domain-model.md` and `api.md` are emphatic:

> A cookbook is **read as a view of the collection, not as a collection of its
> own**… which is what gives a cookbook the same search, tag filter, time
> ceiling, ingredient ranking and cursor paging the whole library has, with no
> second implementation of any of them.

And smart cookbooks:

> **The rules are stored; what matches them is not.** A smart cookbook is a
> saved question, answered whenever it is read.

**Reasoning.** This is the shape the recommendation subsystem must take, and it
is already the house style. A suggestion is a **saved question answered at read
time**, ranking the one collection — never a second collection, never a
materialised list that can drift from the recipes it claims to describe. If this
document has one architectural thesis, it is: *recommendations are a sort, not a
feed.*

It also means **smart cookbooks are already a rule-based recommender** that
ships today. The new work is the ranking, not the retrieval.

### A.9 Frontend architecture

**Fact.** SvelteKit 2 / Svelte 5 runes, static SPA, same-origin, no CORS.

- `src/lib/design-system/` is domain-free; `src/lib/features/<domain>/` is
  domain-aware; `src/routes/` is thin. "If it knows what a recipe is, it is in
  the wrong folder."
- Per-feature rune stores are the single source of truth. Components never
  `fetch` — an ESLint rule fails the build (`frontend-api-client`).
- Five navigation destinations, fixed and deliberate
  (`lib/app/navigation.ts`, `docs/navigation-research.md`): Recipes, Cookbooks,
  Week, Shopping, Me. **There is no home page** — `(app)/+page.svelte` *is* the
  recipe library.
- Three colour layers; components may only name semantic tokens, enforced by a
  lint rule and a contrast contract test.
- Weight budget, measured and enforced: **97.4 kB of JavaScript across every
  route, budget 140 kB**; styles 12.0 kB of 24 kB.
- `createLoadingState()` owns the 150 ms / 300 ms / 10 s timing rules; every
  feature uses it.
- i18n is compile-time Paraglide, German and English, both always complete.

**Reasoning.** Two consequences. First, **there is no home page to put a
carousel on** — which is a gift, because it forces the recommendations into the
flows where they belong instead of into a dashboard. Second, the JS budget has
~42 kB of headroom across the *whole app*; a recommendation feature that needs a
new page, a new store and three new components is spending a meaningful fraction
of it. The proposal below adds one component.

Two existing components are already the right shape:

- `FeaturedRecipe.svelte` — a large feature panel with an **eyebrow**, a title,
  a meta line and a link. Its current selection rule is
  `recipes.items.find(r => r.imageId !== null)`, i.e. *the first recipe with a
  photo*. Arbitrary.
- `RecipeCard.svelte` — already renders an **eyebrow**, currently
  `recipe.tags[0] ?? null`. Also arbitrary.

Both already have the slot a reason goes in.

### A.10 Deployment

One image serving API and SPA from the same origin, plus Postgres. Two mandatory
volumes. TLS is the proxy's job. `read_only: true`, `no-new-privileges`,
`cap_drop: ALL`. The operator's whole job is `docker compose up -d`.

**Reasoning.** Anything that adds a third container, a Redis, a model file, or a
`CREATE EXTENSION` on an existing database is not "an infrastructure
requirement" — it is a **breaking change to every existing installation's
upgrade path**, for a 2–8 person app. That is the bar §D measures against.

---

## B. Recommendation use cases

Five distinct questions. They are not five algorithms — §G shows they are one
scorer with different context and different presentation — but they are five
different *questions*, and conflating them is how apps end up with "Recommended
for you" five times on one screen.

### B.1 Decide — "what should I cook tonight?"

The primary case. A person opens the app with no query in mind. Today the app
shows what was edited last.

Context available **for free, without asking**: the clock (→ meal slot), the
date (→ weekday/weekend, month), the household's plan for the next few days, the
household's cook log for the last few weeks, and this person's own history.

Success looks like: the top of the library is a list they would plausibly cook,
and the first item is one they *actually* cook.

### B.2 Rediscover — "we used to make this"

A distinct question, and the one a small library is best at. A 2–8 person
household accumulates 100–400 recipes over years and cooks perhaps 20 of them in
rotation. The other 300 are not bad recipes; they are forgotten ones.

**Reasoning.** This is where a small installation *beats* a large platform.
Netflix cannot tell you that you loved something in 2019 because it has a
billion items and you watched 400. Culina has 300 items and you cooked 250 of
them — it knows exactly which ones fell out of rotation and when. Rediscovery
needs no model at all: it needs `max(made_at)` and a sense of proportion.

### B.3 Plan — "what goes on Thursday?"

The richest context in the product and the one that needs no new UI. When
somebody taps an empty day in the week planner, the app *already knows* the
date, the slot (the sheet asks), and what else is planned that week. Today
`RecipePicker` opens showing the library sorted by `-updatedAt`.

Success: the picker's first screen, before anyone types, is the answer often
enough that typing is optional.

### B.4 Relate — "I like this one; what else?"

On the recipe page. A different question from B.1: it is not about *me*, it is
about *this recipe*. Content similarity (shared ingredients, shared tags,
comparable time and method) does most of the work, and household co-cooking adds
a little.

**Reasoning.** With 2–8 users, "users who liked this also liked" has no
statistical content (§F.3). "Uses eleven of the same twelve ingredients" has
plenty, and is also *explainable*, which the co-occurrence version is not.

### B.5 Rescue — an empty result, an empty day, an empty list

Search found nothing. A planned week has a hole. The library is brand new. These
are the moments where a suggestion is unambiguously welcome, because the
alternative on screen is nothing.

`EmptyState` already exists and already takes an `action` snippet.

### B.6 Where a recommendation would be noise — decided, not deferred

- **While cooking.** `RecipeSurface` in `cook` emphasis, and the "I made it"
  moment. The toast already says "that's the 8th time". A suggestion here
  interrupts the one flow the product is proudest of.
- **The shopping list.** It is a task being executed in a shop, not a browse.
  And the tempting version — "you already have most of these things" — is a
  pantry claim, which `README.md` refuses on the grounds that it goes stale.
  The list is a *record of intent to buy*, not a record of what is in the
  cupboard, and treating it as the second thing is exactly the failure mode.
- **Inside a cookbook.** A cookbook is a curation; "you might also want these on
  your shelf" undermines the thing that makes it a shelf and not a tag.
- **The recipe editor.** Nothing to suggest; the person is writing.

---

## C. Available signals

### C.1 Signals that already exist and are already written

Ranked by strength-per-byte. All of these need **no new events and no new
tables** — this is the "already paid for" list.

| Signal | Table | Strength | Why |
| --- | --- | --- | --- |
| **Cooked it** | `cook_log_entries(recipe_id, user_id, made_at)` | ★★★★★ | A deliberate act with a cost. Dated, per-person, append-only. The gold standard, and the reason Culina needs no ratings. |
| **Cooked it repeatedly** | same, `count` | ★★★★★ | Repetition in food *is* preference; unlike media, re-consumption is the normal case (§F.5). |
| **Planned it** | `meal_plan_entries(recipe_id, on_date, slot)` | ★★★★ | Intent, dated, and **carries the meal type** the recipe lacks. |
| **Put it on a shelf** | `cookbook_recipes(recipe_id, added_at, added_by)` | ★★★★ | An explicit, attributed curation. The closest thing to a "favourite" the app has. |
| **Wrote a note on it** | `personal_notes(recipe_id, user_id, updated_at)` | ★★★ | Nobody annotates a recipe they are indifferent to. |
| **Photographed the result** | `cook_log_entries.image_hash` | ★★★ | Took a picture of it. Strong, rare, free. |
| **Scaled it** | `cook_log_entries.servings`, `cook_sessions.servings` | ★★ | Cooking for six is a different occasion from cooking for two. |
| **Finished cooking it** | `cook_sessions.completed_at` | ★★ | Completion vs. the log entry: a session finished *and* logged is the strongest single event in the schema. |
| **Recency of all the above** | every one of them is dated | ★★★★★ | Everything here has a timestamp. That is what makes decay and rediscovery possible without any new column. |

### C.2 The one signal that must be explicitly collected: "not this"

**Reasoning.** With 2–8 users there is no such thing as a statistically
meaningful non-click. If a person is shown five suggestions and cooks none of
them, that is one data point drawn from a population of one evening, and the
honest reading is "we do not know". Inferring dislike from silence at this scale
manufactures data.

Negative feedback therefore has to be **asked for, once, cheaply, and never
again**: a dismissal. One row:

```sql
suggestion_dismissals(user_id, recipe_id, dismissed_at, reason?)
```

This is the only genuinely new *signal*. Everything else in §C.1 is a read.

### C.3 What should deliberately **not** be tracked

The brief lists recipe-opened, searched-for, viewed-repeatedly, skipped,
recommendation-ignored. My recommendation is to collect **none of them**, and
the reasons are specific rather than reflexive:

- **Recipe views.** The highest-volume write the app would have, and the least
  informative. In a shared kitchen you open a recipe to cook it, to shop for it,
  to show it to somebody, and because you mis-tapped. The outcome you care about
  — did they cook it — is already recorded downstream. It is also the most
  privacy-sensitive table in the design ("what has my housemate been looking
  at"), on an app whose entire premise is that it is *your* kitchen, and it
  would be the first table in Culina that exists purely to watch the user.
  **Fails the "does the problem exist?" test: cook-log density is the binding
  constraint, and views do not improve it, they only add volume.**
- **Search queries.** Typed, personal, and already expressed more reliably by
  what got cooked afterwards.
- **Dwell time, scroll depth, hover.** Meaningless at n=4 and expensive to
  collect from an SPA.
- **Impressions of every card in every list.** See below for the one bounded
  exception.

### C.4 The one impression record that earns its place

For the bounded suggestion sets only (§G.4), record what was shown:

```sql
suggestion_impressions(id, user_id, household_id, recipe_id, shown_at,
                       purpose, rank, score, reason)
```

It earns its place because it pays for a **user-visible behaviour**, not just
analytics: *"do not show me the same five recipes every evening this week."*
Without it, a deterministic ranker shows an identical top five for days, which
is the single most common way a small-catalogue recommender feels broken. That
it also makes click-through measurable (§M) is a bonus, not the justification.

Constraints, stated up front: household-scoped, 90-day retention swept by a
`BackgroundService`, never leaves the instance, excluded from the archive export
(like every other person-owned table, per `domain-model.md`).

### C.5 The signal that is documented but missing

`shopping_list_item_sources` (§A.5) would give **"added to the shopping list"**
— a strong, dated intent signal sitting one table away, already specified in
`domain-model.md`, already needed to make the documented
"subtract this recipe's contribution" behaviour real.

**Recommendation:** build it, but as its own bead for its own reason (the
shopping list's correctness), and let the ranker read it when it lands. Do not
bundle it into this work; it does not block anything here.

### C.6 Derived content features

No new storage. Computed from what recipes already are:

| Feature | Derivation | Use |
| --- | --- | --- |
| Tag set | `recipe_tags → tags.slug` | content profile, similarity, diversity |
| Ingredient set | `recipe_ingredients.name`, normalised (lower, unaccent, trim) | content profile, similarity, diversity |
| Total time | `prep_minutes + cook_minutes`, null-preserving | hard context filter |
| Effort proxy | `count(steps)`, `count(recipe_ingredients)`, `sum(steps.duration_seconds)` | **filter only, never a displayed score** (§A.3) |
| Unattended share | `sum(duration_seconds)` vs `prep_minutes` | "start it and walk away" — a real weeknight signal |
| Has a photo | `image_id is not null` | presentation, not ranking |
| Age in the library | `created_at`, `recipe_origins.imported_at` | novelty / freshness (§H.7) |
| Language | `recipes.language` | tie-break toward the reader's locale |

**Ingredient normalisation**: `unaccent` and `pg_trgm` are already installed, and
`recipe_ingredients.name` already has a trigram GIN index. Normalise to
`lower(unaccent(trim(name)))` — the same folding the shopping list already does
into `name_key`. Do **not** attempt stemming, singular/plural folding or a
synonym table: `domain-model.md` already draws this exact line between
`CommonIngredients` (names to offer) and `SectionKeywords` (stems to match), and
inventing a third vocabulary is how it rots.

### C.7 Event semantics, weighting and decay

One rule for all of them: **a signal's weight is the cost of producing it.**

| Event | Weight `w` | Decay half-life | Note |
| --- | --- | --- | --- |
| cook log entry | 1.00 | 365 d | The unit. |
| cook log entry with a photo | 1.30 | 365 d | Multiplier on the same row, not a second event. |
| planned (`meal_plan_entries`) | 0.60 | 180 d | Intent, not completion — and it may have been swapped out. |
| added to a manual cookbook | 0.80 | none | A curation is not an event; it is a standing statement. Decaying it would be saying the shelf expired. |
| personal note written | 0.40 | 365 d | |
| cook session completed | +0.30 on the matching log entry | 365 d | A modifier, not an event. |
| cook session **abandoned** | **0.00** | — | See below. |
| dismissal | −2.00 | 90 d | Deliberately larger than any positive, and deliberately temporary. |

**`abandoned_at` must not be read as dislike.** `domain-model.md` is explicit
that there is at most one active session per user and *"Starting a new one
abandons the previous"* — enforced by a partial unique index. Abandonment is
therefore overwhelmingly a mechanical consequence of starting something else,
not a judgement. Treating it as negative feedback would systematically punish
the recipes people cook *most often*, because those are the ones they start
again. **Reasoning, and the kind of mistake that is invisible until the ranking
is quietly upside down.**

**Repeated interactions.** Use `log1p(Σ w·decay)` rather than the raw sum. The
difference between cooked-once and cooked-twice is large; between eleven and
twelve times it is nothing, and linear weighting lets one weekly staple
dominate every list forever.

**Accidental interactions.** The cook log already has undo
(`UndoCookedCommand`, `DELETE .../cook-log/{entryId}`) — a deleted entry is
simply not there, so no de-duplication heuristic is needed. Two log entries for
the same recipe within one hour are collapsed to one at read time: that is a
double tap, not two dinners.

**Per-user vs household.** The distinction `domain-model.md` draws must be
respected exactly:

- **Affinity is personal.** Two people in one household may disagree about a
  recipe, and the model must let them (§C.1 sources are all `user_id`-keyed
  except the plan).
- **Repetition fatigue is household-level.** If your partner made the lasagne on
  Tuesday, *you ate it*, and it should not be suggested to you on Wednesday even
  though your own cook log is silent. This asymmetry is the single most
  household-specific piece of modelling in the design, and no general-purpose
  recommender expresses it.

---

## D. Technology landscape

Every option is judged on one question: **what does this buy a four-person
household that the plainer option below it does not, and what does it cost the
person running `docker compose up`?**

### D.1 Managed recommendation services

| | Amazon Personalize | Recombee / Algolia Recommend / similar |
| --- | --- | --- |
| Self-hostable | No | No |
| Data leaves the instance | Yes — every recipe title, tag and cooking event | Yes |
| Minimum useful data | **1,000 interactions** ([AWS docs](https://docs.aws.amazon.com/personalize/latest/dg/native-recipe-user-personalization-v2.html)) | Comparable order of magnitude |
| Cold item defined as | **fewer than 5 interactions** ([AWS docs](https://docs.aws.amazon.com/personalize/latest/dg/native-recipe-similar-items.html)) | — |
| Cost floor | see below | subscription |
| Explainability | None — a transformer returns scores | None |
| Vendor lock-in | Total | Total |

**Cost, derived.** Amazon Personalize's published pricing is $0.15 per 1,000
recommendation requests for v2 recipes, and *"Amazon Personalize charges a
minimum of 1 recommendation request transaction per second (TPS) for all active
campaigns by default"* ([pricing](https://aws.amazon.com/personalize/pricing/)).
**My arithmetic, stated as such:** one TPS billed continuously is
1 × 3600 × 24 × 30 ≈ 2.59 M requests per month, so an idle campaign costs
≈ **$389/month** on v2 pricing, or ≈ **$144/month** on the legacy tier
($0.0556/1,000). For a household that will issue perhaps 300 real requests a
month. That is roughly **$1.30 per actual recommendation** on the legacy tier
and $3.50 on v2 — before training, before ingestion, before an AWS account being
a prerequisite for a self-hosted recipe book.

**Disqualifying, independent of cost:** it is a network service holding a
household's private cooking history, on an app whose README says "this is your
kitchen, not a network" and whose deployment story is two volumes and a proxy.

**Verdict: reject.** Not because it is bad — it is very good — but because its
minimum viable dataset is two orders of magnitude larger than this app will ever
have, and its own documentation would classify essentially every recipe in a
household library as a permanently cold item.

### D.2 Self-hosted recommendation platforms

**Gorse** ([github.com/gorse-io/gorse](https://github.com/gorse-io/gorse), Apache-2.0,
~9.8k stars, actively maintained). Go, RESTful, stores data in MySQL/Postgres/
MongoDB/ClickHouse with **intermediate results cached in Redis**, and runs as
**master + server + worker** nodes (a single-node "in-one" image exists, which
the project describes as a playground).

**Cost to Culina:** at least one more container and a Redis, a second copy of the
item catalogue that must be kept in sync with `recipes`, a second failure mode at
startup, and an upgrade path for every existing installation. **Reasoning:** for
a workload of ~300 requests a month over ~300 items, this is an operations
budget spent on nothing. The training set is too small for the models it exists
to run.

Other open-source systems (RecBole, LensKit, Microsoft Recommenders, Cornac) are
**research toolkits in Python**, not services. They are the right tools for
*evaluating* an approach offline and the wrong shape entirely for a .NET single
container. They are, however, genuinely useful as a reference for the offline
replay harness in §M.

**Verdict: reject as runtime; keep as reading.**

### D.3 .NET / NuGet libraries

| Package | Latest | State | Fit |
| --- | --- | --- | --- |
| `Microsoft.ML` + `Microsoft.ML.Recommender` | 0.23.0, 11 Nov 2025, depends on `Microsoft.ML ≥ 5.0.0` ([nuget](https://www.nuget.org/packages/Microsoft.ML.Recommender)) | Maintained, MIT, still 0.x | `MatrixFactorizationTrainer` wraps **LIBMF**, a native library. Needs the native runtime present in a **chiseled** container and works against `InvariantGlobalization=true` — both need proving, and neither is free. Adds >100 MB to an image whose frontend budget is measured in kilobytes. And matrix factorization on an 8×300 implicit matrix is not a model, it is noise with a loss curve. |
| `DiscoRec` | 0.1.2, 11 May 2025, **739 total downloads** ([nuget](https://www.nuget.org/packages/DiscoRec)) | MIT, a thin wrapper that *depends on* `Microsoft.ML.Recommender` | All of ML.NET's costs plus a single-maintainer dependency with three-digit adoption, for an API convenience. No. |
| `MyMediaLite` | last updated **2016** | Abandoned | No. |
| `NReco.Recommender` | .NET port of Apache Mahout CF | Alive, but Mahout-era user/item-based CF | Same fundamental problem as everything else in this row: CF needs users. |
| `Infer.NET` | Microsoft Research, probabilistic | Alive | Genuinely interesting for *small-data* Bayesian ranking, and the only entry here whose statistical machinery is actually designed for sparse evidence. But it is a large conceptual dependency and a model somebody has to maintain the graph for. **Keep as a Phase 4 option, not now.** |

**Reasoning.** Every library in this table exists to fit latent factors to an
interaction matrix. §F.3 shows the matrix in question has at most 8 rows. The
libraries are not the problem; the premise is.

### D.4 Embeddings and vector similarity

The case for: recipe *text* (title, description, steps) carries meaning that
tags and ingredient names miss — "Ofengericht" and "traybake" are the same idea
in two languages, and Culina is bilingual by design (`recipes.language`,
Paraglide DE+EN).

The cost, concretely:

- **A model.** `all-MiniLM-L6-v2` (384-d, ~90 MB) is **English-only** and would
  be wrong half the time in this app. A multilingual model
  (`paraphrase-multilingual-MiniLM-L12-v2`, `multilingual-e5-small`) is
  ~450–500 MB. That is **four times the whole application image**, shipped to
  every self-hoster, to reorder 300 recipes.
- **A runtime.** ONNX Runtime in a `read_only`, chiseled container, plus a model
  file that must live on a volume or in the image. The mature options
  (`SmartComponents.LocalEmbeddings`, `ElBruno.LocalEmbeddings`, Kjarni) are all
  **community projects**, not Microsoft-supported, and all assume a normal
  container.
- **A store.** `pgvector` 0.8.6 supports PG18 and there is an official image
  ([pgvector/pgvector](https://github.com/pgvector/pgvector)) — but Culina runs
  `postgres:18-alpine` and creates extensions **as superuser at first-container-
  start only** (`scripts/db-init.sh`). Adding pgvector is therefore a base-image
  change *and* a manual `CREATE EXTENSION` for every existing installation.

**And the benchmark it has to beat:** for ~300 recipes, exact cosine similarity
over a sparse tag+ingredient vector, computed in Postgres, is sub-millisecond.
There is no retrieval problem to solve. ANN indexes exist to make *millions* of
comparisons cheap; 300 is not a number that needs an index.

**Verdict: reject for now, with the door explicitly left open.** §G stores
similarity behind an interface with one implementation, so the day a household
has 5,000 recipes in four languages, swapping the implementation is one class.

### D.5 Custom implementation in SQL and C#

The remaining option: a scoring pass expressed as one Postgres query plus a small
C# ranking stage, in the same shape as `RecipeSearcher`.

| | |
| --- | --- |
| New infrastructure | **None.** No container, no extension, no model, no volume. |
| New dependencies | **None.** |
| Image size delta | **0 MB.** |
| Works on day one | Yes — content-based, from metadata that exists. |
| Works at n=4 | Yes — it never assumes otherwise. |
| Explainable | **Yes, exactly** — every term is named, so a reason is a fact rather than a story. |
| Improves with data | Yes — every term is already a function of the cook log. |
| Migration path off it | It *is* the migration path: the interface stays, the implementation changes. |
| Risk | The weights are hand-set at first. §H and §M say how they stop being. |

**Verdict: this is the recommendation.** Not as a compromise — as the correct
answer for this problem size.

---

## E. Amazon Personalize analysis

Worth studying carefully, because its *architecture* is excellent even where its
*service* is inapplicable. All statements here are from AWS documentation, cited.

### E.1 How it works

**Datasets.** Three, in a dataset group: **Item interactions** (required),
**Items** (optional metadata), **Users** (optional metadata). Interactions carry
`USER_ID`, `ITEM_ID`, `TIMESTAMP`, optionally `EVENT_TYPE`, `EVENT_VALUE`,
**contextual metadata** (the user's environment at event time), and
**impressions** (what was visible when they acted).

**Recipes** (their word for algorithms), the relevant ones:

- `aws-user-personalization-v2` — transformer over the user's interaction
  sequence. **Minimum 1,000 interactions.** Trains on up to 5 M items. Handles
  exploration itself. Hyperparameter `apply_recency_bias` (default `true`).
- `aws-user-personalization` (v1) — RNN-based, and the one that exposes the
  interesting knobs: `hidden_dimension` (default 149), `bptt` (32),
  `recency_mask` (true), and the two exploration parameters below.
- `aws-similar-items` — item-to-item from **co-occurrence in user histories plus
  item metadata similarity**. Minimum **1,000 unique interactions**. Defines a
  **cold item as one with fewer than five interactions**, and falls back to
  *popular items* when asked about an unknown item.
- `aws-personalized-ranking-v2` — re-ranks a caller-supplied candidate list.

**Exploration**, on v1, is two explicit numbers:

- `exploration_weight` — default **0.3**, range [0.0, 1.0]. "The closer the value
  is to 1.0, the more exploration. At zero, no exploration occurs and
  recommendations are based on current data (relevance)."
- `exploration_item_age_cut_off` — default **30.0 days**, minimum 1. Scopes
  exploration by item age, computed from creation timestamp.

**Filters** are a small declarative DSL applied at request time, *outside* the
model:

```
EXCLUDE/INCLUDE ItemID WHERE {dataset}.{field} IN/NOT IN ({value|$parameter})
```

with `AND`/`OR`, comparison operators for numerics, `CurrentUser.attribute` in a
trailing `IF`, `CurrentItem.attribute` for related-items recipes, `|` to chain
expressions, and `$PARAM` placeholders supplied per request. Interaction-based
filters consider up to **100 recent interactions per user per event type**.

**Retraining.** Automatic updates every two hours to consider new items;
full retraining and HPO every 90 days when automatic training is on.

**Promotions** exist specifically because v2 "more frequently recommends
existing items with interactions data", so you must *deliberately* reserve
slots for new items.

### E.2 Which concepts transfer, and how they scale down

This is the useful output of studying it.

| Personalize concept | Keep? | Culina's appropriately-sized version |
| --- | --- | --- |
| **Interactions as the primary dataset; metadata as secondary** | ✓ **Keep entirely** | The cook log is the interactions dataset. It already exists and is already argued for. |
| **Event *type* and event *value* with per-type weights** | ✓ Keep | §C.7's weight table. Cooked ≠ planned ≠ shelved. The idea that events are not fungible is the single most valuable thing in the model. |
| **Contextual metadata supplied at request time, not stored on the item** | ✓ **Keep — this is the big one** | Culina's recipes have no `mealType` column and should not get one. Slot, clock, date and season are *request context*, exactly as Personalize models them. §G.2's `SuggestionContext`. |
| **Declarative filters applied outside the model** | ✓ Keep the *idea*, not the DSL | Culina already has the filters — `?tag=`, `?maxMinutes=`, `?ingredient=`, `?cookbookId=` — and a rule that a shelf and the filter bar cannot disagree. Reuse those clauses; do not build a second expression language. |
| **Explicit exploration weight** | ✓ Keep the concept | §H.8. But implemented as a seeded deterministic jitter rather than a bandit, because stability matters more than optimality at this scale. |
| **Item age cut-off scoping exploration** | ✓ Keep | `recipes.created_at` / `recipe_origins.imported_at` give "recently added to the book", and 30 days is a sane starting value. |
| **Promotions: reserve slots for new items** | ✓ **Keep — and it is more important here** | A household that imports 200 Tandoor recipes has 200 items with zero interactions. Without a reserved slot, a pure relevance ranker would never show any of them. §H.7. |
| **Impressions: record what was shown** | ✓ Keep, bounded | §C.4, and for a user-visible reason rather than for training. |
| **Separate related-items model** | ✓ Keep the separation | §B.4 is a different question. But base it on content, not co-occurrence (§F.3). |
| **Cold-start fallback to popular items** | ✓ Keep | §G.7's ladder ends in exactly this. |
| **Dataset groups, solutions, solution versions, campaigns, TPS** | ✗ Drop | Deployment machinery for a multi-tenant service. Culina has one process. |
| **Transformers / RNNs / latent factors** | ✗ Drop | §F.3. |
| **HPO every 90 days** | ✗ Drop; replace | §M's offline replay grid-search, which does the same job in seconds over eight weights. |
| **Automatic retraining every 2 hours** | ✗ Drop; replace | There is no model to retrain. Scores are computed at read time, so a recipe saved this evening is ranked correctly this evening — the same property §A.8 gives smart cookbooks. |

### E.3 The one-line answer

**Amazon Personalize's *ideas* are almost all right and almost all free;
Personalize itself is priced, sized and scoped for a problem Culina does not
have.** Take the interaction/context/filter/exploration/impression model whole;
take none of the infrastructure; and replace the learned scoring function with an
explicit one, which is a downgrade in ceiling and an upgrade in everything else
at n=4 — including the ability to say *why*.

---

## F. Algorithm comparison under the 2–8 user constraint

### F.1 The arithmetic that decides this

**Assumption**, stated so it can be corrected: a 4-person household cooks from
the app ~5 times a week between them and keeps 100–400 recipes.

That is **~260 cook-log entries per year**, against 100–400 items.

| | Culina, year 1 | Culina, year 5 | Personalize's stated minimum |
| --- | --- | --- | --- |
| Users | 2–8 | 2–8 | thousands |
| Items | 100–400 | 300–1,200 | up to 5 M supported |
| Interactions | ~260 | ~1,300 | **1,000 to train at all** |
| Interactions per item | ~1 | ~1.5 | — |
| Items AWS would call "cold" (<5 interactions) | ~95 % | ~85 % | — |

**Reasoning.** Culina reaches Personalize's *minimum to begin* somewhere around
year four, at which point ~85 % of its catalogue is still cold by AWS's own
definition. This is not a small dataset. It is a dataset where the number of
*users* is smaller than the number of *latent factors* those libraries default to
(`hidden_dimension` defaults to 149).

### F.2 Rule-based / weighted scoring

**Fit: excellent.** Every term is computable from data that exists, on day one,
with zero interactions. Every term is nameable, so every ranking is explainable
and every bug is findable. Weights are initialisable from stated ordering rules
(§H.9) and improvable by offline replay (§M.3).

**Weakness:** the weights are somebody's opinion until there is enough history to
calibrate them. This is a real weakness and §M addresses it directly rather than
hand-waving it.

### F.3 Collaborative filtering — the decisive verdict

**User-user CF** computes similarity between users and recommends what your
neighbours liked. With 8 users, every user has **at most 7 neighbours**, and in a
household they are the people you already eat with. The "discovery" it produces
is *"your partner also cooks this"* — which is true, useful, and computable with
`count(*) ... group by recipe_id`. **The value is real; the collaborative
filtering is not doing any of the work.**

**Item-item CF** computes co-occurrence in user histories. With ~260
interactions over ~300 items, the modal co-occurrence count between any two
recipes is **zero**, and the non-zero ones are mostly 1 — which is one person,
one month, one coincidence. Any similarity derived from this is noise with a
confidence interval wider than the scale.

**Matrix factorization** fits latent factors to an 8 × 300 matrix that is ~99.7 %
empty. It will converge. It will produce numbers. The numbers will be
overfitted to a handful of evenings, and — this is the part that matters — they
will be **unexplainable**, so nobody will be able to tell that they are wrong.

**Verdict: reject collaborative filtering, in all three forms, at this scale.**
Keep its *one* genuinely useful residue — household popularity — as an explicit,
small, explainable term (§H.6). Revisit only at the trigger in §N.4.

### F.4 Content-based filtering

**Fit: the core of the system.** Build a per-person profile over tags and
ingredient names from their weighted, decayed history; score a recipe by cosine
similarity to that profile.

Why it works here specifically:

- It needs **one** interaction to start saying something, not a thousand.
- It handles new items natively: a recipe imported this morning has tags and
  ingredients, so it is scoreable before anybody has touched it — which is
  precisely the case Personalize needs *promotions* to work around.
- The features are **household-local free text**, and that is fine because the
  model is household-local too. `vegetarisch` does not need to mean anything
  outside this kitchen.
- Each contributing feature is a word a person recognises, so the explanation
  writes itself: *"You often cook things tagged suppe."*

Its classic failure — over-specialisation, the filter bubble — is real and is
what §H.7 (novelty), §H.8 (exploration) and §G.5 (diversity) exist to counteract.

### F.5 Repeat-consumption and temporal models

Food is unusual, and the literature says so. *Recommender for Its Purpose:
Repeat and Exploration in Food Delivery Recommendations*
([arXiv:2402.14440](https://arxiv.org/abs/2402.14440), Li, Sun, Ma, Sun & Zhang,
Feb 2024) finds that repeat orders are prevalent for both users and stores, that
situational context influences repeat and exploration consumption *differently*,
and that existing situation-aware methods cannot address both at once — so they
build **two simple models**, one for repeat and one for exploration, and note
that both are "simple in their design and computation".

**Reasoning.** That is a strong external endorsement of the split this design
already wants: *rediscovery* and *discovery* are different questions and should
be different terms, not one blended score. It also endorses simplicity as a
finding rather than a concession.

The Hawkes-process line of work (item-specific short-term and lifetime effects in
repeat consumption) is the sophisticated version of §H.3's repetition penalty. It
is the right Phase 4 upgrade and the wrong Phase 1 starting point: it fits a
per-item temporal kernel, and Culina has ~1 observation per item.

### F.6 Learning to rank

Needs labelled relevance judgements and thousands of impressions. Culina will
produce a few hundred impressions a year. **Reject.** The offline replay in §M.3
is the tractable shadow of the same idea: it evaluates a ranking against what was
actually cooked, and grid-searches eight weights rather than fitting a model.

### F.7 Multi-armed bandits

Genuinely tempting: bandits are the one family designed for *small* data, and
Thompson sampling over ~10 ranking weights would converge on a few hundred
observations.

**But the reward signal is the problem.** The reward is "did they cook this",
which arrives hours later, is confounded by whether they were home, and occurs
once a day at most. At ~260 rewards per year and ~8 arms, a bandit needs years to
distinguish the arms — and during those years it deliberately shows worse
suggestions to learn.

**Reject as a learner. Keep the exploration primitive**: a bounded, seeded,
deterministic jitter (§H.8) buys the diversity benefit without the instability,
and the seeded part is what stops the list reshuffling under the user's thumb.

### F.8 The verdict

| Approach | Verdict at 2–8 users |
| --- | --- |
| Weighted scoring over explicit terms | ✅ **Core** |
| Content-based (tags + ingredients) | ✅ **Core** |
| Temporal: decay, repetition penalty, rediscovery | ✅ **Core, and the most product-specific part** |
| Contextual filtering (time, slot, plan) | ✅ **Core** |
| Household popularity | ✅ Small term, honest |
| Diversity (MMR) | ✅ On bounded sets only |
| Seeded exploration | ✅ Bounded, stable |
| Item-item content similarity | ✅ For "related recipes" |
| Item-item **co-occurrence** similarity | ❌ Insufficient data |
| User-user CF | ❌ ≤7 neighbours |
| Matrix factorization | ❌ 99.7 % sparse, unexplainable |
| Learning to rank | ❌ No labels, no volume |
| Bandits as learners | ❌ Reward too slow and too rare |
| Embeddings / ANN | ❌ No retrieval problem to solve at 300 items |

The pipeline the brief proposes — candidate generation → contextual filtering →
personalization → diversity → exploration → ranking — **is the right shape.** The
correction is that at this scale several stages collapse into a single SQL
statement, and pretending otherwise would be the premature complexity §14 of the
brief warns against.

---

## G. Recommended architecture

### G.1 The thesis

**A suggestion is a saved question answered at read time, ranking the one
collection.** It is a sort, not a feed. This is the same decision Culina already
made for cookbooks and smart cookbooks (§A.8), and making it a third time is what
makes the feature feel native rather than bolted on.

Consequences, all of them good:

- No materialised recommendation table that can drift from the recipes.
- A recipe written this evening is ranked correctly this evening.
- Every existing filter composes with it for free.
- Nothing to backfill when a weight changes.

### G.2 The context model

One record, small, composable — not a list of modes. Following Personalize's
separation (§E.2), **context is supplied per request and never stored on the
recipe.**

```csharp
namespace Application.Abstractions;

/// <summary>Why we are asking, and what we already know about the occasion.</summary>
public sealed record SuggestionContext(
    Guid HouseholdId,
    Guid UserId,
    SuggestionPurpose Purpose,
    DateTimeOffset Now,             // clock and calendar; never DateTime.UtcNow inside
    DateOnly? ForDate,              // the day being planned, when one is
    MealSlot? Slot,                 // breakfast | lunch | dinner, when known
    int? MaxMinutes,                // "I have 25 minutes"
    IReadOnlyList<string> Tags,     // reuses ?tag=
    IReadOnlyList<string> Ingredients, // reuses ?ingredient=
    Guid? LikeRecipeId,             // "more like this one"
    IReadOnlyList<Guid> Exclude,    // already on screen, already planned
    int Count);

public enum SuggestionPurpose
{
    Browse = 0,   // rank the whole library; paged; no diversity, no exploration
    Decide = 1,   // a small explained set; diversity and exploration on
    Like = 2      // item-to-item; personal affinity down-weighted
}
```

**Three purposes, not nine.** `Purpose` selects a *preset* — how many, how
diverse, how much exploration, whether affinity or similarity dominates — and
nothing else. Every screen-specific behaviour is expressed by the other fields.
Adding "Breakfast" or "QuickMeal" as purposes would be exactly the "dozens of
hard-coded modes" the brief warns against: they are `Slot = Breakfast` and
`MaxMinutes = 30`.

`Now` is passed in rather than read, because the existing handlers already inject
`TimeProvider` and because a ranking function that reads the clock cannot be
tested.

### G.3 Components and data flow

```
                       GET /recipes?sort=suggested…      GET /suggestions?…
                                 │                              │
                                 ▼                              ▼
                         GetRecipesQuery            GetSuggestionsQuery
                                 │                              │
                                 └────────────┬─────────────────┘
                                              ▼
                                      SuggestionContext
                                              │
   Application ──────────────────────────────┼───────────────────────────────
                                              ▼
                                   ISuggestionRanker          (one port)
                                              │
   Infrastructure ────────────────────────────┼───────────────────────────────
                                              ▼
                                    SuggestionRanker
                                              │
   ┌──────────────────────────────────────────┴───────────────────────────────┐
   │ 1. CANDIDATES + HARD FILTERS      one SQL statement, household-scoped     │
   │      the WHERE clauses GET /recipes already applies (SmartShelfSql-style) │
   │      minus: dismissed, planned within ±3 days, currently being cooked     │
   │                                                                          │
   │ 2. FEATURES                        same statement, as CTEs               │
   │      taste_profile  — decayed tag/ingredient weights for this user       │
   │      last_cooked    — max(made_at) per recipe, HOUSEHOLD-wide            │
   │      affinity       — decayed personal weight per recipe                 │
   │      household_pop  — others' decayed cook counts                        │
   │      slot_prior     — from meal_plan_entries.slot + made_at hour         │
   │      season_prior   — per-tag month distribution, evidence-gated         │
   │                                                                          │
   │ 3. SCORE                           same statement: one score per row,    │
   │      plus every term returned SEPARATELY so a reason is a fact           │
   └──────────────────────────────────────────┬───────────────────────────────┘
                                              ▼
                              ScoredRecipe[] (score + per-term breakdown)
                                              │
                    ┌─────────────────────────┴─────────────────────────┐
                    │ Browse                          Decide / Like     │
                    │ order by score, cursor-paged    4. DIVERSITY (MMR)│
                    │ no diversity, no exploration    5. EXPLORATION    │
                    │                                 6. REASON         │
                    └─────────────────────────┬─────────────────────────┘
                                              ▼
                                   Contracts → HTTP → Svelte
```

Steps 1–3 are **one Postgres query**. Steps 4–6 are ~60 lines of C# over at most
50 rows. That is the whole subsystem.

### G.4 The two doors

**Door one — the sort.** `GET /recipes?sort=suggested`, with every existing
parameter. It is `Purpose = Browse`: the full library, ranked, cursor-paged, no
diversity, no exploration (so paging is stable and a cursor means something).

This is the highest-value change in the proposal and it adds **no new endpoint,
no new store and no new component** — one `RecipeSort` value, one `OrderBy` arm,
one `ResumePredicate` arm, one `KeysOf` arm in `RecipeSearchSql.cs`, and the
scoring CTEs in `RecipeSearcher`. Paging resumes on `(score, id)` exactly as
`MostCooked` resumes on `(cook_count, id)` today.

**Door two — the bounded set.** `GET /suggestions?householdId=…` returns 3–12
recipes **with reasons**, never paged.

```
GET /api/v1/suggestions
    ?householdId=…            required
    &purpose=decide|like      default decide
    &forDate=2026-09-18       optional
    &slot=dinner              optional
    &maxMinutes=30            optional
    &tag=suppe                repeatable
    &ingredient=aubergine     repeatable
    &likeRecipeId=…           optional (implies purpose=like)
    &exclude=…                repeatable
    &limit=5                  default 5, max 12
```

`/suggestions` is a plural-noun collection with filters as query parameters,
which is what `rest-api-design` asks for (`/tags?householdId=`,
`/cookbooks?householdId=`). It is `Cache-Control: no-store` like every
authenticated response, carries no ETag (it is not an entity), and is not
wrapped in a cursor envelope because it is deliberately bounded — the wrapping
convention exists for *collections that page*, and a thing with no next page
should not claim one.

**Why two doors and not one.** They answer different questions and have
genuinely different mechanics: one must page and therefore cannot reorder for
diversity; the other must diversify and therefore cannot page. One scorer, two
callers — which is what `code-simplicity` means by extracting the thing that
repeats.

### G.5 Diversity

Greedy MMR over the top ~40 scored candidates, for bounded sets only:

```
pick the highest-scoring;
then repeatedly pick argmax over remaining of
    score(r) − λ · max( similarity(r, p) for p already picked )
with λ ≈ 0.35 and similarity = Jaccard over (tags ∪ normalised ingredient names)
```

**Reasoning.** Five suggestions that are five pasta dishes is the failure a small
library produces *most* often, because a household's taste profile is genuinely
narrow and content-based scoring is faithful to it. λ is deliberately modest:
diversity that overrides preference produces a list of things you do not want,
which is worse than a list of similar things you do.

### G.6 Seasonality, done honestly

This is the part that must not become the curated table `culina-v2-erv` rejected.

**Learned, never curated.** For each **tag slug** (and each sufficiently common
normalised ingredient name), compute the distribution of `extract(month from
made_at)` over this household's cook log. Claim a seasonal effect only when:

1. that tag has **≥ 12 cook-log observations** in the household, and
2. the current month's share is **≥ 1.5×** uniform (i.e. ≥ 12.5 % of the mass).

Otherwise the term is **exactly zero and no seasonal reason is shown.**

Why per-tag rather than per-recipe: at ~260 entries a year over ~300 recipes,
per-recipe month distributions are one observation and a hope. Tags pool — every
soup entry informs "soup". That is the level at which this household has enough
evidence to say anything.

**A fresh installation has no seasonality and says nothing about seasons.** That
is the correct behaviour, and it is what makes the claim trustworthy when it
eventually appears: *"you cook this kind of thing in November"* is a statement
about them, verifiable by them, and it was silent until it was true.

**Weather is rejected.** It would require the first unsolicited outbound network
call in the product (the only current ones are user-initiated imports), a
location for a household that never gave one, and a CC-BY attribution obligation
on every install (Open-Meteo's free tier is keyless and generous — ≤10,000 calls
a day — but requires CC-BY 4.0 and is non-commercial-only). And the signal is
nearly collinear with the month, which is free. **Month is most of the value at
none of the cost.**

### G.7 Cold start — the ladder

The system must be useful before it knows anything. Four rungs, and it climbs
them by itself:

| State | What ranks | What a reason says |
| --- | --- | --- |
| **Empty instance, no recipes** | Nothing. `EmptyState` already handles this and says "write one" / "import one". | — |
| **Recipes, no cook log** (a fresh import of 200 Tandoor recipes) | Context only: time fits the slot, has a photo, recently added, has enough detail to cook from. Plus diversity so it is not 200 alphabetical pastas. | "New in your book" |
| **A few entries (1–20)** | Content-based from that handful, plus household popularity. One cook of a curry moves curries. | "You've been cooking with [aubergine]" |
| **A working history (20+)** | The full model. Rediscovery becomes possible around the six-month mark, and is the term that makes a mature library feel alive. | all of §I |

**A new user in an established household** is the case a generic recommender
handles worst and Culina handles best: they inherit the *household* terms
(popularity, plan slots, repetition fatigue) immediately, and the personal terms
fade in. Nothing special is needed — the household terms are simply the only
non-zero ones on day one.

**Onboarding preferences: do not build them.** The brief lists them as an
option. A "pick your favourite cuisines" screen requires a cuisine taxonomy the
app does not have (§A.6), asks a question before the person has seen the
product, and produces a preference that is contradicted by behaviour within a
month. The household's first three cook-log entries are worth more than any
answer to that screen, and they cost the user nothing.

**The one exception worth considering later:** if a household tags heavily,
`GET /tags?householdId=` already returns usage counts. A "which of these do you
cook most?" step *over the household's own tags* asks about words they wrote
themselves. Phase 3 at the earliest, and only if the replay harness shows the
cold weeks are actually bad.

### G.8 Persistence, caching, background work

**Persistence.** Two new tables, and I argue against a third:

```sql
-- The only genuinely new signal: "not this".
create table suggestion_dismissals (
    user_id      uuid        not null references users (id) on delete cascade,
    recipe_id    uuid        not null references recipes (id) on delete cascade,
    dismissed_at timestamptz not null,
    primary key (user_id, recipe_id)
);

-- What was shown, so the same five recipes are not shown all week.
create table suggestion_impressions (
    id           uuid        not null primary key,
    user_id      uuid        not null references users (id) on delete cascade,
    household_id uuid        not null references households (id) on delete cascade,
    recipe_id    uuid        not null references recipes (id) on delete cascade,
    shown_at     timestamptz not null,
    purpose      text        not null,
    rank         int         not null,
    score        numeric(8,4) not null,
    reason       text
);
create index suggestion_impressions_recent_idx
    on suggestion_impressions (user_id, shown_at desc);
```

Neither is an aggregate root: neither gets a `version` column, because neither is
ever read-modify-written and an ETag on an event log would be a claim nobody
needs. A dismissal is a fact at a known address — hence `primary key (user_id,
recipe_id)` and a `PUT`-shaped, idempotent write, exactly as
`cookbook_recipes` handles a double tap.

**No `user_taste_profile` table in Phase 1.** It is the obvious optimisation and
it is premature: the profile is an aggregate over ~260 rows that Postgres will
compute in single-digit milliseconds. `code-simplicity` is explicit — "optimise
only when a measurement says this code is the problem". `culina.usecase.duration`
already measures it per handler, so the trigger is a number, not a feeling. §N
records where the table goes when that number arrives.

**Caching.** None beyond what exists. Authenticated responses are already
`no-store`. The *stability* people expect from a suggestion list is provided by
the daily seed (§H.8), not by a cache — which is better, because it survives a
restart and is identical on two devices.

**Background work.** One job, and only for housekeeping: extend or mirror
`ExpiredSessionSweeper` to delete `suggestion_impressions` older than 90 days.
There is no training job, because there is no model.

### G.9 Extensibility

```csharp
public interface ISuggestionRanker
{
    Task<IReadOnlyList<ScoredRecipe>> RankAsync(
        SuggestionContext context, CancellationToken cancellationToken);
}

public sealed record ScoredRecipe(
    RecipeSearchRow Recipe,
    decimal Score,
    IReadOnlyList<ScoreTerm> Terms);   // ordered by contribution, descending

public sealed record ScoreTerm(SuggestionReason Kind, decimal Contribution, string? Subject);
```

One interface, one implementation, per `code-simplicity`'s rule that ports exist
where a technology boundary really is — and here one really is, because the
implementation is SQL and the callers are handlers. The extension points that
actually matter are:

- **`ScoreTerm` is a list.** Adding a term adds a CTE, an enum value and a
  message key. It changes no caller and no component.
- **Similarity is one function.** `similarity(a, b)` is Jaccard over tags and
  ingredients today. If it ever becomes a dot product over stored vectors, the
  call sites do not move.
- **The weights are a record**, not constants — so §M's grid search has something
  to search, and an instance setting could one day expose one.

---

## H. The ranking model

Every term is bounded, signed and named. The total is their sum. A term
contributes to the *reason* only if it dominates (§I).

Notation: `d` = days since; `w` = weight; `hl` = half-life;
`decay(t, hl) = 0.5 ^ (t / hl)`.

### H.1 Personal affinity `A` — do *I* like this one?

```
A = w_A · log1p( Σ  weight(e) · decay(days_since(e), 365) )
             e ∈ this user's events on this recipe
```
with `weight(e)` from §C.7's table (cook 1.0, ×1.3 with a photo, plan 0.6,
shelf 0.8 undecayed, note 0.4, +0.3 for a completed session).

`log1p` because the eleventh cook is not worth what the second was.
**`w_A = 1.0`** — the unit everything else is measured against.

### H.2 Content affinity `C` — do I like recipes *like* this?

Build a profile vector `p` over tags and normalised ingredient names:

```
p[f] = Σ  weight(e) · decay(days_since(e), 365)   for every recipe carrying f
     · idf(f)
idf(f) = ln( (1 + N_recipes) / (1 + N_recipes_with_f) )
```

The IDF term matters more here than anywhere: in a household where 90 % of
recipes contain `Salz`, salt must carry no information. Then

```
C = w_C · cosine(p, v_recipe)          v_recipe = the recipe's own binary tag+ingredient vector
```

**`w_C = 0.8`.** Slightly below direct affinity: having cooked *this* recipe
outranks having cooked things like it.

This term is what carries a never-cooked recipe, and therefore what makes the
system useful in week one.

### H.3 Repetition penalty `R` — not again, we had that Tuesday

The term no general-purpose recommender gets right, and the most
product-specific thing in the design.

```
R = − w_R · exp( − d_household / 10 )
```
where `d_household` = days since **anyone in the household** last cooked it
(§C.7: you ate it either way).

With `w_R = 1.2`:

| days since | penalty |
| --- | --- |
| 0 | −1.20 |
| 2 | −0.98 |
| 7 | −0.59 |
| 14 | −0.30 |
| 21 | −0.15 |
| 30 | −0.06 |
| 60 | −0.003 |

Calibrated so that a beloved recipe (A ≈ 1.2 after many cooks) is *net negative*
the day after it was made, roughly neutral after a week, and fully itself after a
month. A never-cooked recipe has `R = 0`.

τ = 10 days is a single global constant on purpose: a per-recipe interval needs
several repeat observations per recipe, which is a Phase 4 idea (§N.4) and the
Hawkes-process literature's actual subject (§F.5).

### H.4 Rediscovery `U` — we used to make this

Applies **only** where `A > 0` — you cannot rediscover something you never made.

```
U = w_U · clamp( (d_household − 90) / 90, 0, 1 ) · min(A, 1)
```

Zero until three months, ramping to full at six, then flat. Scaled by affinity so
it surfaces things you *liked* and forgot, not things you tried once and
abandoned.

**`w_U = 0.6`.** Large enough to lift a well-loved, long-unmade recipe past a
mediocre familiar one; not large enough to fill the list with archaeology.

### H.5 Context fit `F`

Two kinds, and the distinction is important.

**Hard (a filter, not a score).** `maxMinutes` reuses the existing clause
verbatim, including its documented refusal to treat an unknown time as zero:

> A recipe with no stated time is excluded by a time filter rather than treated
> as taking zero minutes. "I have 25 minutes" asks for recipes known to fit, and
> an unknown time is not an answer.

Same for `tag`, `ingredient`, `cookbookId`. **Reusing these clauses is not an
optimisation; it is the rule that a shelf and the filter bar cannot disagree.**

**Soft (scored).**

```
F_slot   = w_slot · ( slotPrior(recipe, slot) − 0.5 ) · 2      ∈ [−w_slot, +w_slot]
```
`slotPrior` = the share of this recipe's `meal_plan_entries` in that slot,
smoothed toward the household's overall slot distribution with a Laplace prior
(α = 2) so one breakfast entry does not make something a breakfast recipe. Falls
back to the *tag's* slot distribution when the recipe itself has no plan history.
**`w_slot = 0.5`.**

```
F_effort = w_effort · (weekday ? −normalisedEffort : +0.2·normalisedEffort)
```
A Saturday tolerates a project; a Wednesday does not. `normalisedEffort` from the
proxies in §C.6 — **used here as an internal ranking input and never displayed as
a difficulty score**, which is the line `culina-v2-erv` drew. **`w_effort = 0.3`.**

### H.6 Household popularity `P` — the honest residue of CF

```
P = w_P · log1p( Σ decayed cook weight from OTHER members )
```
**`w_P = 0.35`.** Small. It is the "your partner cooks this" signal, and at this
scale that is a genuinely useful piece of information and an entirely
uncontroversial one — as long as it is small enough not to flatten two people's
different tastes into one household average, which is precisely what
`domain-model.md` built the person/household split to prevent.

### H.7 Novelty and freshness `N`

```
N = w_new  · [ never cooked by anyone ]
  + w_fresh · decay(days_since(created_at or imported_at), 30)
```
**`w_new = 0.25`, `w_fresh = 0.3`.** The 30-day half-life is Personalize's
`exploration_item_age_cut_off` default, borrowed knowingly.

This is Culina's version of Personalize **promotions**, and §E.2 argues it
matters *more* here: a household that imports 200 recipes from Tandoor has 200
items with zero interactions, and a pure-relevance ranker would show none of them
ever.

### H.8 Exploration `ε` — stable, not random

```
ε = w_ε · ( hash(userId, recipeId, dateOnly, purpose) / 2^32 − 0.5 )
```
**`w_ε = 0.15`**, roughly the gap between adjacent mid-list candidates.

**The seed is the whole point.** Seeded by the *date*, the list is identical all
evening across devices and refreshes, and different tomorrow. A recommendation
list that reshuffles under your thumb is worse than a slightly worse list that
stays put — the design system already treats spatial stability as a
non-negotiable (§A.9), and this is the same principle one layer down.

`w_ε = 0.15` against Personalize's `exploration_weight` default of 0.3 on a
[0,1] scale: less exploration, deliberately, because the catalogue is small
enough that a household will see most of it anyway.

### H.9 The whole score, and where the weights come from

```
S = A + C + R + U + F_slot + F_effort + P + N + ε
  = 1.00·affinity
  + 0.80·content
  − 1.20·repetition
  + 0.60·rediscovery
  + 0.50·slot        + 0.30·effort
  + 0.35·household
  + 0.25·new         + 0.30·fresh
  + 0.15·exploration
```

**These numbers are not arbitrary and must not be presented as if they were.**
They are *derived from stated ordering rules*, and the rules — not the numbers —
are the specification. Each becomes an xUnit fact in
`tests/Application.UnitTests/Suggestions/RankingOrderTests.cs`:

1. A recipe cooked yesterday never outranks the same recipe's profile cooked
   three weeks ago. → fixes `w_R` relative to `w_A`.
2. A never-cooked recipe matching the person's top three tags outranks a
   twice-cooked recipe matching none. → fixes `w_C` relative to `w_A`.
3. A loved recipe unmade for eight months outranks a moderately-liked one made
   last month. → fixes `w_U`.
4. On a Wednesday evening, a 25-minute recipe outranks a 2-hour one of equal
   affinity. → fixes `w_effort`.
5. A recipe planned as breakfast four times never leads a dinner list. → fixes
   `w_slot`.
6. A recipe another member cooks weekly and this person never has appears in the
   top 20, and never in the top 3. → **bounds `w_P` from both sides.**
7. Exploration never moves a recipe more than three positions. → bounds `w_ε`.
8. With an empty cook log, ranking is stable, non-empty, and not alphabetical.
9. A dismissed recipe appears nowhere for 90 days.
10. Two calls on the same day with the same context return the same order.

**Reasoning.** The weights become a *solution to a stated set of constraints*
rather than taste. When someone later changes one, a test tells them which
product promise they broke. That is the difference between a tuned system and a
fiddled one — and it is the same move `docs/design-system.md` makes with the
contrast contract test: derive the check from the stated rule, never restate it.

Calibration after that is §M.3: offline replay against the cook log, grid-search
over these ten weights, report recall@5 before and after, and change a weight
only when replay and the ordering tests both agree.

### H.10 Worked example

Wednesday, 18:40, September. Anna asks for suggestions; the household has two
years of history.

| Recipe | A | C | R | U | slot | effort | P | N | ε | **S** | Reason shown |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Linsensuppe (made 9×, last 5 months ago) | 0.92 | 0.61 | −0.00 | 0.34 | +0.31 | +0.08 | 0.12 | 0 | +0.04 | **2.42** | "Not since April" |
| Pasta al limone (made 14×, last Sunday) | 1.08 | 0.55 | **−0.83** | 0 | +0.22 | +0.11 | 0.21 | 0 | −0.06 | **1.28** | — (no dominant term) |
| Ofengemüse (imported June, never cooked) | 0 | 0.71 | 0 | 0 | +0.18 | +0.09 | 0 | 0.25+0.06 | +0.09 | **1.38** | "You cook a lot with [Aubergine]" |
| Boeuf bourguignon (made 2×, last year) | 0.31 | 0.28 | 0 | 0.18 | +0.12 | **−0.27** | 0.05 | 0 | +0.02 | **0.69** | — |

**Reasoning about what this shows.** The household staple that was made four days
ago is *correctly demoted* without being hidden — it is still second, because it
is still the thing they like most. The imported-but-never-cooked recipe is
competitive purely on content, which is the cold-start case working. The
three-hour braise is pushed down by the weekday effort term and would lead the
same list on a Saturday. None of this required a model.

---

## I. Explainability

**The rule: a reason is the term that actually won, or there is no reason.**

The ranker already returns `ScoreTerm[]` ordered by contribution. The reason is
`Terms[0]` **if and only if** its contribution is ≥ 35 % of the total positive
score. Otherwise the card shows its ordinary meta line and nothing else.

**Reasoning.** An unexplained good suggestion is fine; an invented explanation is
a lie the user will eventually catch, and catching it discredits every other
suggestion on the screen. This is the same instinct as `matchLineFor()` in
`recipeMeta.ts`, which returns `null` rather than saying "uses 0 of 0" — the
codebase already has this discipline and this just follows it.

| `SuggestionReason` | Shown as (EN) | Shown as (DE) | Derived from |
| --- | --- | --- | --- |
| `Affinity` | "You've made this 9 times" | "Du hast das 9× gekocht" | `A` |
| `Rediscovery` | "Not since April" | "Zuletzt im April" | `U`, month of `max(made_at)` |
| `ContentTag` | "You often cook {suppe}" | "Du kochst oft {Suppe}" | top-contributing feature in `C` |
| `ContentIngredient` | "You cook a lot with {Aubergine}" | "Du kochst viel mit {Aubergine}" | top-contributing feature in `C` |
| `Season` | "Your kitchen cooks this in November" | "Bei euch ein November-Essen" | `S`, only past the evidence gate (§G.6) |
| `Slot` | "Usually a weeknight dinner" | "Meist ein Abendessen unter der Woche" | `F_slot` |
| `Household` | "Anna cooks this often" | "Anna kocht das oft" | `P` — **display name, never a bare id** |
| `New` | "New in your book since June" | "Seit Juni im Buch" | `N` |
| `Similar` | "Like Carbonara" | "Ähnlich wie Carbonara" | `Like` purpose |
| `Quick` | "Ready in 25 minutes" | "In 25 Minuten fertig" | `F` under a `maxMinutes` context |

Two constraints from the repo, both mandatory:

- Message keys are **appended to the end** of `messages/{en,de}.json`, never
  re-sorted — the files are grouped by feature, and re-serialising them breaks
  `pnpm messages` with an unrelated error.
- Counts use the message-format plural array form, or `{count} recipes` reads
  "1 recipes".

**The tap target.** A reason is a small, quiet line, not a button. Tapping the
*card* opens the recipe, as it does everywhere else. There is no "why am I seeing
this?" dialog: the reason is already the whole answer, and a second surface
explaining the first one is an admission that the first one did not work.

---

## J. Backend integration

Precisely where this lands, in the existing structure.

### J.1 New files

```
src/backend/src/Domain/Suggestions/
    SuggestionPurpose.cs         enum: Browse | Decide | Like
    SuggestionReason.cs          enum, one per §I row
    RankingWeights.cs            record of the ten weights, with Default
    SuggestionErrors.cs          Result errors, per dotnet-result-pattern

src/backend/src/Application/Abstractions/
    SuggestionContext.cs         §G.2
    ISuggestionRanker.cs         §G.9
    ISuggestionFeedback.cs       dismissals + impressions

src/backend/src/Application/Suggestions/
    GetSuggestions/GetSuggestionsQuery.cs      + handler + mappings
    Dismiss/DismissSuggestionCommand.cs        + handler

src/backend/src/Contracts/Suggestions/GetAll/
    Response.cs  Suggestion.cs  ReasonCode.cs

src/backend/src/Api/Endpoints/Suggestions/
    SuggestionsEndpoints.cs
    GetAll/V1/GetSuggestionsEndpoint.cs
    Dismiss/V1/DismissSuggestionEndpoint.cs

src/backend/src/Infrastructure/Persistence/Suggestions/
    SuggestionRanker.cs          the one SQL statement
    SuggestionScoringSql.cs      the CTEs, kept apart like RecipeSearchSql
    SuggestionFeedbackRepository.cs
    Migrations/0012_suggestions.sql
```

### J.2 Changed files — deliberately few

| File | Change |
| --- | --- |
| `Application/Abstractions/RecipeSearch.cs` | one enum member: `RecipeSort.Suggested = 6` |
| `Infrastructure/Persistence/Recipes/RecipeSearchSql.cs` | one arm each in `OrderBy`, `ResumePredicate`, `KeysOf` — resuming on `(score, id)` exactly as `MostCooked` resumes on `(cook_count, id)` |
| `Infrastructure/Persistence/Recipes/RecipeSearcher.cs` | join the scoring CTEs when the sort is `Suggested`; add `score` to the projection |
| `Api/.../GetRecipesRequestExtensions.cs` | accept `sort=suggested` |
| `Api/Extensions/EndpointExtensions.cs` | register two endpoints by name (no scanning — the architecture test enforces this) |
| `Infrastructure/DependencyInjection.cs` | register the ranker and the feedback repository |
| `Application/Telemetry/CulinaTelemetry.cs` | `culina.suggestions.shown`, `culina.suggestions.accepted`, `culina.suggestions.dismissed` |

`GetRecipesQueryHandler` needs **no change**: it already resolves a cookbook
scope and hands a `RecipeSearch` to the repository, and `Suggested` is one more
sort inside that contract.

### J.3 Where the write-side hooks are (and why there are none)

There is deliberately **no event bus, no domain event, and no hook in
`RecordCookedCommandHandler`.** Every signal in §C.1 is already durably written
by the handler that owns it, and the ranker reads those tables directly. A
notification pipeline whose only job is to tell a read-model about a row that is
already committed is exactly the "layer of indirection whose only job is to
forward a call" that `code-simplicity` forbids.

The only two writes this feature owns are the dismissal and the impression, and
both belong to the suggestion handler that caused them.

### J.4 Access control and the existing conventions

- Membership is checked exactly as `GetRecipesQueryHandler` does — non-member
  gets `404`, never `403`, per the cross-cutting rule.
- The ranker filters on `household_id` in the same `WHERE` as everything else.
- `DELETE`/`PUT` on a dismissal is idempotent and returns `204` on a repeat,
  following `cookbook_recipes`.
- `POST`/`PUT` carry CSRF; `GET /suggestions` does not mutate, which is what
  keeps the safe-method CSRF exemption sound. **Impressions are therefore
  written by the client's explicit call, not as a side effect of the `GET`** —
  a `GET` that writes would break a rule the API doc states plainly.

**Reasoning.** That last point is a genuine design constraint and worth being
explicit about: impressions are recorded by `POST /suggestions/impressions` sent
by the client when the set is actually rendered. That is also *more* correct
than logging at response time, because a response that never painted was never
an impression.

---

## K. Frontend and product integration

Five integration points, ranked by value per kilobyte. **Four of them add no new
component.**

### K.1 The library's sort — the highest-leverage change in this document

`(app)/+page.svelte` is the app's front page. It currently lists by
`-updatedAt` and labels it `recipes.list.recent`.

**Change:** once a household has ≥ 20 cook-log entries, the default becomes
`sort=suggested`, and the existing `.collection-note` says so — *"Sorted for
tonight"* rather than *"Recently updated"* — with the sort control offering
"Recently updated" as one tap away.

Why this is right and not a hijack:
- The list, the cards, the grid, the paging, the search and the quick filter are
  **completely unchanged**. It is a different `order by`.
- It composes: type "chicken" and the results are still ranked for you. Tap the
  30-minute chip and they are ranked for you *within* 30 minutes. This is
  "what should I cook?" and "I have 25 minutes" being **the same feature**, which
  is the thing separate recommendation screens can never achieve.
- The threshold means a new household never sees an order it has no basis for.
- The label is honest about what it did. A list whose order changed without
  saying so is the thing that makes people distrust an app.

### K.2 The featured slot — a reason where an arbitrary choice is now

`FeaturedRecipe.svelte` currently shows *the first recipe with a photo*, under
the eyebrow `recipes.featured.eyebrow`.

**Change:** it shows the **top suggestion**, and the eyebrow becomes its reason —
"Good for tonight", "Not since April", "New in your book". Same component, same
geometry, same photo treatment, one different selection rule and a dynamic
eyebrow.

**Reasoning.** This is the whole "contextual, not promotional" requirement,
solved by deletion rather than addition: the app already had a hero slot filled
by an accident of photography, and it is now filled by an answer. No section
header saying "Recommended for you" is needed, because the panel never claimed to
be a recommendation — it claimed to be worth looking at, and now it is.

Fallback: if no suggestion clears the reason threshold, the eyebrow stays
"Featured" and nothing about the screen changes. The feature degrades to exactly
today's behaviour.

### K.3 The plan's empty day — the strongest context in the product

`plan/+page.svelte` opens `RecipePicker` with the date and slot already chosen
(`adding = day.date; slot = 'dinner'`), and the picker currently shows the
library newest-first until somebody types.

**Change:** when the picker opens with nothing typed, its list is
`GET /suggestions?forDate=…&slot=…&exclude=<this week's recipes>`. The moment a
query is typed it reverts to search, exactly as it does now.

**Reasoning.** Nothing on screen changes. No section header, no carousel, no new
sheet. The blank state of an existing sheet simply becomes useful, and it does so
with the richest context the product ever has — an explicit date, an explicit
meal slot, and a known set of things already planned that week. `RecipePicker`
already takes a `taken` prop for "recipes this caller has already used", so the
exclusion mechanism is there.

This also closes the loop the brief asks about: **the plan feeds the ranker
(slot priors, intent) and the ranker fills the plan.**

### K.4 The recipe page — the one new component

Below `PersonalNotePanel`, a `SimilarRecipes.svelte` strip: a heading, three
`RecipeCard`s, horizontally scrollable on phones.

**Reasoning.** This is the only place that genuinely needs new markup, because
nothing on the recipe page is currently a list of other recipes. It is also
where `docs/design-system.md`'s existing rule applies verbatim: *"Cookbook
shelves scroll horizontally within the page. Keyboard focus reveals the whole
card… snapping is disabled while focus is in the shelf."* Reuse that shelf, do
not invent a carousel.

It renders nothing at all when fewer than three candidates clear a similarity
floor. **A section that sometimes shows two weak matches is worse than a section
that is sometimes absent** — and absence costs nothing on a page that is already
complete without it.

### K.5 Empty states

`EmptyState` already takes an `action` snippet. Three places gain suggestions:

- Search found nothing → "Nothing matched 'risotto'." + three suggestions.
- The week has a hole → already covered by K.3.
- A brand-new library → **unchanged**. "Write your first recipe" is the right
  answer; suggesting from an empty library is impossible and pretending
  otherwise would be the worst first impression the app could make.

### K.6 What is deliberately not built

- **No home page or dashboard.** `docs/navigation-research.md` fixed five
  destinations and gave the evidence; a sixth for recommendations would be a
  regression against a decision already taken on better grounds than this
  feature has.
- **No "Recommended for you" heading anywhere.** Contextual titles only, and
  each one is a *reason* rather than a category.
- **No infinite suggestion feed.** Bounded sets everywhere. The library's sort is
  the only paged surface, and it is paging *the library*, which is finite and
  theirs.
- **Nothing in cook mode or in the "I made it" flow** (§B.6).
- **Nothing on the shopping list** (§B.6).

---

## L. UX details

The small things, in the vocabulary the design system already uses.

**Loading.** `createLoadingState()` and nothing else — 150 ms before anything
appears, 300 ms minimum once shown, 10 s to "taking longer than usual". The
suggestion strip uses `RecipeCardSkeleton`, which already exists and already
reserves the right geometry.

**No layout jump, ever.** The featured panel reserves its height before the
suggestion resolves, because it already has `min-height: 18rem`. The similar
strip reserves three card slots. `docs/accessibility-and-performance.md` holds
CLS under 0.02 on list routes and says anything above a rounding error means a
box was forgotten — a suggestion that arrives late and pushes the page down would
be that box.

**Refresh is not a button.** There is no shuffle control. The list is seeded by
the day (§H.8), so it is the same all evening and different tomorrow, and that is
the promise: *the app is not fidgeting*. A refresh button teaches people that the
first answer was arbitrary.

**Dismissal is a swipe or a small ⨯, and it is undoable.** One tap, an optimistic
removal, and a `toaster` Undo — exactly the pattern the week planner already uses
for a moved meal ("A drop is optimistic and answered with an Undo toast rather
than a confirmation"). The removed card's space collapses over
`--duration-base`; the rest of the list **does not re-rank**, because re-ranking
under the thumb after a dismissal is how you lose the item you were about to tap.
The replacement appears at the end, not in the gap.

**Reasons are quiet.** `--text-sm`, `--text-muted`, one line, never wrapping to
two, truncated with an ellipsis rather than reflowing the card. The eyebrow
position already exists on both `RecipeCard` and `FeaturedRecipe`.

**Motion.** Nothing new. `docs/design-system.md` has a closed motion inventory —
"Anything not here does not animate" — and a suggestion list is not on it. The
card collapse on dismissal reuses the shopping item check's 200 ms row-sink,
which is already listed.

**Mobile.** The similar strip is the existing horizontal shelf with its
established focus and snapping rules. The featured panel already collapses to one
column below 64rem. Nothing needs a new breakpoint; `docs/design-system.md` is
explicit that a breakpoint per control is a smell.

**Accessibility.** The strip is a `<section>` with a real heading, so it appears
in the landmark list and can be skipped. The reason line is plain text inside the
card's link, so a screen reader reads "Linsensuppe, not since April" as one
label. The dismiss control is a real `<button>` with a `VisuallyHidden` label
naming the recipe — "Dismiss Linsensuppe" — because "Dismiss" ⨯ 5 is unusable.
Touch targets ≥ 44 px, per the existing floor.

**Print.** `tokens/print.css` exists and the recipe page already hides its back
link on paper. Suggestions print as nothing.

**Offline / failure.** A failed suggestion request renders **nothing** — no error
state. The library still lists, the recipe page still reads. This is the one
feature in the app that is pure enhancement, and it should fail the way an
enhancement fails: silently, leaving a complete page behind.

---

## M. Evaluation

Conventional A/B testing is statistically meaningless at n=4 — a two-arm test
needs hundreds of sessions per arm and this household produces ~5 a week. Three
methods that *are* valid at this scale:

### M.1 Offline replay — the primary method, and it works today

The cook log is dated, so the counterfactual is computable **without shipping
anything**:

> For each cook-log entry at time `t`, reconstruct the world as of `t − 1s`
> (cook log, plan, shelves, recipes as they were), ask the ranker for its top-k,
> and check whether the recipe actually cooked at `t` is in it.

Metrics: **recall@5, recall@10, MRR**. A 300-entry history yields ~300
evaluation points, minus a warm-up window — enough to compare two weight vectors
even though it is nowhere near enough to *fit* one.

This lives in the existing test infrastructure (`IntegrationTests` already runs
Testcontainers Postgres) as a runnable harness, not a service. It answers, in
seconds, "is this change better?" — the question a small installation otherwise
has no way to ask.

**Honest caveat.** The objective is "predict what they cooked", and once the app
starts influencing what they cook, the target is partly the ranker's own output.
That bias is small for the first year (today the app suggests nothing, so the
existing history is clean) and grows after. Mitigation: evaluate on a **frozen
pre-launch slice** as a permanent reference point, alongside the rolling window.
Report both.

### M.2 Online metrics — three counters, no more

On the existing `Culina` meter, so they reach whatever OTLP endpoint the operator
already runs and **nowhere else**:

| Metric | Reads as |
| --- | --- |
| `culina.suggestions.shown{purpose}` | denominator |
| `culina.suggestions.accepted{purpose, action}` | opened / planned / shopped / cooked |
| `culina.suggestions.dismissed{purpose, reason}` | the loudest signal, because dismissal costs effort |

**The one number that matters is `cooked`**, not `opened`. Click-through is the
metric that makes recommender systems optimise for thumbnails; Culina's stated
position is that what you cook beats what you claim, and the evaluation must
agree with the product. Opens are a diagnostic, not a goal.

Quality metrics computable from `suggestion_impressions` without any user
involvement:

- **Coverage** — distinct recipes suggested over 90 days ÷ library size. A
  healthy value is well above 30 %; a collapse toward 5 % means the profile has
  over-specialised and `w_ε` or λ needs raising.
- **Repetition rate** — how often the same recipe is suggested to the same person
  within 7 days. Should be near zero; this is what §C.4 pays for.
- **Novelty share** — proportion of suggestions with zero prior interactions.
- **Gini over suggestion frequency** — one number for "is it showing eight
  recipes forever?".

These four are the ones that catch the small-catalogue failure mode, and they
need no user feedback at all.

### M.3 Weight calibration without an ML pipeline

Ten weights, one scalar objective (recall@5 from M.1), a few hundred evaluation
points. **Coordinate descent over a coarse grid** — hold nine weights, sweep the
tenth over ~9 values, keep the best, repeat twice. That is ~200 replays, seconds
of compute, and it is entirely reproducible.

Two guardrails, both non-negotiable:

1. **The §H.9 ordering tests must still pass.** A weight vector that wins on
   recall by making the app suggest Tuesday's dinner again on Wednesday has not
   won. The ordering rules encode product promises that a metric cannot see.
2. **Weights change in a commit with the before/after numbers in the message** —
   the same discipline `docs/accessibility-and-performance.md` demands of raising
   a weight budget: "Raise a budget deliberately, in a commit that says what was
   added and why it was worth it — never to make a build pass."

**No per-instance auto-tuning.** A self-hosted app whose ranking silently drifts
per installation is unsupportable: two people comparing notes cannot reproduce
each other's behaviour, and a bug report becomes untriageable. Weights are code.

### M.4 The qualitative check that actually decides it

**n=4 means you can just ask.** Once a quarter, the maintainer reads their own
top ten and marks each plausible / implausible. Ten judgements from someone who
knows what they want for dinner outweighs a CTR curve built from forty
impressions. At this scale, that is not a fallback for lack of statistics — it is
the *higher-quality* signal, and the honest thing to say is so.

---

## N. Evolution path

### Phase 1 — Rank the library honestly (~1 week)

The smallest thing that changes the product's answer to its own central question.

- `RankingWeights`, `ISuggestionRanker`, `SuggestionRanker` (one SQL statement).
- Terms: **A, C, R, U, P, N** — affinity, content, repetition, rediscovery,
  household popularity, novelty. No slot, no season, no exploration yet.
- `RecipeSort.Suggested` + the four `RecipeSearchSql` arms.
- Migration `0012`: `suggestion_dismissals` only.
- Library default sort flips at ≥ 20 cook-log entries; `.collection-note` says so.
- `FeaturedRecipe` shows the top suggestion with its reason as the eyebrow.
- The ten ordering tests from §H.9.

New tables: **1.** New endpoints: **0.** New components: **0.** New
dependencies: **0.**

**Done when:** the front page answers "what should I cook?" instead of "what did
I edit?".

### Phase 2 — Context, and a reason (~1 week)

- `GET /suggestions` + `SuggestionContext` + `SuggestionPurpose`.
- Terms added: `F_slot`, `F_effort`, `ε`.
- MMR diversity, reasons with the 35 % dominance rule, DE+EN strings.
- `suggestion_impressions` + `POST /suggestions/impressions` + the 90-day sweep.
- `RecipePicker`'s blank state uses `forDate` + `slot` + this week's exclusions.
- `SimilarRecipes.svelte` on the recipe page (`purpose=like`).
- Search-empty-state suggestions.
- The three OTel counters.

**Done when:** tapping an empty Thursday offers something you would actually
cook on a Thursday, and says why.

### Phase 3 — Learn from the household (~1 week, once there is history)

- Observed per-tag seasonality with the evidence gate (§G.6).
- The offline replay harness (§M.1) and the coordinate-descent calibration (§M.3).
- Coverage / repetition / novelty / Gini reported from impressions.
- **Only if `culina.usecase.duration` says so:** materialise the taste profile
  into `user_taste_profile`, refreshed by a `BackgroundService`.

**Done when:** a weight change can be defended with a number.

### Phase 4 — Only on a stated trigger, possibly never

Each item names the condition that would justify it. **None of these conditions
is expected to occur at 2–8 users, and writing them down is how the architecture
stays honest about that.**

| Would build | Only when |
| --- | --- |
| Per-recipe learned repetition interval (Hawkes-style) | ≥ 10 repeat observations for a meaningful number of recipes |
| Text embeddings + pgvector for similarity | > 5,000 recipes, or similarity visibly failing across DE/EN |
| Matrix factorization / latent factors | > 20 active users **and** > 20,000 interactions |
| Bandit over ranking weights | > 5,000 impressions per year |
| Per-instance auto-tuning | never (§M.3) |

**Nothing in Phases 1–3 blocks any of these.** `ISuggestionRanker` is the seam;
`ScoreTerm` is a list; similarity is one function. Each row above is a changed
implementation behind an unchanged interface.

---

## O. Final recommendation

### O.1 Build now

1. **`sort=suggested` on `GET /recipes`**, scored by §H's terms in one SQL
   statement, reusing every filter clause that already exists.
2. **The library's default sort flips** at ≥ 20 cook-log entries, labelled
   honestly in the note element that already exists.
3. **`FeaturedRecipe` shows the top suggestion**, with its reason as the eyebrow.
4. **`GET /suggestions`** — bounded, contextual, explained — plus dismissal.
5. **Three product placements, one new component**: the plan picker's blank
   state, the recipe page's similar strip, the search empty state.
6. **The ten ordering tests**, which are the real specification of the weights.

### O.2 Deliberately do not build now

| Not building | Because |
| --- | --- |
| Amazon Personalize, Recombee, any managed service | ~$144–389/month floor by my arithmetic from published rates, 1,000-interaction minimum, private cooking history leaving the instance, and AWS's own <5-interaction "cold item" definition covering ~95 % of a household library |
| Gorse or any recommender service | +2 containers and a Redis, a duplicated catalogue, and a new upgrade path for every installation — to rank 300 items 300 times a month |
| ML.NET / matrix factorization | an 8 × 300 matrix that is 99.7 % empty, native binaries in a chiseled read-only container, >100 MB of image, and an unexplainable output |
| Embeddings + pgvector | a ~450 MB multilingual model (4× the app image) and a base-image change plus a manual `CREATE EXTENSION` for existing installs, to index 300 rows that need no index |
| Recipe-view / search telemetry | high volume, low information, highest privacy cost in the design, and the outcome it proxies is already recorded |
| Curated ingredient→season tables | already rejected in `culina-v2-erv`, correctly, for the same reason as the pantry |
| Difficulty or nutrition scores | already rejected — a number users would rightly distrust |
| Weather | the first unsolicited outbound call in the product, a location nobody gave, a CC-BY obligation, for a signal nearly collinear with the month |
| Onboarding preference screens | asks before the person has seen anything, needs a taxonomy that does not exist, and is outdated by the third cook-log entry |
| A home page, a dashboard, a "Recommended for you" feed | contradicts a navigation decision already taken on better evidence |
| Bandits, LTR, per-instance auto-tuning | reward too slow, labels absent, and unsupportable respectively |

### O.3 Extension points to prepare anyway

- **`ISuggestionRanker`** — one port, one implementation, the seam for every
  Phase 4 row.
- **`ScoreTerm[]`** rather than a scalar — a new term is a CTE, an enum value and
  a message key, and changes no caller.
- **`RankingWeights` as a record**, not constants — so calibration has something
  to search.
- **`similarity(a, b)` as one function** — Jaccard today, a dot product one day,
  same call sites.
- **`SuggestionContext` as a record** — a new context field is a field, not a new
  "mode".
- **`SuggestionPurpose` with exactly three members** — the discipline that stops
  it becoming nine.
- **The scoring CTEs kept in their own file**, as `RecipeSearchSql` is kept apart
  from `RecipeSearcher`, so ordering can be read on its own.

**A worked example of why these seams are the right ones.** `culina-v2-bk9l`
proposes deriving nutrition automatically from Open Food Facts' ingredients
taxonomy. Nothing in this document changes to accommodate it, and it is
deliberately *not* built here. When it lands, a nutrition term is a CTE, a
`SuggestionReason` value and a message key; a per-serving ceiling is a
`SuggestionContext` field beside `MaxMinutes`, not a new `SuggestionPurpose`;
and its weight is searchable by the same calibration in §M.3.

One condition, recorded now so it is not discovered later: **any nutrition term
must read the ingredient's resolution status, not just its value.** Ranking on a
number that is unknown for a third of the library would silently sort the
unresolved third to the bottom — a ranking bug that looks like a preference, and
exactly the class of quiet wrongness §I's dominance rule exists to prevent.

### O.4 Start collecting immediately

**Nothing new, except dismissals.** That is the headline finding of this
document: the epic `culina-v2-erv` was right that *"Culina already records more
than it spends"*, and recommendations are the clearest case of it.
`cook_log_entries.made_at`, `meal_plan_entries.slot` and `.on_date`,
`cookbook_recipes.added_at` and `.added_by`, `personal_notes.updated_at`,
`cook_log_entries.image_hash` and `.servings`, `recipes.created_at` and
`recipe_origins.imported_at` are **all written today and all unread**. Phase 1
reads them.

Three things to add, in order of value:

1. **`suggestion_dismissals`** — Phase 1. The only signal that cannot be derived.
2. **`suggestion_impressions`** — Phase 2. Earns its place by preventing weekly
   repetition, not by being analytics.
3. **`shopping_list_item_sources`** — its own bead, for the shopping list's own
   documented correctness (§A.5/§C.5). The ranker reads it for free when it
   lands.

### O.5 How it grows up

Every term in §H is already a function of the cook log, so the same code that is
weak in week one is strong in year three **without a single line changing**:
affinity sharpens as entries accumulate, content profiles get IDF that means
something, repetition intervals become observable, rediscovery only becomes
possible at all after six months, and seasonality crosses its evidence gate in
year two. The first upgrade that requires new *code* rather than new *data* is
per-recipe repetition intervals, and the trigger for it is written down.

### O.6 The one-sentence recommendation

**Build a ranking, not a recommender**: one SQL statement that scores the
household's own recipes on named, explainable terms; expose it as a sort on the
list the app already has and as one small contextual endpoint; put it where
people already decide — the front page, the featured slot, the empty Thursday —
and let it get better as the cook log fills, because every term is already
written down and nobody is reading it yet.

---

## Sources

**This repository** (the source of truth): `README.md`, `docs/domain-model.md`,
`docs/api.md`, `docs/design-system.md`, `docs/navigation-research.md`,
`docs/accessibility-and-performance.md`, `docs/deployment.md`,
`Infrastructure/Persistence/Migrations/0001`–`0011`,
`Infrastructure/Persistence/Recipes/RecipeSearcher.cs`,
`Application/Abstractions/RecipeSearch.cs`, `scripts/db-init.sh`,
`compose.yaml`, `compose.prod.yaml`, `Dockerfile`, bead `culina-v2-erv`.

**External:**

- [Amazon Personalize — User-Personalization-v2 recipe](https://docs.aws.amazon.com/personalize/latest/dg/native-recipe-user-personalization-v2.html)
- [Amazon Personalize — User-Personalization recipe (exploration hyperparameters)](https://docs.aws.amazon.com/personalize/latest/dg/native-recipe-new-item-USER_PERSONALIZATION.html)
- [Amazon Personalize — Similar-Items recipe (cold item = <5 interactions)](https://docs.aws.amazon.com/personalize/latest/dg/native-recipe-similar-items.html)
- [Amazon Personalize — filter expression structure](https://docs.aws.amazon.com/personalize/latest/dg/creating-filter-expressions.html)
- [Amazon Personalize — pricing](https://aws.amazon.com/personalize/pricing/)
- [Gorse (gorse-io/gorse)](https://github.com/gorse-io/gorse)
- [Microsoft.ML.Recommender on NuGet](https://www.nuget.org/packages/Microsoft.ML.Recommender)
- [DiscoRec on NuGet](https://www.nuget.org/packages/DiscoRec)
- [pgvector](https://github.com/pgvector/pgvector)
- [Open-Meteo terms](https://open-meteo.com/en/terms)
- Li, Sun, Ma, Sun & Zhang, *Recommender for Its Purpose: Repeat and Exploration
  in Food Delivery Recommendations*, [arXiv:2402.14440](https://arxiv.org/abs/2402.14440)
