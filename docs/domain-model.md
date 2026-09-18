# Culina — domain model

The authoritative description of Culina's entities, their invariants and their
persistence shape. Every backend bead references this document instead of
restating it.

Layer rules come from `dotnet-project-setup`: the types below live in `Domain`,
know nothing about HTTP or SQL, and reach the wire only through the
per-operation DTOs in `Contracts` (`dotnet-layer-mapping`).

## Overview

```
User ──< HouseholdMember >── Household ──< Recipe
                                  │          ├──< IngredientGroup ──< RecipeIngredient
                                  │          ├──< Step ──< StepIngredientRef
                                  │          └──< RecipeTag >── Tag
                                  ├──< Cookbook ──< CookbookRecipe >── Recipe
                                  └── ShoppingList ──< ShoppingListItem ──< ShoppingListItemSource

User ──< PersonalNote   ── Recipe     per person, never mutates the recipe
User ──< CookLogEntry   ── Recipe     "made it", dated
User ──< CookSession    ── Recipe     resume: current step + servings
```

Two ownership axes, and keeping them apart is the point of the model:

- **Household-owned**: recipes, tags, the shopping list. Every member sees and
  edits the same object.
- **Person-owned**: personal notes, cook log, cook sessions, user settings.
  Two people in one household can disagree about a recipe without either of
  them editing it.

## Identity

### User

| Field | Type | Notes |
| --- | --- | --- |
| `Id` | `Guid` | v7, time-ordered |
| `Email` | `Email` (VO) | lowercased, trimmed, unique |
| `DisplayName` | `DisplayName` (VO) | 1–80 chars, trimmed, not blank |
| `PasswordHash` | `string` | Argon2id encoded string, never logged |
| `CreatedAt` | `DateTimeOffset` | |
| `Version` | `long` | bumped by every write, drives the ETag |

Invariants: email parses and is unique; display name is non-blank after
trimming. `Email.Create` and `DisplayName.Create` return `Result<T>` — they do
not throw (`dotnet-result-pattern`).

### UserSettings — person-owned, one row per user

`Locale` (`de` | `en`), `Theme` (theme id, default `warm-paper`),
`Mode` (`light` | `dark` | `system`), `MeasurementSystem` (`metric` | `imperial`,
v1 stores it and always renders metric — see `scaling-rules.md`).

### Household

`Id`, `Name` (1–80 chars), `CreatedAt`, `Version`.

### HouseholdMember

`HouseholdId`, `UserId`, `Role` (`owner` | `member`), `JoinedAt`.
Composite primary key `(HouseholdId, UserId)`.

`HouseholdMembershipPolicy` (Domain) owns every membership rule, so no handler
re-derives them:

- A household always has **at least one owner**. Removing or demoting the last
  owner returns `households.last_owner`.
- Only an **owner** may invite, remove members, rename, or delete a household.
- A member may remove **themselves** (leaving), unless they are the last owner.
- A user may belong to several households.

### HouseholdInvitation

`Id`, `HouseholdId`, `Code` (32 chars, cryptographically random, URL-safe,
stored **hashed** — treat it as a credential), `CreatedBy`, `CreatedAt`,
`ExpiresAt` (default +14 days), `RedeemedBy?`, `RedeemedAt?`.

Single-use. Redeeming an expired, unknown or already-redeemed code returns the
**same** error (`households.invitation_invalid`) so a code cannot be probed.

### Session

