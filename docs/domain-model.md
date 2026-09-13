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

### Tag

`Id`, `HouseholdId`, `Name` (1–40), `Slug` (lowercased, normalised).
Household-scoped so each household keeps its own vocabulary and nothing leaks
between them. `(HouseholdId, Slug)` is unique. `RecipeTag(RecipeId, TagId)`.

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

## Shopping

### ShoppingList — household-owned

`Id`, `HouseholdId` (unique — exactly one list per household), `Version`.

Created lazily on first access. One list, not many: a second list is a planning
feature, and planning is v2.

### ShoppingListItem

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

### ShoppingListItemSource

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
