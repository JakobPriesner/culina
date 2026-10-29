# Contributing to Culina

## Prerequisites

- **.NET 10 SDK** (`10.0.100+`, pinned in `global.json`)
- **Node.js 24 LTS** + **pnpm 10** (`corepack enable`)
- **PostgreSQL 17** (via `compose.dev.yaml`)
- **Docker** (for Testcontainers integration tests and local Postgres)

## Development Setup

```bash
docker compose -f compose.dev.yaml up -d db searxng
dotnet run --project src/backend/Culina.Api          # http://localhost:5180
pnpm --dir src/frontend install && pnpm --dir src/frontend dev  # http://localhost:5173
```

## Code Organization

- `src/backend/Culina.Domain` — Entities, value objects, domain services (`IngredientAggregator`, `QuantityFormatter`, `IngredientAvailability`, `RecipeNutritionEstimator`). Zero infrastructure dependencies.
- `src/backend/Culina.Application` — Request/response DTOs, validators, service interfaces.
- `src/backend/Culina.Infrastructure` — EF Core (`CulinaDbContext`), PostgreSQL migrations, AI providers, scrapers, background jobs, search/suggestion engines.
- `src/backend/Culina.Api` — Minimal API endpoints (`/api/v1/*`), MCP server (`/mcp`), auth, rate limiting, ProblemDetails handling.
- `src/backend/Culina.Tests` — xUnit unit + Testcontainers PostgreSQL integration tests.
- `src/frontend/src` — SvelteKit 2 + Svelte 5 runes (`$state`, `$derived`, `$effect`), generated OpenAPI client (`$lib/api/schema.d.ts`), Warm Modernism CSS tokens (`app.css`).

## Verification Before Submitting

1. **Backend tests:** `dotnet test src/backend/Culina.slnx`
2. **OpenAPI client sync (if API DTOs/endpoints changed):**
    ```bash
    dotnet build src/backend/Culina.Api
    pnpm --dir src/frontend generate:api
    git diff --exit-code src/frontend/src/lib/api/schema.d.ts
    ```
3. **Frontend checks & unit tests:**
    ```bash
    pnpm --dir src/frontend check
    pnpm --dir src/frontend lint
    pnpm --dir src/frontend test:unit
    ```
4. **Database migrations (if entities/mappings changed):**
    ```bash
    dotnet ef migrations add <MigrationName> \
      --project src/backend/Culina.Infrastructure \
      --startup-project src/backend/Culina.Api
    ```

## Coding & Design Conventions

- **German UI copy:** All user-facing strings use informal _Du_, correct umlauts (`ä`, `ö`, `ü`, `ß`), typography (`–` for ranges, `…` for ellipsis), and terminology from [`docs/glossary.md`](docs/glossary.md).
- **Warm Modernism tokens:** Never hardcode hex colours or pixel widths in Svelte components; use semantic tokens from `src/frontend/src/app.css` (see [`docs/design-system.md`](docs/design-system.md)).
- **Generated API client:** Never hand-edit `src/frontend/src/lib/api/schema.d.ts` or construct raw `fetch('/api/v1/...')` calls when typed `apiClient` helpers exist.

## Commit Rules

- Use concise imperative subjects (`Add pantry expiry filter`, `Fix unit conversion for pinch`).
- **No AI attribution:** Commits must be authored solely by the human developer (`Jakob Priesner <jakob.priesner@outlook.de>`). Never include `Claude`, `Anthropic`, `Co-Authored-By`, `Generated with Claude`, or AI-tool `Signed-off-by` trailers in commit messages or author fields (enforced by `.beads/hooks/commit-msg`).
