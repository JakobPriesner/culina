# Culina — HTTP API (v1)

The full v1 surface. Conventions come from `rest-api-design`,
`dotnet-endpoints`, `dotnet-result-pattern`, `http-caching-etags` and
`cookie-auth-and-security`; this document only fixes *what* exists, never
restates *how* an endpoint is written.

Base path `/api/v1`. JSON only, camelCase, UTF-8, RFC 3339 UTC timestamps.
Every failure is an RFC 9457 problem document with `code`, `detail` and
`requestId`.

## Authentication at a glance

Cookie only. `__Host-culina.session` (HttpOnly, Secure, SameSite=Lax, opaque),
plus a readable `culina.csrf` companion cookie whose value is echoed in
`X-Culina-CSRF` on every unsafe request. No bearer tokens, no CORS — the SPA is
served from the same origin.

`me` is the alias for the authenticated user.

## Identity

| Method | Path | Notes |
| --- | --- | --- |
| `POST` | `/users` | Register. `201`. Honours `registration` instance settings (open / invitation-only / max users). Rate limited per IP. |
| `GET` | `/users/me` | `200` + ETag. The client calls this on boot to resolve the session. |
| `PATCH` | `/users/me` | Display name. `If-Match` required. |
| `GET` | `/users/me/settings` | locale, theme, mode, measurement system. |
| `PUT` | `/users/me/settings` | |
| `POST` | `/sessions` | Log in. `201`. Returns the CSRF token and the user. Rate limited per IP **and** per account. Failure is always `auth.invalid_credentials` — never "no such user". |
| `DELETE` | `/sessions/current` | Log out. Deletes the server-side session, then expires the cookie. `204`. |
| `GET` | `/sessions` | Your active sessions — device, IP, last seen. Powers "sign out everywhere". |
| `DELETE` | `/sessions/{sessionId}` | Revoke one. `204`. |

## Households

| Method | Path | Notes |
| --- | --- | --- |
| `GET` | `/households` | The ones you belong to. |
| `POST` | `/households` | `201`. Creator becomes `owner`. |
| `GET` | `/households/{householdId}` | `200` + ETag. |
| `PATCH` | `/households/{householdId}` | Rename. Owner only. `If-Match`. |
| `DELETE` | `/households/{householdId}` | Owner only. Cascades. |
| `GET` | `/households/{householdId}/members` | |
| `DELETE` | `/households/{householdId}/members/{userId}` | Owner removes anyone; a member may remove themselves. `households.last_owner` if it would leave none. |
| `PATCH` | `/households/{householdId}/members/{userId}` | Role change. Owner only. |
| `GET` | `/households/{householdId}/invitations` | Open invitations. Owner only. Returns metadata, **never** the code — the code is shown once, at creation. |
| `POST` | `/households/{householdId}/invitations` | `201`. Response carries the one-time code. |
| `DELETE` | `/households/{householdId}/invitations/{invitationId}` | Revoke. |
| `POST` | `/invitations/{code}/redemptions` | Join. `201` with the household. Expired / unknown / used all return the identical `households.invitation_invalid`, so codes cannot be probed. Rate limited. |

`POST /invitations/{code}/redemptions` is the "non-CRUD action as a created
sub-resource" pattern, not a verb route: redeeming *creates a redemption*.

## Recipes

| Method | Path | Notes |
| --- | --- | --- |
| `GET` | `/recipes` | See query parameters below. Cursor-paginated, wrapped. |
| `POST` | `/recipes` | `201` + `Location`. Only `householdId` and `title` are required. |
| `GET` | `/recipes/{recipeId}` | `200` + ETag, `304` on `If-None-Match`. Full detail incl. step segments. |
| `PUT` | `/recipes/{recipeId}` | Full replace incl. ingredients and steps. `If-Match` required; missing → `428`, stale → `412`. |
| `DELETE` | `/recipes/{recipeId}` | `204`, and `204` again when already gone. |
| `PUT` | `/recipes/{recipeId}/image` | `multipart/form-data`. JPEG/PNG/WebP, ≤ 10 MB, ≤ 8000 px. Re-encoded server-side — the uploaded bytes are never served back. |
| `DELETE` | `/recipes/{recipeId}/image` | |
| `GET` | `/recipes/{recipeId}/notes` | Your personal notes for this recipe. |
| `PUT` | `/recipes/{recipeId}/notes` | Upsert. Recipe-level and per-step in one document. |
| `GET` | `/recipes/{recipeId}/cook-log` | Your "made it" entries, newest first. |
| `POST` | `/recipes/{recipeId}/cook-log` | `201`. Body may be empty — one tap is the whole interaction. |

