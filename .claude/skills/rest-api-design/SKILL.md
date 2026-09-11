---
name: rest-api-design
description: RESTful URL and HTTP conventions for culina-v2 — resources and identifiers only (/users/123, /settings/households/123, /users/me/settings), no verb or hyphenated pseudo-resources, plus method, status code, pagination, filtering and versioning rules. Use whenever choosing a route, adding an endpoint, naming a resource, or reviewing an API shape.
---

# REST API design

**Think in resources, not in operations.** A URL names a thing; the HTTP method
says what is being done to it. If a route reads like a function call, it is
wrong.

```
GET    /api/v1/users                    list
POST   /api/v1/users                    create
GET    /api/v1/users/{userId}           read one
PUT    /api/v1/users/{userId}           replace
PATCH  /api/v1/users/{userId}           partial update
DELETE /api/v1/users/{userId}           delete
```

## Naming rules

- **Plural nouns, lowercase, kebab-case if a noun needs two words**:
  `/recipe-collections`, never `/recipeCollections` or `/RecipeCollections`.
- **No verbs in paths.** Not `/users/123/update`, not `/getUser`,
  not `/users/search` (use `GET /users?query=…`).
- **Identifiers are path segments**: `/users/123e4567-…`. An id never travels
  as a query parameter for a single-resource read.
- **Nest to express ownership, at most one level**:
  `/households/{householdId}/members/{userId}`. Deeper nesting means the child
  is really its own resource — promote it and filter:
  `/members?householdId=…`.
- `me` is the alias for the authenticated principal: `/users/me`,
  `/users/me/settings`. It avoids leaking the caller's id into every URL and
  lets the client avoid a lookup.

## Group by domain, not by consumer

This is the rule most often got wrong. When several things have the same kind
of sub-resource, the **sub-resource is the domain** and the owner is the
segment underneath it — do not invent a flat hyphenated pseudo-resource per
owner.

```
✗ /user-settings            ✗ /household-settings        ✗ /instance-settings
✓ /users/me/settings        ✓ /settings/households/{householdId}
                            ✓ /settings/instance
```

Two acceptable shapes, chosen by which noun the client thinks in:

- The setting belongs to *one* owner the caller already has in hand →
  `/users/me/settings`, `/households/{householdId}/settings`.
- Settings are their own administrable domain (one screen listing them, one
  policy guarding them) → `/settings/households/{householdId}`,
  `/settings/registration`.

Pick one per domain and be consistent; never both for the same thing. The same
reasoning applies to anything that would otherwise become
`<owner>-<something>`: memberships, invitations, preferences, exports.

## Actions that are not CRUD

Some operations are genuinely not "update the resource" — publishing,
transferring ownership, resetting a password. Model them as a **sub-resource
that is created**, not as a verb:

```
POST /api/v1/recipes/{recipeId}/publications        publish
POST /api/v1/households/{householdId}/ownership-transfers
POST /api/v1/sessions                               log in   (DELETE to log out)
POST /api/v1/password-resets                        request a reset
```

If no noun can be found and the operation is a true command, `POST
/resource/{id}/actions/<verb>` is the last resort — and needs a comment
explaining why no noun exists.

## Methods and status codes

| Method | Body | Idempotent | Success |
| --- | --- | --- | --- |
| `GET` | no | yes | `200`, `304` on a matching `If-None-Match` |
| `POST` (create) | yes | no | `201` + `Location` + the created resource |
| `POST` (accepted work) | yes | no | `202` |
| `PUT` | yes, complete | yes | `200` with the new state (or `204`) |
| `PATCH` | yes, partial | no | `200` with the new state |
| `DELETE` | no | yes | `204`, and `204` again when already gone |

Failures follow `dotnet-result-pattern`: always an RFC 9457 problem document
with `code`, `detail`, and `requestId`. Never a `200` with
`{"success": false}`.

- `400` malformed or invalid input · `401` not authenticated · `403`
  authenticated but not allowed · `404` absent *or* invisible to the caller
  (never leak existence through a `403`) · `409` conflicts with current state ·
  `412` failed precondition (`If-Match`) · `422` only if a body is
  syntactically fine but semantically impossible · `429` rate limited.

## Collections

```
GET /api/v1/recipes?householdId=…&tag=vegan&sort=-createdAt&cursor=…&limit=50
```

- Always a wrapped object: `{ "items": [...], "nextCursor": "...", "total": 123 }`.
  Never a bare JSON array at the top level.
- **Cursor pagination** (`cursor` + `limit`, `nextCursor` in the response) for
  anything that grows; offset pagination only for small, stable lists.
- `limit` has a documented default and a hard maximum enforced server-side.
- Filtering is `?field=value`, sorting is `?sort=field` / `?sort=-field` for
  descending, multiple keys comma-separated.
- Query parameter names are camelCase, matching the JSON body style.
- An unknown or duplicated query parameter is rejected with `400`, not ignored
  — a silently ignored filter returns wrong data that looks right.

## Versioning

- The version is a **path segment**: `/api/v1/...`. No `Asp.Versioning`
  package, no header or query-string versioning.
- Adding an optional field, a new endpoint, or a new enum value the client may
  ignore is **not** breaking and happens in place.
- Removing or renaming a field, changing a type, tightening validation, or
  changing a status code **is** breaking and needs `/api/v2/...`, a new `V2`
  endpoint folder, and the v1 endpoint left working.
- Contracts are per-operation DTOs precisely so one operation can be versioned
  without dragging others along (`dotnet-endpoints`).

## Representation

- JSON only, camelCase property names, UTF-8.
- Timestamps are RFC 3339 UTC strings (`2026-09-11T12:00:00Z`), from
  `DateTimeOffset`. Durations are ISO 8601 or explicit `…Seconds` integers,
  never bare "number of somethings".
- Money is an integer minor unit plus a currency code, never a float.
- Enums are lowercase snake_case strings, never integers — an integer enum on
  the wire cannot be extended safely.
- `null` means "not set"; an absent property in a `PATCH` means "unchanged".
  Document which of the two applies wherever both are possible.

## Checklist

- [ ] The path names a resource, in the plural, with ids as path segments.
- [ ] No verb, no `<owner>-<thing>` pseudo-resource; the domain leads.
- [ ] Method and success status match the table; failures are problem documents.
- [ ] Collections wrapped, cursor-paginated, with a capped `limit`.
- [ ] Read endpoints return an `ETag`; unsafe ones honour `If-Match`
      (`http-caching-etags`).
- [ ] The change is additive, or it opened a `v2`.
