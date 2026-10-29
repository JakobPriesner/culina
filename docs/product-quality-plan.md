# Product Quality Plan & Release Gates

Quality standards, automated test gates, and polish criteria across Culina's backend, frontend, and container delivery.

---

## 1. Automated Verification Gates

Every change must pass four automated verification layers before release:

1. **Domain & Integration Suite (`src/backend/Culina.Tests`):**
    - Pure unit tests for `IngredientAggregator`, `QuantityFormatter`, `IngredientNameNormalizer`, `RecipeNutritionEstimator`, `NutritionCalculator`, `QueryGrammar`, and `RankingRules`.
    - Real PostgreSQL 17 integration tests via Testcontainers covering migrations, FTS/trigram search (`SearchGoldenTests`), household/diary authorization boundaries, ETag concurrency, and archive export/import round-trips.
2. **Contract & Type Safety (`openapi-typescript` + `svelte-check`):**
    - Zero drift between ASP.NET Core OpenAPI output (`/openapi/v1.json`) and `src/frontend/src/lib/api/schema.d.ts`.
    - Zero TypeScript or Svelte 5 rune diagnostics in `pnpm --dir src/frontend check`.
3. **Frontend Unit & E2E Smoke Suite (`Vitest` + `Playwright`):**
    - Unit tests for scaling utilities, native bridges, offline sync queue, and stores (`pnpm --dir src/frontend test:unit`).
    - Playwright E2E flows (`src/frontend/tests/e2e/`) covering registration/login, recipe creation & scaling, meal planning → shopping list generation, and cooking mode.
4. **Production Container Smoke Test (`docker-smoke`):**
    - Verifies clean startup of the multi-stage `Dockerfile`, automatic PostgreSQL initialization & EF migration under advisory lock, `/healthz == 200`, and zero `.map` file exposure.

---

## 2. UX Quality Bar

- **Optimistic UI with Rollback:** High-frequency taps (shopping item check/uncheck, favorite toggle, star rating, meal plan slot move) update the UI in `< 16 ms` and roll back with a German toast on network failure.
- **No Layout Shifts:** Tabular numerals (`tabular-nums`) on all numbers/timers, fixed aspect-ratio containers on recipe cards, and zero-jitter drawer headers.
- **Data Portability Guarantee:** Every household data entity created in Culina is included in `GET /api/v1/archive/export` and restorable via `POST /api/v1/archive/import`.