### `GET /recipes` query parameters

| Parameter | Meaning |
| --- | --- |
| `householdId` | **required** — scopes the query. |
| `query` | Free text over the title, tags, ingredient names, description and step text. Tolerant of typos (`Bolgnese`), of German spelling variants (`Bolognäse`, `Muesli`, `Musli`), of inflection in both directions (`Tomate` ↔ `Tomaten`) and of compounds (`Hähnchen` finds `Hähnchenbrustfilet`). |
| `tag` | Slug. Repeatable; repeated values are ANDed. |
| `maxMinutes` | Total time ceiling. "I have 25 minutes." |
| `ingredient` | Repeatable. Ranks by how many match and how few extras are needed. |
| `sort` | `-updatedAt`, `title`, `totalMinutes`, `-cookCount`, `relevance`, `suggested`, `cookbookOrder` (only with `cookbookId`). An explicit value always wins; with none, `query` or `ingredient` means `relevance`, a `cookbookId` alone means `cookbookOrder`, and everything else means `-updatedAt`. |
| `cookbookId` | Only what is on that cookbook — its rows if somebody fills it, its rules if it fills itself. Every other filter still applies, ANDed. |
| `cursor`, `limit` | Cursor paging. `limit` default 24, max 100. |

Unknown or duplicated parameters are rejected with `400` by
`QueryParameterGuardMiddleware`. A silently ignored filter returns wrong data
that looks right.

**How `relevance` orders.** A tier first, then a score inside it. The tier is
what kind of evidence put a recipe on the page — an exact title, a word of the
title, a compound or near-miss of one, a tag, an ingredient, the method — and it
is compared before anything else, so a recipe that merely resembles the query
can never climb past one the query names. The score inside a tier weighs how
much of the title the query accounts for, how much of the query the recipe
accounts for, the full-text rank, and how well the recipe fits any `ingredient`
values. Neither number is exposed: they are how a page is ordered and how its
cursor resumes, not a fact about a recipe.

With `ingredient`, each item additionally carries:

```json
{ "ingredientMatch": { "matched": 3, "requested": 3, "missing": 2 } }
```

which the UI renders as *"uses 3 of 3 · 2 more needed"*. That is the whole
"what can I cook?" feature — no pantry to maintain, so nothing to go stale.

### Step segments

The API never exposes the raw `[[ingredient:…]]` token form. A step is returned
pre-split, so the client renders without parsing:

```json
{
  "id": "…", "sortOrder": 3, "durationSeconds": 1200,
  "segments": [
    { "type": "text", "value": "Melt " },
    { "type": "ingredient", "recipeIngredientId": "…", "name": "butter",
      "quantity": 120, "unit": "g" },
    { "type": "text", "value": " in the pan." }
  ]
}
```

Quantities are the **base** amounts. The client applies the factor and the
rounding (`scaling-rules.md`).

On write, `PUT /recipes/{id}` accepts the same segment shape and the server
re-serialises it to tokens — so the token format stays a persistence detail.

### `sort=suggested`

Ranks the whole collection for whoever is asking: how much they cook this recipe
and things like it, how recently the **household** ate it, how long it has been
since they last did, what the plan says about the meal it suits, how much of a
project it is on a weekday, what everyone else cooks, and how new it is. Every
other filter still applies, so "what should I cook?" and "I have twenty-five
minutes and some chicken" are one feature rather than two that can disagree.

**Scored against the day, not the instant.** Two requests on one day produce the
same order, which is what makes the cursor mean something on the second page —
and what stops the list rearranging under somebody who is still reading it.

A recipe this person has dismissed is not in this order. It is still in every
other one, still searchable and still on its shelves: hiding a recipe from your
own suggestions is not the same sentence as deleting it.

## Suggestions

| Method | Path | Notes |
| --- | --- | --- |
| `GET` | `/suggestions?householdId=…` | A bounded set with a reason for each, best first. Not paged. |
| `PUT` | `/recipes/{recipeId}/suggestion-dismissal` | Stop suggesting this to me. `204`, idempotent. |
| `DELETE` | `/recipes/{recipeId}/suggestion-dismissal` | Undo that. `204`, and `204` when it was never hidden. |

### `GET /suggestions` query parameters

