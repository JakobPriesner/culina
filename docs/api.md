# API Reference

Culina exposes a versioned REST API under `/api/v1` and a Streamable HTTP Model Context Protocol (MCP) server under `/mcp`.

---

## 1. Conventions, Auth & Security

### Authentication & CSRF

1. **Browser Sessions:** `POST /api/v1/auth/login`, `/register`, or `/passkey/login-complete` returns a short-lived **JWT access token** (`15 min`, sent via `Authorization: Bearer <jwt>`) and sets an `HttpOnly`, `SameSite=Lax` refresh token cookie (`culina_rt`, `30 days`, path `/api/v1/auth`).
2. **Refresh Rotation:** `POST /api/v1/auth/refresh` rotates `culina_rt` and issues a new JWT. Reusing a revoked refresh token triggers family revocation.
3. **Personal Access Tokens (PATs):** Long-lived tokens prefixed `culina_pat_...` (SHA-256 hashed in `PersonalAccessTokens`) accepted in `Authorization: Bearer culina_pat_...` for CLI, scripts, and MCP clients.
4. **CSRF Protection (`OriginValidationMiddleware`):** State-changing requests (`POST`, `PUT`, `PATCH`, `DELETE`) carrying cookies must have an `Origin` or `Referer` matching `CULINA_PUBLIC_ORIGIN`.

### Concurrency & Idempotency

- **Optimistic Concurrency (`ETag`):** `GET /api/v1/recipes/{id}` returns `ETag: "<version>"`. `PUT /api/v1/recipes/{id}` accepts `If-Match: "<version>"` and returns `412 Precondition Failed` (`concurrency_conflict`) on version mismatch.
- **Idempotency (`IdempotencyMiddleware`):** `POST` endpoints accept `Idempotency-Key: <uuid>` (cached for `24h` per user + path + body hash).

### Error Format (RFC 9457 Problem Details)

All errors return `application/problem+json` with stable machine-readable `problemCode` and `traceId`:

```json
{
    "type": "https://culina.app/problems/validation_failed",
    "title": "Validierungsfehler",
    "status": 400,
    "detail": "Der Titel darf nicht leer sein.",
    "problemCode": "validation_failed",
    "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
    "errors": { "Title": ["Der Titel darf nicht leer sein."] }
}
```

| HTTP Status   | `problemCode`                                    | Meaning                                            |
| ------------- | ------------------------------------------------ | -------------------------------------------------- |
| `400`         | `validation_failed` / `bad_request`              | Field validation error or malformed input          |
| `401`         | `unauthorized` / `token_expired`                 | Missing, invalid, or expired credentials           |
| `403`         | `forbidden`                                      | Insufficient household or cookbook role            |
| `404`         | `not_found`                                      | Entity does not exist or is invisible to caller    |
| `409`         | `conflict` / `duplicate_resource`                | Unique constraint or state conflict                |
| `412`         | `concurrency_conflict`                           | `If-Match` ETag mismatch on recipe update          |
| `422`         | `intake_extraction_failed` / `ai_not_configured` | Scraper or AI provider could not extract recipe    |
| `429`         | `rate_limit_exceeded`                            | Rate limit reached (`Retry-After` header included) |
| `502` / `504` | `upstream_error` / `upstream_timeout`            | External AI provider or scraped URL failed         |

### Rate Limit Policies

| Policy         | Default Limit | Partition Key                         | Endpoints                                              |
| -------------- | ------------- | ------------------------------------- | ------------------------------------------------------ |
| `global`       | `300 / min`   | Authenticated `UserId` (or client IP) | All `/api/v1/*` endpoints                              |
| `auth`         | `10 / min`    | Client IP                             | `/api/v1/auth/*`, `/oauth/*`                           |
| `ai`           | `20 / min`    | Authenticated `UserId`                | `/api/v1/intake/*`, `/assistant/*`, AI image/nutrition |
| `public-share` | `60 / min`    | Client IP                             | `/api/v1/shared/*`, `/api/v1/library-sync/feed/*`      |

---

## 2. Endpoint Summary (`/api/v1`)

