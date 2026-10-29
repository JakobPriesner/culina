# Deployment & CI/CD Architecture

Culina ships as a single self-contained container image (`ghcr.io/jakobpriesner/culina`) for `linux/amd64` and `linux/arm64`.

---

## 1. Container Topology

```
┌───────────────────────────────────────────────────────────┐
│ Container: ghcr.io/jakobpriesner/culina (:8080)           │
│                                                           │
│  docker-entrypoint.sh (root bootstrap)                    │
│   ├─ Initializes /var/lib/postgresql/data if empty        │
│   ├─ Ensures user/db `culina` + syncs $POSTGRES_PASSWORD  │
│   ├─ Chowns /app/uploads to `culina:culina` (UID 10001)   │
│   └─ Execs supervisord                                    │
│       ├─ [postgres]   (user: postgres, 127.0.0.1:5432)    │
│       └─ [culina-api] (user: culina,   0.0.0.0:8080)      │
│            ├─ Serves SvelteKit static SPA (/app/wwwroot)  │
│            ├─ Serves REST API (/api/v1/*) & MCP (/mcp)    │
│            └─ Runs EF Core migrations under advisory lock │
└───────────────────────────────────────────────────────────┘
```

- **External PostgreSQL mode:** If `ConnectionStrings__DefaultConnection` points to a non-local host, you can run without the local Postgres volume or use `compose.dev.yaml` for split-container development.

---

## 2. Multi-Stage `Dockerfile`

1. **`frontend-build` (`node:24-alpine`):** Runs `pnpm install --frozen-lockfile` and `pnpm build` (`@sveltejs/adapter-static` with `200.html` SPA fallback).
2. **`backend-build` (`mcr.microsoft.com/dotnet/sdk:10.0-noble`):** Restores and publishes `src/backend/Culina.Api/Culina.Api.csproj` (`-c Release`).
3. **`runtime` (`mcr.microsoft.com/dotnet/aspnet:10.0-noble`):**
    - Installs `postgresql-17`, `supervisor`, `curl`, `gosu`, and `libgssapi-krb5-2`.
    - Creates unprivileged system user `culina` (`UID 10001`).
    - Copies backend binaries to `/app` and static frontend assets to `/app/wwwroot`.
    - Configures Docker `HEALTHCHECK` against `http://127.0.0.1:8080/healthz`.

---

## 3. Database Migrations (`MigrationHostedService`)

- On startup, `MigrationHostedService` acquires PostgreSQL session advisory lock `784_920_113` (`pg_advisory_lock`), runs `db.Database.MigrateAsync()`, seeds default categories/BLS nutrition reference data (`BlsSeederHostedService`), and releases the lock.
- Concurrent replicas or rapid restarts wait safely on the advisory lock rather than racing schema changes.

---

## 4. CI/CD Pipelines (`.github/workflows/`)

### `ci.yml` (Every Push & PR)

- **`backend-test`:** Builds `Culina.slnx` and runs xUnit + Testcontainers PostgreSQL 17 integration tests.
- **`frontend-verify`:** Boots the compiled backend solely to export `/openapi/v1.json`, verifies `pnpm generate:api` produces zero diff against `src/frontend/src/lib/api/schema.d.ts`, then runs `pnpm lint`, `pnpm check`, and `pnpm test:unit` (Vitest).
- **`e2e-smoke`:** Runs Playwright smoke suites against the full stack (`src/frontend/tests/e2e`).
- **`docker-smoke`:** Builds the production `Dockerfile`, boots the container with real secrets, waits for `/healthz == 200`, and verifies static asset serving and security headers.

### `release.yml` (Tags `v*.*.*`)

- Builds multi-arch (`linux/amd64`, `linux/arm64`) images via Docker Buildx, pushes tags (`latest`, `X.Y.Z`, `X.Y`, `sha-...`) to `ghcr.io/jakobpriesner/culina`, generates CycloneDX SBOMs (`anchore/sbom-action`), and attests build provenance (`actions/attest-build-provenance`).