`Id` (the opaque cookie value's identifier), `UserId`, `CreatedAt`,
`LastSeenAt`, `ExpiresAt`, `CsrfTokenHash`, `IpAddress`, `UserAgent`,
`RevokedAt?`. Specified by `cookie-auth-and-security`.

## Recipes

### Recipe — household-owned

| Field | Type | Notes |
| --- | --- | --- |
| `Id` | `Guid` | |
| `HouseholdId` | `Guid` | ownership; every query filters on it |
| `Title` | `string` | 1–200 chars, required — the only required field |
| `Description` | `string?` | ≤ 2000 chars |
| `Language` | `de` \| `en` | the language the *content* is written in, independent of UI locale |
| `BaseYield` | `Yield` (VO) | `Amount` (decimal > 0, ≤ 1000) + `Kind` (`servings` \| `pieces`) |
| `PrepMinutes` | `int?` | ≤ 10000 |
| `CookMinutes` | `int?` | ≤ 10000 |
| `ImageId` | `Guid?` | see `RecipeImage` |
| `CreatedBy` | `Guid` | user id |
| `CreatedAt` / `UpdatedAt` | `DateTimeOffset` | |
| `Version` | `long` | ETag / `If-Match` |

`TotalMinutes` is derived (`Prep + Cook`), never stored — it is what the time
filter sorts on, computed in SQL.

**A recipe with only a title is valid.** That is deliberate: it is the
placeholder for "I want to write this down later". No other field is required,
so the create form can never become a wall.

`Yield.Kind = pieces` exists so "12 muffins" scales as honestly as
"4 portions".

### IngredientGroup

`Id`, `RecipeId`, `Name?`, `SortOrder`.

Every recipe has **one group with a null name**, created implicitly. The
concept is therefore invisible until someone names a group ("For the dough"),
which is how progressive disclosure is supposed to work — the UI shows no
grouping affordance until a second group exists.

### RecipeIngredient

| Field | Type | Notes |
| --- | --- | --- |
| `Id` | `Guid` | referenced by step tokens and shopping-list sources |
| `GroupId` | `Guid` | |
| `SortOrder` | `int` | |
| `Quantity` | `decimal?` | null is legitimate — "salt" has no amount |
| `Unit` | `Unit?` | null when the amount is a bare count or absent |
| `Name` | `string` | 1–120 chars, the *shoppable* noun: "butter" |
| `Note` | `string?` | ≤ 200 chars, preparation: "finely chopped", "room temperature" |

### Unit

An open vocabulary, carried as a code rather than as an enum.

Thirteen units are **built in** — `g kg ml l tsp tbsp piece clove bunch slice
can pack pinch` — and they are the ones that *convert*: a kilo is a thousand
grams for everyone. They are published in the OpenAPI document so a generated
client shares that table rather than redeclaring it.

Anything else is a unit **a household wrote**, and writing it is the whole of
adding one. There is no catalogue table and no screen for it: a unit exists
because something is measured in it, so the list a picker offers and the recipes
actually written can never disagree. `GET /households/{id}/units` returns both
halves.

What a household adds joins the **Count** family. It scales with the portions
and it sums with itself, and it converts to nothing — nobody knows how much a
`Schuss` is, and a shopping list that claimed to would be inventing the number.
That is what makes an open vocabulary safe rather than reckless.

Compared case-insensitively, so `Schuss` and `schuss` are one unit, and stored
as written, so a German noun keeps its capital. Letters and single spaces only:
`200g` is an amount that lost its space, not a unit.

**`Note` is separate from `Name` on purpose.** "butter, finely chopped" and
"butter" must merge into one shopping-list line; if the preparation lives in the
name they never will.

### Ingredient suggestions

There is no ingredient table. An ingredient is whatever somebody types, and
`GET /households/{id}/ingredients?q=…` only *suggests*:

1. the names this household's recipes already use, ranked by how often, because
   after a few recipes a kitchen's own words are how these particular people
   talk about food;
2. then a short seeded list (`CommonIngredients`, ~120 entries, DE + EN with a
   shopping section), so an empty kitchen is offered something.

Named in the **recipe's** language, not the reader's: somebody with an English
app writing down a German recipe wants `Kartoffeln`, and an English word in that
ingredient list is a word nothing else in it will match.

`CommonIngredients` is not `SectionKeywords` and neither replaces the other.
That table matches *stems* inside a phrase — `strawberr`, so both "strawberry"
and "strawberries" find the produce aisle — which is right for guessing and
wrong for offering. These are names a person would be happy to see typed into
their recipe, which is right for offering and useless for matching.

### Step

`Id`, `RecipeId`, `SortOrder`, `Text` (1–4000 chars), `DurationSeconds?`.

`Text` stores **inline tokens** referencing ingredients:

```
Melt [[ingredient:0f1c…]] in the pan and add [[ingredient:7b22…]].
```

Tokens, not character offsets: offsets rot the instant someone edits a word.

`StepIngredientRef(StepId, RecipeIngredientId)` is a derived index table,
rebuilt from the text on every save. It exists so "which steps use the yeast?"
is one indexed query rather than a `LIKE` scan.

**Invariants**
- Every token in `Text` must reference a `RecipeIngredient` of the *same*
  recipe. A token pointing elsewhere is rejected with `recipes.unknown_ingredient_reference`.
- Deleting an ingredient that steps still reference is rejected with
  `recipes.ingredient_in_use`, naming the steps. (The UI unlinks first.)
- `DurationSeconds` ≤ 86400.

The API never exposes the raw token string. Responses carry **segments**; see
`api.md`.

**How an author writes one.** In the editor a reference is typed as `@butter`,
and an `@` opens a picker of the recipe's own ingredients. A name the recipe
does not have yet is offered as "add it", so writing the method builds the
ingredient list rather than repeating it. What is typed is what is stored —
there is no hidden token and no rich-text editor — which is why undo, every
input method and every screen reader work without help. The pill carrying the
scaled amount is what *reading* renders.

Nothing is linked that the author did not ask for. An earlier version scanned
each step for any ingredient name it contained; it cost nothing to use, but it
was invisible, it matched "oil" inside "olive oil", and there was no way to say
"not that one". A reference you cannot see is a reference you cannot correct.

### Cookbook — household-owned

`Id`, `HouseholdId`, `Name` (1–80), `Description?` (≤ 500), `Kind`
(`manual` | `smart`), `Rules`, `CreatedBy`, `CreatedAt`, `UpdatedAt`,
`Version`. A manual cookbook's membership is
`CookbookRecipe(CookbookId, RecipeId, AddedAt, AddedBy?)`, composite primary
key; a smart one has none.

A named shelf somebody chose the contents of — a playlist, not a genre. **This
is not a tag.** A tag is a *property* of a recipe and classifies it; a cookbook
is a curation, with a name in prose, a description, a cover and a page of its
own, and being on one says nothing about the recipe itself. A recipe is *tagged*
vegetarian; it is *in* Sunday roasts.

Household-owned, like the recipes it points at. A private curation of shared
recipes breaks the moment somebody leaves, and `CreatedBy` is kept so "Anna's
Sunday roasts" can say whose idea it was without becoming Anna's property.

**The cookbook is only the shelf.** It never holds its recipes in memory: a
shopping list is bounded by the week and a plan by seven days, but a cookbook
has no size, so membership is written through the repository the way meal-plan
entries are, and read through `GET /recipes?cookbookId=…`.

**Adding a recipe that is already on is a no-op**, enforced by the composite
key rather than by a check — a double tap and a retried request are both
ordinary. It keeps the moment it first went on, and the cookbook's `Version` is
**not** bumped: churning it to report that nothing happened would throw away
every cached copy.

Order inside a cookbook is `AddedAt` ascending, which reads like a table of
contents. Manual ordering is not implemented; when it is, it adds a
`SortOrder` column backfilled from `AddedAt`.

`CookbookRecipe.RecipeId` cascades, as `MealPlanEntry.RecipeId` does: a deleted
recipe drops off every shelf it was on. **The reverse is deliberately not true
— deleting a cookbook deletes no food.**

The cover is derived, not chosen: up to four photographed recipes, oldest
first, so a cover stops moving once there are four. A shelf with nothing
photographed shows its initial rather than a grey box.

#### A cookbook that fills itself

`Rules` is `Tags` (slugs, all required), `Ingredients` (names, all required,
matched as substrings) and `MaxMinutes?`. Every rule must hold: the shelves
worth having are the narrow ones. There is no "any of these" and no nesting — a
rule builder with brackets in it is a query language somebody has to learn.

**The rules are stored; what matches them is not.** A smart cookbook is a saved
question, answered whenever it is read — which is the whole of "a recipe written
this evening is on the right shelf the moment it is saved". There is no job to
run, nothing to backfill when a rule changes, and no membership table that can
drift out of step with the recipes it claims to describe. It empties itself the
same way: edit a recipe out of the rules and it is off, with no row to delete.

The clauses are the same ones `GET /recipes` already applies, so a shelf and the
filter bar cannot reach different conclusions about the same words. The one
difference: an ingredient **rule excludes**, where the ingredient *search* ranks
— on a shelf asking for chicken, a recipe without chicken is not a worse match,
it is not on the shelf.

`Kind` is chosen when the cookbook is made and never changes, and the two are
never mixed. They answer "why is this recipe here?" with different kinds of
answer — "somebody put it there" against "it matches" — and a shelf that was
both could answer neither, nor say what "take this off" was supposed to mean.
Putting a recipe on a smart cookbook by hand returns `cookbooks.rules_decide_membership`.

A smart cookbook has no order somebody chose, so it reads newest-first rather
than by a column that is null for every row on it.

**Cookbooks are not in the archive yet**, so an export currently omits them —
rules included.

### Tag

`Id`, `HouseholdId`, `Name` (1–40), `Slug` (lowercased, normalised).
Household-scoped so each household keeps its own vocabulary and nothing leaks
between them. `(HouseholdId, Slug)` is unique. `RecipeTag(RecipeId, TagId)`.

### CookPhoto

A picture of one attempt, hung on the `CookLogEntry` that dates it:
`ImageHash`, `Width`, `Height`, stored as three columns on `cook_log_entries`
that move together (a check constraint says so).

**Personal, like the note and the log itself.** Two people in one household keep
separate histories and separate photographs of them. The recipe's own
`RecipeImage` is the household's and says what the dish is supposed to look
like; this says what it looked like on a Tuesday, and the two are not the same
claim.

On the entry rather than in a table of its own, because unlike a recipe's hero
image there is nothing to replace: one attempt, one photo. The bytes are
content-addressed and live on the volume, so **a future sweep for unreferenced
files must read `cook_log_entries.image_hash` as well as
`recipe_images.content_hash`.** Removing a photo clears the columns and leaves
the file, which may be another attempt's picture too.

### RecipeImage

`Id`, `RecipeId`, `ContentHash` (SHA-256, content-addressed), `Width`,
`Height`, `ByteSize`, `ContentType`, `CreatedAt`.

Stored on a filesystem volume, not in the database; three derivative widths
(400 / 800 / 1600) generated on upload. Serving and validation rules are in the
image bead. One image per recipe in v1.

## Units

A closed enum in `Domain`, surfaced through OpenAPI so the frontend shares one
definition (`frontend-api-client`).

| Family | Members | Merge / convert |
| --- | --- | --- |
| Mass | `g`, `kg` | freely, canonical `g` |
| Volume | `ml`, `l` | freely, canonical `ml` |
| Spoon | `tsp`, `tbsp` | **never** converted to ml |
| Count | `piece`, `clove`, `bunch`, `slice`, `can`, `pack`, `pinch` | only with the identical unit |
| None | `null` | only with `null` |

Spoons are deliberately not converted: a US tablespoon is 14.8 ml and a metric
one is 15 ml, so "converting" silently invents precision the recipe never had.
Culina keeps spoons as spoons.

## Cooking

### CookSession — person-owned

`Id`, `RecipeId`, `UserId`, `HouseholdId`, `Servings` (decimal, the scaling in
force), `CurrentStepIndex`, `StartedAt`, `LastActiveAt`, `CompletedAt?`,
`AbandonedAt?`, `Version`.

At most **one active session per user** (`CompletedAt` and `AbandonedAt` both
null). Starting a new one abandons the previous. This is what makes
`GET /cook-sessions/current` a single unambiguous answer and powers the
"now cooking" bar.

**Timers are not here.** A timer is device-bound and must keep ticking offline,
so it lives in `localStorage` keyed by cook-session id, storing absolute end
timestamps. A cooking session is a fact worth persisting; a timer is local
ephemera. See `sveltekit-state-and-optimistic-ui`.

Two limits, both stated rather than worked around:

**A timer does not ring when the app is closed.** An absolute deadline is right
whenever the app is looked at again — a phone in a pocket for ten minutes shows
the correct remaining time on unlocking, and one that ran out shows that it did.
What it cannot do is make a sound while nothing is running. Promising an alarm
would need a notification the browser schedules, which needs permission Culina
does not ask for, and a timer that sometimes rings is worse than one that never
claims to.

**`CurrentStepIndex` is a position, not an identity.** A recipe edited from
another device while somebody is cooking it can move the step under them. The
index is clamped so it can never point past the end, but a step *inserted* above
the current one shifts the position silently. Making this exact means recording
the step's id in the session, which is a migration and a contract change for a
case that is rare and immediately visible to the person cooking. Recorded here
so the next person weighing it knows it was weighed.

### CookLogEntry — person-owned

`Id`, `RecipeId`, `UserId`, `HouseholdId`, `MadeAt`, `Servings?`, `Note?`.

Append-only. Powers "you've made this 7 times, last in March" and the
"most cooked" sort, which is why Culina needs no star ratings: what you actually
cook is a better signal than what you claim to like.

### PersonalNote — person-owned

`Id`, `RecipeId`, `UserId`, `StepId?` (null = recipe-level), `Text` (≤ 2000),
`UpdatedAt`, `Version`. Unique on `(RecipeId, UserId, StepId)`.

### SuggestionDismissal — person-owned

`UserId`, `RecipeId`, `DismissedAt`. Composite primary key `(UserId, RecipeId)`.

"Not this." The only signal the suggestion ranking cannot derive from something
another feature already writes, and it has to be asked for rather than inferred:
with two to eight people there is no such thing as a meaningful non-click, so
reading dislike into five suggestions ignored on one evening would be
manufacturing data.

Person-owned, like the note and the cook log. One person hiding a recipe from
their own suggestions says nothing about anybody else in the household, and the
recipe itself is untouched — still in the collection, still searchable, still on
its shelves.

**It expires.** The ranker ignores rows older than its window, so "not tonight"
does not quietly become "never again".

**Not an aggregate root**, so no `version` column: it is never
read-modify-written, has no concurrency to lose, and nothing about it could
sensibly carry an ETag. Dismissing twice is one dismissal, enforced by the
composite key rather than by a check — a double tap and a retried request are
both ordinary, exactly as on `CookbookRecipe`.

## Suggestions

**Nothing is stored.** There is no suggestions table, no precomputed ranking and
no taste profile on disk. A suggestion is a saved question answered whenever
somebody looks — the same decision `Cookbook.Kind = smart` made, and for the same
reasons: a recipe written this evening is ranked correctly this evening, there is
no job to run, nothing to backfill when a weight changes, and no stored ranking
that can drift out of step with the recipes it claims to describe.

The score is a sum of named terms over data eight other features already write:

| Term | Read from |
| --- | --- |
| Affinity | `CookLogEntry` (×1.3 with a photo), `MealPlanEntry`, `CookbookRecipe`, `PersonalNote`, completed `CookSession` |
| Content | `RecipeTag` and `RecipeIngredient.Name`, as a TF-IDF cosine against the same features of what this person cooks |
| Repetition | `max(CookLogEntry.MadeAt)` across **the whole household** |
| Rediscovery | the same date, once it is months old, scaled by affinity |
| Seasonality | the month distribution of this household's own cook log, per tag |
| Slot | `MealPlanEntry.Slot`, Laplace-smoothed |
| Effort | `PrepMinutes`, `CookMinutes`, `count(Step)` |
| Household | everybody else's `CookLogEntry` |
| Novelty | never cooked; `Recipe.CreatedAt` / `RecipeOrigin.ImportedAt` |

Three properties are load-bearing and easy to lose:

**Repetition is household-wide while affinity is personal.** If your partner made
the lasagne on Tuesday then you ate it, and it should not be suggested to you on
Wednesday even though your own cook log is silent. No general-purpose recommender
expresses this asymmetry; it is the most product-specific line in the subsystem.

**`CookSession.AbandonedAt` is not read.** Starting a session abandons the
previous one — the partial unique index guarantees it — so abandonment is
overwhelmingly a consequence of cooking something else, not a judgement. Reading
it as dislike would systematically punish the recipes people cook most often.

**Seasonality is observed, never curated.** A curated ingredient→season table was
rejected for this project for the reason `README.md` rejects a pantry: nobody
maintains it, so it goes stale and poisons what is built on it. This asks the
household's own log instead, per tag, and stays silent below an evidence
threshold — so a fresh installation says nothing about seasons rather than
something confident and wrong.

Scored **against the day rather than the instant**, so two requests on one day
produce one order: a cursor keeps meaning something on the second page, and the
list does not rearrange under somebody still reading it.

## Shopping

#### SuggestionDismissal — person-owned

`UserId`, `RecipeId`, `DismissedAt`. Composite primary key `(UserId, RecipeId)`.

"Not this." The only signal the suggestion ranking cannot derive from something
another feature already writes, and it has to be asked for rather than inferred:
with two to eight people there is no such thing as a meaningful non-click, so
reading dislike into five suggestions ignored on one evening would be
manufacturing data.

Person-owned, like the note and the cook log. One person hiding a recipe from
their own suggestions says nothing about anybody else in the household, and the
recipe itself is untouched — still in the collection, still searchable, still on
its shelves.

**It expires.** The ranker ignores rows older than its window, so "not tonight"
does not quietly become "never again".

**Not an aggregate root**, so no `version` column: it is never
read-modify-written, has no concurrency to lose, and nothing about it could
sensibly carry an ETag. Dismissing twice is one dismissal, enforced by the
composite key rather than by a check — a double tap and a retried request are
both ordinary, exactly as on `CookbookRecipe`.

## Suggestions

**Nothing is stored.** There is no suggestions table, no precomputed ranking and
no taste profile on disk. A suggestion is a saved question answered whenever
somebody looks — the same decision `Cookbook.Kind = smart` made, and for the same
reasons: a recipe written this evening is ranked correctly this evening, there is
no job to run, nothing to backfill when a weight changes, and no stored ranking
that can drift out of step with the recipes it claims to describe.

The score is a sum of named terms over data eight other features already write:

| Term | Read from |
| --- | --- |
| Affinity | `CookLogEntry` (×1.3 with a photo), `MealPlanEntry`, `CookbookRecipe`, `PersonalNote`, completed `CookSession` |
| Content | `RecipeTag` and `RecipeIngredient.Name`, as a TF-IDF cosine against the same features of what this person cooks |
| Repetition | `max(CookLogEntry.MadeAt)` across **the whole household** |
| Rediscovery | the same date, once it is months old, scaled by affinity |
| Seasonality | the month distribution of this household's own cook log, per tag |
| Slot | `MealPlanEntry.Slot`, Laplace-smoothed |
| Effort | `PrepMinutes`, `CookMinutes`, `count(Step)` |
| Household | everybody else's `CookLogEntry` |
| Novelty | never cooked; `Recipe.CreatedAt` / `RecipeOrigin.ImportedAt` |

Three properties are load-bearing and easy to lose:

**Repetition is household-wide while affinity is personal.** If your partner made
the lasagne on Tuesday then you ate it, and it should not be suggested to you on
Wednesday even though your own cook log is silent. No general-purpose recommender
expresses this asymmetry; it is the most product-specific line in the subsystem.

**`CookSession.AbandonedAt` is not read.** Starting a session abandons the
previous one — the partial unique index guarantees it — so abandonment is
overwhelmingly a consequence of cooking something else, not a judgement. Reading
it as dislike would systematically punish the recipes people cook most often.

**Seasonality is observed, never curated.** A curated ingredient→season table was
rejected for this project for the reason `README.md` rejects a pantry: nobody
maintains it, so it goes stale and poisons what is built on it. This asks the
household's own log instead, per tag, and stays silent below an evidence
threshold — so a fresh installation says nothing about seasons rather than
something confident and wrong.

Scored **against the day rather than the instant**, so two requests on one day
produce one order: a cursor keeps meaning something on the second page, and the
list does not rearrange under somebody still reading it.

## ShoppingList — household-owned

`Id`, `HouseholdId` (unique — exactly one list per household), `Version`.

Created lazily on first access. One list, not many: a second list is a planning
feature, and planning is v2.

#### SuggestionDismissal — person-owned

`UserId`, `RecipeId`, `DismissedAt`. Composite primary key `(UserId, RecipeId)`.

"Not this." The only signal the suggestion ranking cannot derive from something
another feature already writes, and it has to be asked for rather than inferred:
with two to eight people there is no such thing as a meaningful non-click, so
reading dislike into five suggestions ignored on one evening would be
manufacturing data.

Person-owned, like the note and the cook log. One person hiding a recipe from
their own suggestions says nothing about anybody else in the household, and the
recipe itself is untouched — still in the collection, still searchable, still on
its shelves.

**It expires.** The ranker ignores rows older than its window, so "not tonight"
does not quietly become "never again".

**Not an aggregate root**, so no `version` column: it is never
read-modify-written, has no concurrency to lose, and nothing about it could
sensibly carry an ETag. Dismissing twice is one dismissal, enforced by the
composite key rather than by a check — a double tap and a retried request are
both ordinary, exactly as on `CookbookRecipe`.

## Suggestions

**Nothing is stored.** There is no suggestions table, no precomputed ranking and
no taste profile on disk. A suggestion is a saved question answered whenever
somebody looks — the same decision `Cookbook.Kind = smart` made, and for the same
reasons: a recipe written this evening is ranked correctly this evening, there is
no job to run, nothing to backfill when a weight changes, and no stored ranking
that can drift out of step with the recipes it claims to describe.

The score is a sum of named terms over data eight other features already write:

| Term | Read from |
| --- | --- |
| Affinity | `CookLogEntry` (×1.3 with a photo), `MealPlanEntry`, `CookbookRecipe`, `PersonalNote`, completed `CookSession` |
| Content | `RecipeTag` and `RecipeIngredient.Name`, as a TF-IDF cosine against the same features of what this person cooks |
| Repetition | `max(CookLogEntry.MadeAt)` across **the whole household** |
| Rediscovery | the same date, once it is months old, scaled by affinity |
| Seasonality | the month distribution of this household's own cook log, per tag |
| Slot | `MealPlanEntry.Slot`, Laplace-smoothed |
| Effort | `PrepMinutes`, `CookMinutes`, `count(Step)` |
| Household | everybody else's `CookLogEntry` |
| Novelty | never cooked; `Recipe.CreatedAt` / `RecipeOrigin.ImportedAt` |

Three properties are load-bearing and easy to lose:

**Repetition is household-wide while affinity is personal.** If your partner made
the lasagne on Tuesday then you ate it, and it should not be suggested to you on
Wednesday even though your own cook log is silent. No general-purpose recommender
expresses this asymmetry; it is the most product-specific line in the subsystem.

**`CookSession.AbandonedAt` is not read.** Starting a session abandons the
previous one — the partial unique index guarantees it — so abandonment is
overwhelmingly a consequence of cooking something else, not a judgement. Reading
it as dislike would systematically punish the recipes people cook most often.

**Seasonality is observed, never curated.** A curated ingredient→season table was
rejected for this project for the reason `README.md` rejects a pantry: nobody
maintains it, so it goes stale and poisons what is built on it. This asks the
household's own log instead, per tag, and stays silent below an evidence
threshold — so a fresh installation says nothing about seasons rather than
something confident and wrong.

Scored **against the day rather than the instant**, so two requests on one day
produce one order: a cursor keeps meaning something on the second page, and the
list does not rearrange under somebody still reading it.

## ShoppingListItem

`Id`, `ListId`, `Name`, `Quantity?`, `Unit?`, `Section`, `IsChecked`,
`CheckedAt?`, `SortOrder`, `IsManual`.

Merge rule, applied when a recipe is added:

> Items merge when `Name` matches case- and diacritic-insensitively **and** the
> units are in the same convertible family. Quantities are summed in the
> family's canonical unit and stored **unrounded**. Anything else becomes a new
> line.

Merging unrounded matters: rounding first and summing second compounds error.
Rounding is presentation, and presentation belongs to the client
(`scaling-rules.md`).

#### SuggestionDismissal — person-owned

`UserId`, `RecipeId`, `DismissedAt`. Composite primary key `(UserId, RecipeId)`.

"Not this." The only signal the suggestion ranking cannot derive from something
another feature already writes, and it has to be asked for rather than inferred:
with two to eight people there is no such thing as a meaningful non-click, so
reading dislike into five suggestions ignored on one evening would be
manufacturing data.

Person-owned, like the note and the cook log. One person hiding a recipe from
their own suggestions says nothing about anybody else in the household, and the
recipe itself is untouched — still in the collection, still searchable, still on
its shelves.

**It expires.** The ranker ignores rows older than its window, so "not tonight"
does not quietly become "never again".

**Not an aggregate root**, so no `version` column: it is never
read-modify-written, has no concurrency to lose, and nothing about it could
sensibly carry an ETag. Dismissing twice is one dismissal, enforced by the
composite key rather than by a check — a double tap and a retried request are
both ordinary, exactly as on `CookbookRecipe`.

## Suggestions

**Nothing is stored.** There is no suggestions table, no precomputed ranking and
no taste profile on disk. A suggestion is a saved question answered whenever
somebody looks — the same decision `Cookbook.Kind = smart` made, and for the same
reasons: a recipe written this evening is ranked correctly this evening, there is
no job to run, nothing to backfill when a weight changes, and no stored ranking
that can drift out of step with the recipes it claims to describe.

The score is a sum of named terms over data eight other features already write:

| Term | Read from |
| --- | --- |
| Affinity | `CookLogEntry` (×1.3 with a photo), `MealPlanEntry`, `CookbookRecipe`, `PersonalNote`, completed `CookSession` |
| Content | `RecipeTag` and `RecipeIngredient.Name`, as a TF-IDF cosine against the same features of what this person cooks |
| Repetition | `max(CookLogEntry.MadeAt)` across **the whole household** |
| Rediscovery | the same date, once it is months old, scaled by affinity |
| Seasonality | the month distribution of this household's own cook log, per tag |
| Slot | `MealPlanEntry.Slot`, Laplace-smoothed |
| Effort | `PrepMinutes`, `CookMinutes`, `count(Step)` |
| Household | everybody else's `CookLogEntry` |
| Novelty | never cooked; `Recipe.CreatedAt` / `RecipeOrigin.ImportedAt` |

Three properties are load-bearing and easy to lose:

**Repetition is household-wide while affinity is personal.** If your partner made
the lasagne on Tuesday then you ate it, and it should not be suggested to you on
Wednesday even though your own cook log is silent. No general-purpose recommender
expresses this asymmetry; it is the most product-specific line in the subsystem.

**`CookSession.AbandonedAt` is not read.** Starting a session abandons the
previous one — the partial unique index guarantees it — so abandonment is
overwhelmingly a consequence of cooking something else, not a judgement. Reading
it as dislike would systematically punish the recipes people cook most often.

**Seasonality is observed, never curated.** A curated ingredient→season table was
rejected for this project for the reason `README.md` rejects a pantry: nobody
maintains it, so it goes stale and poisons what is built on it. This asks the
household's own log instead, per tag, and stays silent below an evidence
threshold — so a fresh installation says nothing about seasons rather than
something confident and wrong.

Scored **against the day rather than the instant**, so two requests on one day
produce one order: a cursor keeps meaning something on the second page, and the
list does not rearrange under somebody still reading it.

## ShoppingListItemSource

`ItemId`, `RecipeId`, `RecipeIngredientId`, `Quantity`, `Unit`, `AddedAt`.

Provenance for "which recipes asked for this", revealed on tap, never by
default. Removing a recipe's contribution subtracts exactly its sources.

### Sections

`produce`, `dairy_eggs`, `meat_fish`, `bakery`, `dry_goods`, `canned_jars`,
`frozen`, `spices_baking`, `drinks`, `household`, `other`.

Ordered the way a shop is walked. Assigned from a seeded DE+EN keyword table
(~150 entries), defaulting to `other`. A user's correction is stored per
household in `HouseholdIngredientSection(HouseholdId, NameNormalised, Section)`
and wins over the seed from then on — sensible default, trivially correctable,
no configuration screen.

## Planning

### MealPlanEntry

`Id`, `HouseholdId`, `Date`, `RecipeId`, `Servings?`, `Slot`, `SortOrder`.

Household-owned, like the recipe: a plan is what the people who eat together
have agreed on, and one only its author could see would be a diary.

**A date, not a timestamp.** "Thursday" has no time zone, and storing one would
move somebody's dinner when they travelled.

`Servings` is null for however many the recipe was written for. Most planned
meals are cooked as written, and asking every time is a question with an obvious
answer.

`Slot` is `breakfast`, `lunch` or `dinner`, defaulting to dinner. Three, and no
"snack" or "dessert": a slot only earns its place if it changes what you buy.
The week view shows no slot picker at all — it lives in the sheet that adds a
meal — so a household that only ever plans dinner never meets the concept.

**A week, not a calendar.** A week is the unit people plan in, because they shop
at the weekend for the week that follows; the API reads seven days from the
**Monday** of the week containing `from`, and always returns all seven whether
or not anything is planned in them. A month view is where recurrence,
drag-and-drop and a second reason for a shopping list come from, and Culina has
exactly one list per household on purpose.

**The plan writes the shopping list through the existing path**, one
`POST /households/{id}/shopping-list/recipes` per planned meal. A second code
path that merged a whole week at once would be a second place for merging to
behave differently, and merging is the entire value of the list.

## The archive

`GET /households/{id}/archive` writes a household's recipes out as plain,
readable JSON — photographs inline, base64 — and `POST` to the same address puts
one back. The right answer to "what if I stop using Culina", which a self-hosted
app owes its users.

**A step's ingredient references travel as positions, never as ids.** The
position is into the recipe's ingredients read in order, group by group, which
is the order they are written back in. Ids are assigned by whichever database
the archive lands in, so a format that carried them would restore into steps
pointing at nothing — and a step whose amounts have come loose is precisely the
failure this app exists to prevent.

**Notes are the asking person's, and nobody else's.** The recipes belong to the
household; notes and the cook log are personal, and an archive carrying every
member's would be one person handing out another's private writing.

**A restore adds; it never replaces.** One recipe per transaction, so an archive
restored because something went wrong is not refused wholesale over one recipe a
newer version wrote strangely — what could not be written is counted and
reported. The format's version is read first and an unknown one is refused
outright, because a half-restored recipe is worse than a failed restore: nobody
can tell which half is wrong.

**The export is streamed**, a recipe at a time. With photographs inline an
archive of a well-used household is tens of megabytes, and building it in memory
would make the export the largest allocation in the process.

## Persistence conventions

- PostgreSQL, accessed with Npgsql + Dapper. No EF Core, no ORM change
  tracking. SQL lives in `Infrastructure/Persistence/<Domain>/`, rows map to
  Domain via `<Entity>RowMappings.cs` (`dotnet-layer-mapping`).
- Every aggregate root carries `version bigint not null default 1`. Writes are
  `... where id = @id and version = @version` and return the updated row; zero
  rows affected means a `412` (`http-caching-etags`). **The version check is in
  the SQL, never in C#.**
- Ids are UUIDv7 generated by the application, so inserts stay index-friendly
  and no round trip is needed to learn an id.
- Timestamps are `timestamptz`, always UTC, always `DateTimeOffset` in C#.
- Money does not exist in Culina. Quantities are `numeric(10,3)` — never
  `float`, which cannot represent 0.1.
- Deletes are hard deletes with `on delete cascade` from the aggregate root.
  There is no soft delete: an undo affordance in the UI is a better answer than
  a `deleted_at` column every query must remember.
- Migrations are numbered `.sql` files embedded in `Infrastructure`, applied at
  startup under a Postgres advisory lock, tracked in `schema_migrations`.
  Forward-only; a mistake is a new migration.