### Auth, Passkeys & Household (`/auth`, `/household`, `/pats`)

- `POST /auth/register`, `POST /auth/login`, `POST /auth/refresh`, `POST /auth/logout`
- `GET /auth/me`, `PUT /auth/me`, `POST /auth/change-password`, `DELETE /auth/account`
- `POST /auth/passkey/register-options`, `POST /auth/passkey/register-complete`, `POST /auth/passkey/login-options`, `POST /auth/passkey/login-complete`, `GET /auth/passkeys`, `DELETE /auth/passkeys/{id}`
- `GET /pats`, `POST /pats`, `DELETE /pats/{id}`
- `GET /household`, `PUT /household`, `POST /household/invites`, `POST /household/join/{token}`, `DELETE /household/members/{userId}`

### Recipes, Search & Suggestions (`/recipes`, `/categories`, `/tags`, `/ingredients`, `/suggestions`)

- `GET /recipes` — Paginated hybrid search (`q`, `categoryId`, `tags`, `ingredient`, `excludeIngredient`, `maxTime`, `difficulty`, `diet`, `favoritesOnly`, `unratedOnly`, `cookbookId`, `sort`: `relevance | newest | rating | quick | title | random`).
- `GET /recipes/completions` — Instant search overlay completions (`q` — titles, ingredients, tags, categories).
- `GET /recipes/lookalikes` — Duplicate detection during recipe creation (`title`, `sourceUrl`).
- `GET /recipes/{id}`, `POST /recipes`, `PUT /recipes/{id}` (supports `If-Match`), `DELETE /recipes/{id}`
- `GET /recipes/{id}/related` — Content-based similar recipes + explicit `RecipeRelations`.
- `POST /recipes/{id}/image` (multipart upload → converted to WebP), `POST /recipes/{id}/generate-image` (AI image generation).
- `PUT /recipes/{id}/rating`, `POST /recipes/{id}/favorite`, `DELETE /recipes/{id}/favorite`
- `POST /recipes/{id}/cooked` — Logs cook history, marks meal plan entry cooked, optionally deducts pantry & logs nutrition diary.
- `GET /recipes/{id}/cook-session`, `PUT /recipes/{id}/cook-session`, `DELETE /recipes/{id}/cook-session`
- `GET /suggestions` — Contextual meal suggestions (`mealType`, `date`, `purpose`, `limit`; see [`docs/suggestions-research.md`](suggestions-research.md)).
- `POST /suggestions/dismiss`, `POST /suggestions/impressions`
- `GET /categories`, `GET /tags`, `POST /tags/suggest`, `GET /ingredients/suggestions`

### Cookbooks & Sharing (`/cookbooks`, `/shared`, `/library-sync`)

- `GET /cookbooks`, `POST /cookbooks`, `GET /cookbooks/{id}`, `PUT /cookbooks/{id}`, `DELETE /cookbooks/{id}`
- `POST /cookbooks/{id}/recipes`, `DELETE /cookbooks/{id}/recipes/{recipeId}`, `PUT /cookbooks/{id}/recipes/reorder`
- `POST /cookbooks/{id}/members`, `PUT /cookbooks/{id}/members/{userId}`, `DELETE /cookbooks/{id}/members/{userId}`
- `POST /cookbooks/{id}/share-link`, `DELETE /cookbooks/{id}/share-link`
- `GET /shared/cookbook/{token}`, `GET /shared/recipe/{token}`
- `GET /library-sync/connections`, `POST /library-sync/connections`, `POST /library-sync/connections/{id}/sync`, `POST /library-sync/synced-recipes/{id}/fork`, `GET /library-sync/feed/{token}`

### Meal Plan, Shopping & Pantry (`/meal-plan`, `/shopping`, `/pantry`)

