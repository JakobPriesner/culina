# Configuration Reference

Culina uses ASP.NET Core configuration bound to strongly-typed options classes and convenience `CULINA_*` environment variables. Precedence (highest to lowest):

1. **Household UI Settings (`app_settings` table)** — AI provider selection, encrypted API keys, and Ollama base URL configured per household in **Einstellungen → KI-Assistent**.
2. **`CULINA_*` Environment Variables** — Top-level shortcuts mapped in `Program.cs`.
3. **Standard ASP.NET Core Double-Underscore Variables** — e.g. `Jwt__AccessTokenMinutes=30`, `RateLimiting__AuthPermitLimit=20`.
4. **`appsettings.json` / `appsettings.{Environment}.json`** — Built-in defaults.

---

## 1. Required Production Variables

In `ASPNETCORE_ENVIRONMENT=Production` (default in the Docker image), startup refuses to boot (`StartupConfigurationValidator`) unless these are set:

| Variable                         | Config Path                      | Validation Rule                                                                     | Purpose                                                                                                      |
| -------------------------------- | -------------------------------- | ----------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------ |
| `POSTGRES_PASSWORD`              | Container entrypoint             | Non-empty                                                                           | Initializes local PostgreSQL 17 user `culina` and injects `ConnectionStrings__DefaultConnection` when unset. |
| `CULINA_JWT_SECRET`              | `Jwt:SecretKey`                  | ≥ 32 characters (not placeholder)                                                   | HMAC-SHA256 signing key for 15-minute access tokens and OAuth state tokens.                                  |
| `CULINA_SETTINGS_ENCRYPTION_KEY` | `Security:SettingsEncryptionKey` | ≥ 32 bytes (raw or base64)                                                          | AES-256-GCM master key for encrypting household AI API keys and WebPush VAPID private keys.                  |
| `CULINA_PUBLIC_ORIGIN`           | `Security:PublicOrigin`          | Valid `http(s)://` origin (non-loopback unless `CULINA_ALLOW_LOOPBACK_ORIGIN=true`) | Canonical origin used for CORS, CSRF validation, WebAuthn RP origin/domain defaults, and OAuth/share URLs.   |

---

## 2. Database & Networking

| Variable                                 | Default                                                                                | Description                                                                                                                      |
| ---------------------------------------- | -------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------- |
| `ConnectionStrings__DefaultConnection`   | `Host=localhost;Port=5432;Database=culina;Username=culina;Password=$POSTGRES_PASSWORD` | Override to use an external PostgreSQL 17 instance instead of the bundled server.                                                |
| `ASPNETCORE_URLS`                        | `http://+:8080`                                                                        | Kestrel listen address inside the container.                                                                                     |
| `CULINA_ALLOW_LOOPBACK_ORIGIN`           | `false`                                                                                | Permits `http://localhost:*` or `127.0.0.1` as `CULINA_PUBLIC_ORIGIN` in Production (e.g. local trial or SSH tunnel).            |
| `CULINA_FORWARDED_HEADERS_KNOWN_PROXIES` | _(empty)_                                                                              | Comma/semicolon-separated proxy IPs or CIDRs (`172.16.0.0/12,10.0.0.0/8`) trusted for `X-Forwarded-For` and `X-Forwarded-Proto`. |
| `Uploads__RootPath`                      | `/app/uploads` (prod) / `./uploads` (dev)                                              | Filesystem directory for recipe WebP images and temporary intake files.                                                          |

---

## 3. Authentication, Passkeys & Rate Limits

| Variable / Path                        | Default                        | Description                                                                               |
| -------------------------------------- | ------------------------------ | ----------------------------------------------------------------------------------------- |
| `Jwt__Issuer` / `Jwt__Audience`        | `culina`                       | JWT issuer and audience claims.                                                           |
| `Jwt__AccessTokenMinutes`              | `15`                           | Access token lifetime (minutes).                                                          |
| `Jwt__RefreshTokenDays`                | `30`                           | Refresh token sliding lifetime (days), stored in `HttpOnly` cookie `culina_rt`.           |
| `WebAuthn__ServerDomain`               | Host of `CULINA_PUBLIC_ORIGIN` | WebAuthn Relying Party ID (e.g. `culina.example.com`).                                    |
| `WebAuthn__ServerName`                 | `Culina`                       | Display name shown in passkey prompts.                                                    |
| `WebAuthn__Origins__0`                 | `CULINA_PUBLIC_ORIGIN`         | Allowed WebAuthn origins (add `ios:bundle-id` or additional origins via `__1`, `__2`).    |
| `RateLimiting__GlobalPermitLimit`      | `300` / min                    | Per-user (or per-IP) global API rate limit.                                               |
| `RateLimiting__AuthPermitLimit`        | `10` / min                     | Per-IP limit on `/api/v1/auth/*` (login, register, passkey, OAuth token).                 |
| `RateLimiting__AiPermitLimit`          | `20` / min                     | Per-user limit on AI endpoints (intake, assistant, nutrition estimate, image generation). |
| `RateLimiting__PublicSharePermitLimit` | `60` / min                     | Per-IP limit on `/api/v1/shared/*` and `/api/v1/library-sync/feed/*`.                     |