| Parameter | Meaning |
| --- | --- |
| `householdId` | **required** — scopes the question. |
| `purpose` | `decide` (default) or `like`. Ranking the whole collection is a sort on the collection, so `browse` is deliberately not nameable here. |
| `slot` | `breakfast`, `lunch` or `dinner`. |
| `maxMinutes` | A ceiling on total time. A filter, never a preference. |
| `tag`, `ingredient` | Repeatable. The caller's constraints, honoured exactly. |
| `likeRecipeId` | Recipes resembling this one. Implies `purpose=like`. |
| `exclude` | Repeatable. What the caller already has on screen or already planned. |
| `limit` | 1–12, default 5. Out of range is rejected rather than clamped — asking for fifty is a client that thinks this is the recipe list. |

**Deliberately not paged, and not wrapped in a cursor envelope.** The paging
envelope exists for collections that page; a thing with no next page should not
claim one. Past a dozen the honest answer is the recipe list, ranked.

**Context is supplied here rather than stored on a recipe.** Whether something
is breakfast is a fact about the occasion and about how this household plans,
not a property of the food — so a recipe has no `mealType` column and should not
get one.

**`likeRecipeId` compares ingredients and tags, not who else cooked what.** With
two to eight people, the co-occurrence between any two recipes is zero or a
coincidence; "uses eleven of the same twelve ingredients" is neither, and it can
be explained.

### Why a recipe was suggested

```json
{ "recipeId": "…", "title": "Linsensuppe", "cookCount": 9,
  "lastCookedAt": "2026-04-12T18:30:00Z",
  "reason": { "code": "ingredient", "subject": "Aubergine" } }
```

`code` is one of `affinity`, `rediscovery`, `tag`, `ingredient`, `season`,
`slot`, `household`, `fresh`, `similar`. `subject` is the tag, ingredient name
or member's display name it is about, when it is about something nameable.

**`reason` is `null` whenever no single signal decided the ranking**, and that
is an ordinary answer meaning *show nothing*. The reason is whichever term
actually dominated the score, never a sentence composed to suit a recipe that
was picked for other reasons — an invented explanation, caught once, discredits
every one that was true.

A code and at most a subject, never prose: the wording belongs to the client,
because that is what knows which of two languages the reader reads.

## Cookbooks

A cookbook is a household's named shelf of recipes. **It is read as a view of
the collection, not as a collection of its own**: the recipes on one come back
from `GET /recipes?cookbookId=…`, which is what gives a cookbook the same
search, tag filter, time ceiling, ingredient ranking and cursor paging the
whole library has, with no second implementation of any of them.

| Method | Path | Notes |
| --- | --- | --- |
| `GET` | `/cookbooks?householdId=…` | Wrapped, cursor-paginated, most recently changed first. Each carries `recipeCount` and up to four `coverRecipeIds` for the cover mosaic. |
| `POST` | `/cookbooks` | `201` + `Location`. Only `householdId` and `name` are required. |
| `GET` | `/cookbooks/{cookbookId}` | `200` + ETag, `304` on `If-None-Match`. The shelf's own metadata — **not** the recipes on it. |
| `PATCH` | `/cookbooks/{cookbookId}` | Name and description together. `If-Match` required. |
| `DELETE` | `/cookbooks/{cookbookId}` | `204`, and `204` again when already gone. **Every recipe that was on it survives.** |
| `PUT` | `/cookbooks/{cookbookId}/recipes/{recipeId}` | Put a recipe on. `204`. `409` on a cookbook that fills itself. |
| `DELETE` | `/cookbooks/{cookbookId}/recipes/{recipeId}` | Take it off. `204`, and `204` when it was never on. `409` on a cookbook that fills itself. |
| `GET` | `/recipes/{recipeId}/cookbooks` | Which cookbooks contain it. Not paged — a recipe is on a handful of shelves or none. |

`PUT` on the membership rather than `POST` to a collection, because being on a
shelf is a fact at a known address and not a new thing each time. It is
therefore **idempotent**: a recipe already on keeps the moment it went on, and
the cookbook's version is not bumped, so a double tap or a retried request
neither duplicates nor invalidates a good cached copy.

**An unknown or foreign `cookbookId` on `GET /recipes` is an empty page, not a
`404`.** The 404-never-403 rule governs resources named in the *path*; this is
a filter value, and an unknown `tag` slug already behaves the same way. The
cookbook's own page reads `GET /cookbooks/{id}` for its header, and that does
answer `404`.