- `GET /meal-plan?weekStart=YYYY-MM-DD`, `POST /meal-plan/entries`, `PUT /meal-plan/entries/{id}`, `DELETE /meal-plan/entries/{id}`
- `POST /meal-plan/copy-week`, `GET /meal-plan/templates`, `POST /meal-plan/templates`, `POST /meal-plan/templates/{id}/apply`
- `GET /meal-plan/availability?weekStart=YYYY-MM-DD` — Compares planned ingredients against `PantryItems` and `ShoppingListItems`.
- `GET /shopping`, `POST /shopping/items`, `PUT /shopping/items/{id}`, `DELETE /shopping/items/{id}`, `POST /shopping/clear-checked`, `POST /shopping/from-meal-plan`, `POST /shopping/from-recipe/{recipeId}`
- `GET /shopping/stream` — Server-Sent Events (`text/event-stream`) for real-time household list sync.
- `GET /pantry`, `POST /pantry`, `PUT /pantry/{id}`, `DELETE /pantry/{id}`, `GET /pantry/staples`, `PUT /pantry/staples`

### Nutrition Diary & Barcode Lookup (`/diary`, `/nutrition`, `/foods`)

- `GET /diary?date=YYYY-MM-DD`, `GET /diary/range?from=YYYY-MM-DD&to=YYYY-MM-DD`
- `POST /diary/entries`, `PUT /diary/entries/{id}`, `DELETE /diary/entries/{id}`, `POST /diary/copy-day`
- `GET /diary/goals`, `PUT /diary/goals`, `POST /diary/weight`, `PUT /diary/water`
- `GET /foods/search?q=...` (BLS 4.0 + custom foods + Open Food Facts), `GET /foods/barcode/{ean}` (local cache → OFF v2 API), `POST /foods/custom`, `POST /foods/off-contribute`
- `POST /nutrition/estimate-recipe/{id}`, `POST /nutrition/estimate-text`, `POST /nutrition/estimate-photo`

### Intake, Olli Assistant, Settings & Archive (`/intake`, `/assistant`, `/settings`, `/archive`)

- `POST /intake/url`, `POST /intake/text`, `POST /intake/files`, `POST /intake/import-third-party`
- `POST /assistant/chat/stream` — Streaming SSE chat with Olli (`pageContext`, tool calls, recipe drafts, timer controls).
- `GET /settings/ai`, `PUT /settings/ai`, `POST /settings/ai/test`
- `GET /archive/export`, `POST /archive/import`

---

## 3. AI Agents & MCP Server (`#ai-agents-mcp`)

Culina hosts a remote **Model Context Protocol (MCP)** server at `/mcp` (`Streamable HTTP` transport, protocol `2025-03-26`).

### Discovery & OAuth 2.1 + PKCE Flow

1. `GET /.well-known/oauth-protected-resource` → points MCP clients to the authorization server metadata.
2. `GET /.well-known/oauth-authorization-server` → advertises:
    - `registration_endpoint`: `POST /api/v1/oauth/register` (RFC 7591 Dynamic Client Registration)
    - `authorization_endpoint`: `GET /oauth/authorize` (interactive consent screen in the SvelteKit app)
    - `token_endpoint`: `POST /api/v1/oauth/token` (`authorization_code` + `S256` PKCE, `refresh_token`)
3. Alternatively, pass a Personal Access Token directly: `Authorization: Bearer culina_pat_<token>`.

### Exposed MCP Tools (`CulinaMcpTools`)

- **Recipes:** `search_recipes`, `get_recipe`, `create_recipe`, `update_recipe`, `scale_recipe`, `import_recipe_from_url`
- **Planning & Shopping:** `get_meal_plan`, `plan_meal`, `remove_meal_plan_entry`, `get_shopping_list`, `add_shopping_items`, `generate_shopping_list_from_meal_plan`
- **Pantry & Suggestions:** `get_pantry`, `upsert_pantry_item`, `get_meal_suggestions`
- **Diary:** `get_nutrition_diary`, `log_food_entry`

---

## 4. Frontend Client Generation

The frontend uses `openapi-typescript` + `openapi-fetch`. Never hand-edit `src/frontend/src/lib/api/schema.d.ts`. After changing any backend DTO or endpoint signature, regenerate:

```bash
dotnet build src/backend/Culina.Api
pnpm --dir src/frontend generate:api
```