---

## 4. AI Providers & Search Helper

Instance-level defaults (overridable per household in the UI):

| Variable                       | Config Path                    | Default Models (`FastModel` / `CapableModel`)                                                                  |
| ------------------------------ | ------------------------------ | -------------------------------------------------------------------------------------------------------------- |
| `CULINA_AI_PROVIDER`           | `Ai:DefaultProvider`           | Auto-detected (`claude` → `gemini` → `openai` → `openrouter` → `ollama`)                                       |
| `CULINA_ANTHROPIC_API_KEY`     | `Ai:Claude:ApiKey`             | `claude-haiku-4-5` / `claude-sonnet-4-6`                                                                       |
| `CULINA_GEMINI_API_KEY`        | `Ai:Gemini:ApiKey`             | `gemini-3-flash-preview` / `gemini-3.1-pro-preview` (image: `gemini-2.5-flash-image`)                          |
| `CULINA_OPENAI_API_KEY`        | `Ai:OpenAi:ApiKey`             | `gpt-5.4-mini` / `gpt-5.4`                                                                                     |
| `CULINA_OPENROUTER_API_KEY`    | `Ai:OpenRouter:ApiKey`         | `google/gemini-3-flash-preview` / `anthropic/claude-sonnet-4-6`                                                |
| `CULINA_OLLAMA_BASE_URL`       | `Ai:Ollama:BaseUrl`            | `http://localhost:11434` (`gemma4:e4b`)                                                                        |
| `CULINA_ALLOW_LOOPBACK_OLLAMA` | `Security:AllowLoopbackOllama` | `false` in Production (`true` in Dev). Allows household UI settings to point at loopback/RFC1918 Ollama hosts. |
| `CULINA_SEARXNG_BASE_URL`      | `Search:SearxngBaseUrl`        | _(optional)_ SearXNG JSON endpoint (`http://searxng:8080`) enabling Olli's `search_web_recipes` tool.          |

---

## 5. Observability & Background Maintenance

| Variable / Path                | Default                      | Description                                                                                                                                                                                                   |
| ------------------------------ | ---------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `CULINA_METRICS_ENABLED`       | `false`                      | Exposes Prometheus metrics at `GET /metrics`.                                                                                                                                                                 |
| `CULINA_METRICS_BEARER_TOKEN`  | _(empty)_                    | Optional bearer token required to scrape `GET /metrics`.                                                                                                                                                      |
| `OTEL_EXPORTER_OTLP_ENDPOINT`  | _(empty)_                    | Exports OpenTelemetry traces (`Culina.Api`, HTTP, EF Core) and metrics via OTLP.                                                                                                                              |
| `Maintenance__Enabled`         | `true`                       | Runs `DatabaseMaintenanceHostedService` every `IntervalHours` (default `6h`) to prune expired refresh tokens, revocations, idempotency keys (`24h`), intake staging (`24h`), and stale cook sessions (`48h`). |
| `LibrarySync__IntervalMinutes` | `15`                         | Background polling interval for connected household libraries (`LibrarySyncBackgroundService`).                                                                                                               |
| `WebPush__Subject`             | `mailto:admin@<origin-host>` | VAPID subject URI for WebPush timer notifications (keys auto-generated and encrypted in `AppMetadata`).                                                                                                       |

---

## 6. Reverse Proxy Examples

### Caddy

```caddyfile
culina.example.com {
    reverse_proxy culina:8080 {
        flush_interval -1 # Required for SSE (/shopping/stream, /assistant/chat/stream, /mcp)
    }
}
```

### Nginx

```nginx
location / {
    proxy_pass http://127.0.0.1:8080;
    proxy_http_version 1.1;
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_buffering off; # Required for SSE streaming
    proxy_read_timeout 3600s;
}
```