### Cookbooks that fill themselves

Send `rules` on `POST /cookbooks` — `{ tags, ingredients, maxMinutes }`, at
least one of them — and the cookbook holds whatever matches, worked out whenever
it is read. Omit `rules` for one you fill yourself. A cookbook is one or the
other, chosen at creation and never changed; `PATCH` may edit a smart
cookbook's rules but may not give a manual one any, and hand-adding to a smart
one is `409 cookbooks.rules_decide_membership`.

**Nothing records what matches.** That is what makes "a recipe written this
evening is on the right shelf already" true rather than eventually true: there
is no sync to run, no backfill when a rule changes, and no stored membership
that can disagree with the recipes. It is also why a smart cookbook's
`recipeCount` and cover come back from evaluating the rules, and why
`sort=cookbookOrder` falls back on one — it was never put in an order.

Every rule must hold, and each is the clause `GET /recipes` already applies, so
a shelf and the filter bar cannot disagree about the same words. The exception
worth knowing: an ingredient **rule excludes**, where the `ingredient` search
parameter ranks.

**Adding a whole cookbook to the shopping list is not an endpoint.** The client
sends one `POST /households/{id}/shopping-list/recipes` per recipe, exactly as
the meal plan does, because a second path that merged many at once would be a
second place for merging to behave differently — and merging is the entire
value of the list.

## Cooking

| Method | Path | Notes |
| --- | --- | --- |
| `POST` | `/cook-sessions` | Start. `201`. Abandons any other active session for this user — there is exactly one. |
| `GET` | `/cook-sessions/current` | `200` or `404`. Drives the "now cooking" bar. |
| `GET` | `/cook-sessions/{cookSessionId}` | `200` + ETag. |
| `PATCH` | `/cook-sessions/{cookSessionId}` | `currentStepIndex`, `servings`, or `completedAt`. High frequency — the client debounces and sends optimistically. |
| `DELETE` | `/cook-sessions/{cookSessionId}` | Abandon. `204`. |

Timers are **not** in the API. They are device-local (`domain-model.md`).

## Shopping

| Method | Path | Notes |
| --- | --- | --- |
| `GET` | `/shopping-lists?householdId=…` | The household's single list. Created lazily. |
| `GET` | `/shopping-lists/{listId}` | `200` + ETag. Items grouped by section, checked items last. |
| `POST` | `/shopping-lists/{listId}/items` | Add a manual item. `201`. Merges into an existing line when the rule in `domain-model.md` matches. |
| `PATCH` | `/shopping-lists/{listId}/items/{itemId}` | Check / uncheck, rename, change amount or section. A section change is remembered for the household. |
| `DELETE` | `/shopping-lists/{listId}/items/{itemId}` | `204`. |
| `POST` | `/shopping-lists/{listId}/recipe-additions` | `{ recipeId, servings }` → merged items, `201`. Response returns the whole list so the client needs no refetch. |
| `DELETE` | `/shopping-lists/{listId}/recipe-additions/{recipeId}` | Subtract exactly that recipe's contributions. |
| `DELETE` | `/shopping-lists/{listId}/items?checked=true` | Clear checked items. `204`. |

`recipe-additions` is a created sub-resource rather than a polymorphic `items`
body: it generates a clean client and stays greppable.

## Tags and settings

| Method | Path | Notes |
| --- | --- | --- |
| `GET` | `/tags?householdId=…` | With usage counts, most used first. Not paged. Feeds the filter bar and the smart-cookbook rule editor. |
| `GET` | `/settings/registration` | Instance settings. Admin only. |
| `PUT` | `/settings/registration` | `openRegistration`, `requireInvitation`, `maxUsers`. |

## Health

`GET /health/live` — the process is up. `GET /health/ready` — the database
answers. Neither is under `/api`, neither requires auth, both are excluded from
tracing (`dotnet-observability`).

## Cross-cutting guarantees

- **Every read of a single entity** returns a strong `ETag` derived from its
  `version`, and honours `If-None-Match` with `304`.
- **Every unsafe single-entity write** requires `If-Match`: missing → `428`,
  stale → `412`. The check is in the SQL `WHERE`.
- **A `412` is never retried.** Retrying it would silently overwrite whoever
  wrote first. The client rolls its own copy back, keeps what was typed on the
  device that typed it, and puts the choice to the person: keep mine — which
  re-reads to learn the current version and writes over it deliberately — or
  take theirs, which drops the local copy. Merging two people's recipes
  automatically is a guess, and a guess about somebody's dinner is worse than a
  question.
