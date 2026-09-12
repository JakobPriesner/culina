# Culina — deployment and delivery pipeline

Culina ships as **one container image** that serves the API and the built SPA
from the same origin, plus a PostgreSQL database. That single-origin property is
not a packaging convenience — it is what removes CORS entirely and makes the
cookie/CSRF model in `cookie-auth-and-security` sound.

## Runtime topology

```
            ┌────────────────────────────────────────────┐
  browser ──┤ operator's reverse proxy (TLS, HSTS)        │
            └───────────────────┬────────────────────────┘
                                │ http, X-Forwarded-*
                   ┌────────────┴─────────────┐
                   │  culina  (one container) │
                   │  Kestrel :8080           │
                   │   /api/*  → endpoints    │
                   │   /*      → SPA fallback │
                   │  volumes: /data/images   │
                   │           /data/keys     │
                   └────────────┬─────────────┘
                                │
                        ┌───────┴────────┐
                        │  postgres:18   │
                        └────────────────┘
```

**TLS terminates at the operator's proxy.** The app sets no HSTS, performs no
HTTPS redirect and does no response compression — compressing
cookie-authenticated responses invites BREACH. It trusts `X-Forwarded-*` only
from configured proxy addresses (`ForwardedHeaders:KnownProxies`).

Two volumes are mandatory, and forgetting either is a silent failure:

| Path | Why |
| --- | --- |
| `/data/images` | Recipe images. Content-addressed; lost on redeploy otherwise. |
| `/data/keys` | ASP.NET data-protection keys. |

A note on `/data/keys`, because the usual warning does **not** apply here:
Culina's session cookie carries an opaque reference, not an encrypted payload,
so the session row is what authenticates a request and a lost key ring does not
sign anyone out. The volume is still configured — anything the framework
protects later would otherwise change key on every restart — but the volume that
genuinely must survive a redeploy is `/data/images`.

## The image

Multi-stage, deterministic, non-root, no build tooling in the final layer.

```
stage 1  node:22-alpine     pnpm install --frozen-lockfile
                            pnpm generate:api        (from the committed OpenAPI doc)
                            pnpm build               → build/
stage 2  dotnet/sdk:10.0    dotnet restore --locked-mode
                            copy stage-1 build/ into Api/wwwroot/
                            dotnet publish -c Release
stage 3  dotnet/aspnet:10.0-noble-chiseled
                            non-root (uid 64198), read-only rootfs,
                            no shell, HEALTHCHECK → /health/ready
```

- `--locked-mode` / `--frozen-lockfile` on both sides: a build that silently
  resolves a different dependency is not reproducible.
- The frontend is generated from the **committed** `openapi/Api.json`, never
  from a running server, so the image build needs no database.
- Chiseled runtime: no shell, no package manager — a much smaller attack
  surface and a much smaller CVE feed.
- Labels carry `org.opencontainers.image.revision` and `.version` so a running
  container can always be traced back to a commit.

## Configuration

Bootstrap settings only, via environment variables (`dotnet-configuration`);
everything an admin can change lives in the database instead.

| Variable | Default | |
| --- | --- | --- |
| `Database__Host` / `__Port` / `__Name` / `__Username` / `__Password` | — | required, parts not a connection string |
| `Database__RequireSsl` | `true` | |
| `Storage__ImagePath` | `/data/images` | |
| `Storage__DataProtectionKeyPath` | `/data/keys` | |
| `Cookies__Secure` | `true` | only ever `false` for local HTTP dev |
| `ForwardedHeaders__KnownProxies` | — | comma-separated; required behind a proxy |
| `PasswordHashing__*` | Argon2id defaults | memory, iterations, parallelism |
| `RateLimits__*` | sensible | login, registration, invitation redemption |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | unset | unset ⇒ console logs only, no export |

**The process must refuse to start on invalid configuration.** Every settings
record has `Validate()`, called at startup; a misconfigured deployment fails
loudly rather than on the first request that needed the value.

Secrets arrive as env vars or Docker secrets. There is no secret in any
committed file, and `Database__Password` is never logged, echoed by an endpoint
or included in a problem document.

## Database migrations

Numbered `.sql` files embedded in `Infrastructure`, applied at startup by a
hosted service **before** the app accepts traffic:

1. Take a Postgres **advisory lock**, so rolling or replicated starts cannot
   race each other.
2. Compare `schema_migrations` with the embedded set.
3. Apply each missing migration in its own transaction, in order.
4. Release, log one `Information` line per applied migration, then serve.

Forward-only. A mistake is a new migration, never an edited one. A migration
that fails is `Critical` and the process exits non-zero — a half-migrated
database must not serve traffic.

## CI pipeline (GitHub Actions)

Three workflows. Everything below runs on every pull request; only the last job
differs on `main`.

### `ci.yml` — on push and pull request

```
backend        dotnet format --verify-no-changes
               dotnet build -warnaserror          (warnings are already errors)
               dotnet test  Domain.UnitTests Application.UnitTests ArchitectureTests
               dotnet test  IntegrationTests      (Testcontainers → real Postgres)

frontend       pnpm install --frozen-lockfile
               pnpm lint · pnpm check             (svelte-check, zero errors)
               pnpm test:unit                     (Vitest incl. scaling + theme contract)
               pnpm build

contract       dotnet build → export openapi/Api.json
               pnpm generate:api
               git diff --exit-code               ← a stale generated client cannot merge

e2e            docker compose up + Playwright: auth, create, scale, cook, shop
               uploads traces and screenshots on failure

security       CodeQL (C#, TypeScript)
               dotnet list package --vulnerable --include-transitive  (fails on any)
               pnpm audit --audit-level=high
               Trivy on the built image (fails on HIGH/CRITICAL, fixed only)
               Gitleaks secret scan
```

The `contract` job is the one that keeps the two halves honest: the backend and
the generated frontend client cannot drift, because drift fails the build.

### `release.yml` — on a tag `v*`

Builds `linux/amd64` + `linux/arm64` with Buildx, pushes to GHCR tagged
`vX.Y.Z`, `vX.Y` and `latest`, generates an SBOM (Syft), signs the image and the
SBOM with Cosign (keyless OIDC), and attaches SLSA build provenance. The release
notes are generated from the beads closed since the previous tag.

### `nightly.yml`

Re-scans the **published** image with Trivy — a CVE disclosed after release is
exactly the one nobody notices — refreshes the dependency audit, and opens a
bead when something needs action.

## Environments

| | |
| --- | --- |
| **Local dev** | `docker compose up -d db` + `dotnet watch` + `pnpm dev`. Vite proxies `/api` → backend so the app is same-origin in development too — cookies and CSRF behave exactly as in production, which is why there is no CORS anywhere and no dev-only auth path. |
| **Local prod check** | `docker compose -f compose.yaml -f compose.prod.yaml up` — the real image, real migrations, `Cookies__Secure=false` behind a local proxy only. |
| **Production** | The published image behind the operator's proxy. Deploy = pull the new tag, restart. Migrations run at boot. |

## Operating it

- **Health**: `/health/live` (process) and `/health/ready` (database). The
  container `HEALTHCHECK` and any orchestrator use `ready`.
- **Observability**: set `OTEL_EXPORTER_OTLP_ENDPOINT` and all three signals
  export. Unset, the app logs JSON to stdout and exports nothing. Culina
  invents no telemetry configuration names of its own.
- **Backups**: `pg_dump` for the database plus a copy of `/data/images` and
  `/data/keys`. The documented restore procedure is tested as part of the
  release checklist — an untested backup is a hope.
- **Rollback**: redeploy the previous tag. Safe as long as no migration since
  then was destructive, which is why migrations are additive by default and a
  destructive one is called out in its release notes.
- **Zero downtime is not a goal.** A single-instance self-hosted recipe app
  restarts in a second or two; engineering around that would cost more than it
  returns.

## Security posture

- Non-root, read-only root filesystem, `no-new-privileges`, dropped
  capabilities, and only the two data volumes writable.
- The database is not published to the host in the production compose file —
  it is reachable only on the internal network.
- Security headers, CSP with a per-response nonce, and no `unsafe-inline`
  anywhere, per `cookie-auth-and-security`.
- Dependency and image scanning gate the pipeline; `NuGetAudit` is already on
  at `low` in `Directory.Build.props`, so a vulnerable transitive package fails
  the compile, not the review.
- `/.well-known/security.txt` ships with a working contact and a future
  `Expires` (`frontend-static-assets`).
