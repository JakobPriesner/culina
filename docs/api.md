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
| `query` | Free text over title, description and ingredient names. |
| `tag` | Slug. Repeatable; repeated values are ANDed. |
| `maxMinutes` | Total time ceiling. "I have 25 minutes." |
| `ingredient` | Repeatable. Ranks by how many match and how few extras are needed. |
| `sort` | `-updatedAt` (default), `title`, `totalMinutes`, `-cookCount`, `relevance` (implied when `query` or `ingredient` is present). |
| `cursor`, `limit` | Cursor paging. `limit` default 24, max 100. |

Unknown or duplicated parameters are rejected with `400` by
`QueryParameterGuardMiddleware`. A silently ignored filter returns wrong data
that looks right.

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
| `GET` | `/tags?householdId=…` | With usage counts, for the filter bar. |
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