- **Every collection** is wrapped (`items`, `nextCursor`, `total`) and
  cursor-paginated.
- **Authenticated responses** carry `Cache-Control: no-store`.
- **`404`, never `403`**, when a resource exists but is not visible to the
  caller. Existence is not leaked through a status code.
- **No `GET` mutates state**, which is what makes the CSRF exemption for safe
  methods sound.

## The generated client

The OpenAPI document is the contract. `pnpm generate:api` writes
`src/lib/api/generated/schema.d.ts` from `src/backend/openapi/Api.json`; it is
committed, never hand-edited, and ignored by the formatter and the linter.

`src/lib/api/` is the only place in the frontend that may call `fetch` or import
the generated schema — an ESLint rule fails the build otherwise, because the
moment a component builds its own request it also has to remember credentials,
CSRF, conditional requests and the shape of a failure.

Everything cross-cutting lives there once:

- `credentials: 'include'` and no base URL, because the generated paths already
  carry `/api/v1` and the app is served from the same origin as its API. There is
  no `Authorization` header anywhere.
- `X-Culina-CSRF` on every unsafe request, read from the `culina.csrf` cookie at
  send time so a sign-in in another tab is picked up without a reload.
- `If-None-Match` on reads and `If-Match` on writes, both from an in-memory ETag
  cache. A 304 is replayed from memory and never reaches the caller as an empty
  success. A successful write forgets the URL it wrote and anything on its path.
  Memory only: a household's recipes must not outlive the session on disk.
- Every response becomes a `Result<T>` — `{ ok: true, value }` or
  `{ ok: false, error }`. Nothing throws for a failure the server described.
  `AppError` carries `code`, `detail`, `status`, `requestId` and one entry per
  wrong field. **Branch on `code`; `detail` is prose and will be reworded.**
- A 401 clears the cache and notifies the shell, exactly once, never retried.
- A 403 with `auth.csrf_invalid` is retried exactly once, then given up on.
- A 15-second deadline per request, combined with the caller's own signal so a
  request still dies with the component that started it.

## ETags must cover what the response contains

Culina's ETags are derived from each entity's monotonic version, which is cheap,
stable, and already what optimistic concurrency needs. Two rules keep that safe,
both learned the hard way:

1. **A version alone identifies a resource only when the URL does.**
   `/users/me` names a different person for every session, and two people are
   both at version 1 on the day they sign up. A browser holding one person's
   response revalidates it, is told `304`, and shows their name and households to
   the next person to sign in on that device. Any route whose meaning depends on
   who is asking passes the entity's identity into the tag.

2. **A tag must change whenever any part of the body does.** `/users/me` lists
   the households the account belongs to, and joining one does not change the
   account — so a tag made only of the account's version answers `304` to a
   client that is missing a kitchen it can now cook in. The tag folds the
   membership set in alongside the version.

Both are asserted in `CurrentUserEndpointTests`, and both tests were verified to
fail without the fix.

## Rate limits during end-to-end tests

The limits (`RateLimits__*`) are configuration, so a test run raises them rather
than the defaults being weakened:

```bash
RateLimits__RegisterPerIpPerHour=200 RateLimits__LoginPerIpPerMinute=200 dotnet run
```

## Cooking sessions

`POST /cook-sessions`, `GET /cook-sessions/current`, `PATCH /cook-sessions/{id}`,
`DELETE /cook-sessions/{id}`.

At most **one active session per person**, enforced by a partial unique index
rather than by application code — two devices starting a session at the same
moment is exactly the case application code gets wrong. Starting abandons
whatever else was going, in one transaction, because the abandonment is what
makes the insert legal.

`PATCH` is called on every step advance, so it is deliberately cheap: a step
touches two columns and **does not bump the version**, since the last tap
genuinely is the truth about where the cook is, and making each advance a
concurrency event would leave a second device permanently stale for no benefit.
Rescaling does bump it.

`GET /cook-sessions/current` carries the recipe's title, so the bar that leads
back to what is on the hob costs one request rather than two on every page.

**Timers are not in the API.** A timer must keep counting while the app is
closed and the phone is in a pocket, so it is a wall-clock deadline stored on
the device, keyed by session id. A cooking session is a fact worth persisting; a
timer is local ephemera.
