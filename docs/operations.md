# Operations Runbook

Day-1 and day-2 guide for self-hosting Culina (`ghcr.io/jakobpriesner/culina`). Single-container image bundles PostgreSQL 17, .NET 10 API, SvelteKit frontend, and `supervisord`.

---

## 1. Installation

### Prerequisites

- Linux/macOS host with Docker 24+ and Docker Compose v2 (`amd64` or `arm64`)
- 1 vCPU, 1 GB RAM minimum (2 GB recommended), 5 GB disk
- Optional: reverse proxy (Caddy, Traefik, Nginx) terminating TLS

### Secrets & `compose.yaml`

Generate `.env`:

```bash
cat <<EOF > .env
POSTGRES_PASSWORD=$(openssl rand -hex 24)
CULINA_JWT_SECRET=$(openssl rand -base64 48 | tr -d '\n')
CULINA_SETTINGS_ENCRYPTION_KEY=$(openssl rand -base64 32 | tr -d '\n')
CULINA_PUBLIC_ORIGIN=https://culina.example.com
CULINA_FORWARDED_HEADERS_KNOWN_PROXIES=172.16.0.0/12
EOF
chmod 600 .env
```

> **Back up `CULINA_SETTINGS_ENCRYPTION_KEY` in your password manager.** It encrypts household AI keys and WebPush VAPID keys at rest (AES-256-GCM). Losing it renders stored provider keys unreadable.

Start and verify:

```bash
docker compose up -d
curl -fsS http://localhost:8080/healthz
# {"status":"Healthy","entries":{"database":{"status":"Healthy"},"uploads":{"status":"Healthy"}}}
```

The first registered user becomes the instance owner.

---

## 2. Upgrades & Rollbacks

EF Core migrations run automatically at startup inside a PostgreSQL advisory lock (`MigrationHostedService`).

### Upgrade Procedure

```bash
# 1. Pre-upgrade logical backup
docker exec culina pg_dump -U culina -d culina -Fc > "culina-pre-upgrade-$(date +%F).dump"

# 2. Pull & recreate
docker compose pull
docker compose up -d
docker compose logs -f --tail=100 culina
```

### Rollback Procedure

Down-migrations are not executed automatically. To roll back across a schema change:

1. Pin the previous image tag in `compose.yaml` (`ghcr.io/jakobpriesner/culina:1.x.y`).
2. Restore the pre-upgrade `.dump` using [Section 4](#4-restore-drill) before starting the older image.

---

## 3. Backups

Culina persists state in two Docker volumes plus `.env`:

1. **`/var/lib/postgresql/data`** — PostgreSQL 17 database (consistent backups require `pg_dump -Fc`, not raw volume copies while running).
2. **`/app/uploads`** — Recipe WebP images and intake staging files.
3. **`.env`** — `CULINA_SETTINGS_ENCRYPTION_KEY` and `CULINA_JWT_SECRET`.

### Automated Daily Backup Script (`backup-culina.sh`)

```bash
#!/usr/bin/env bash
set -euo pipefail

CONTAINER="${CULINA_CONTAINER:-culina}"
BACKUP_DIR="${BACKUP_DIR:-/var/backups/culina}"
RETENTION_DAYS="${RETENTION_DAYS:-14}"
STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT

mkdir -p "$BACKUP_DIR"

# 1. Consistent custom-format Postgres dump
docker exec "$CONTAINER" pg_dump -U culina -d culina -Fc > "$WORK_DIR/db.dump"

# 2. Uploads archive
docker exec "$CONTAINER" tar -C /app -czf - uploads > "$WORK_DIR/uploads.tar.gz"

# 3. Bundle & prune
tar -C "$WORK_DIR" -czf "$BACKUP_DIR/culina-backup-$STAMP.tar.gz" db.dump uploads.tar.gz
chmod 600 "$BACKUP_DIR/culina-backup-$STAMP.tar.gz"
find "$BACKUP_DIR" -name 'culina-backup-*.tar.gz' -mtime +"$RETENTION_DAYS" -delete
```

---

## 4. Restore Drill

```bash
ARCHIVE="/var/backups/culina/culina-backup-YYYYMMDDTHHMMSSZ.tar.gz"
RESTORE_DIR="$(mktemp -d)"
tar -xzf "$ARCHIVE" -C "$RESTORE_DIR"

# 1. Start container, then stop API process while keeping Postgres alive
docker compose up -d
docker exec culina supervisorctl stop culina-api

# 2. Recreate schema cleanly and restore dump
docker exec -i culina pg_restore -U culina -d culina --clean --if-exists --no-owner < "$RESTORE_DIR/db.dump"

# 3. Restore uploads and fix ownership
docker exec culina sh -c 'rm -rf /app/uploads/*'
docker exec -i culina tar -C /app -xzf - < "$RESTORE_DIR/uploads.tar.gz"
docker exec culina chown -R culina:culina /app/uploads

# 4. Restart API and verify
docker exec culina supervisorctl start culina-api
curl -fsS http://localhost:8080/healthz
rm -rf "$RESTORE_DIR"
```

---

## 5. Key Rotation & Loss Recovery

- **Rotating `CULINA_JWT_SECRET`:** Update `.env` and `docker compose up -d`. Active access tokens (`15 min`) and refresh tokens are invalidated; users sign in again. Passkey credentials and data are unaffected.
- **Recovering from a lost `CULINA_SETTINGS_ENCRYPTION_KEY`:**
    1. Generate a new 32-byte base64 key in `.env`.
    2. Clear unreadable ciphertext rows so the UI/push service can re-initialize cleanly:
        ```bash
        docker exec -i culina psql -U culina -d culina <<'SQL'
        UPDATE app_settings
           SET anthropic_api_key_encrypted = NULL,
               gemini_api_key_encrypted = NULL,
               openai_api_key_encrypted = NULL,
               openrouter_api_key_encrypted = NULL;
        DELETE FROM "AppMetadata" WHERE "Key" IN ('WebPush:PublicKey', 'WebPush:PrivateKeyEncrypted');
        DELETE FROM "PushSubscriptions";
        SQL
        ```
    3. Restart (`docker compose up -d`) and re-enter AI keys in **Einstellungen → KI-Assistent**.

---

## 6. Alerts & Health Monitoring {#alerts}

- **Liveness/Readiness:** `GET /healthz` checks PostgreSQL connectivity (`SELECT 1`) and write permission on `/app/uploads`. Returns HTTP `200` (`Healthy`) or `503` (`Unhealthy`).
- **Prometheus Metrics:** `GET /metrics` (enable via `CULINA_METRICS_ENABLED=true`, restrict to internal/bearer scraping via `CULINA_METRICS_BEARER_TOKEN`).

### Recommended Alert Rules

| Alert                  | Condition / PromQL                                                                                  | Action                                                                                            |
| ---------------------- | --------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------- |
| **InstanceDown**       | `GET /healthz != 200` for `2m`                                                                      | Check `docker compose logs culina`; verify disk space & Postgres status (`supervisorctl status`). |
| **High5xxRate**        | `sum(rate(http_server_request_duration_seconds_count{http_response_status_code=~"5.."}[5m])) > 0.1` | Inspect structured error logs (`problemCode`, exception stack) and upstream AI/DB availability.   |
| **DiskSpaceLow**       | Volume `/var/lib/postgresql/data` or `/app/uploads` > 85% full                                      | Prune old backups or expand Docker host volume.                                                   |
| **LibrarySyncStalled** | Connected library `lastSyncError` non-null or `lastSyncedAt` > 6h old                               | Verify remote instance reachability, TLS certificate validity, and share token expiration.        |

---

## 7. Troubleshooting

| Symptom                                                        | Root Cause                                                                                                                                               | Resolution                                                                                                                                                          |
| -------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Container exits immediately with `InvalidOperationException`   | Missing/short `CULINA_JWT_SECRET` (<32 chars), missing `CULINA_SETTINGS_ENCRYPTION_KEY` (<32 bytes), or `localhost` `CULINA_PUBLIC_ORIGIN` in Production | Fix `.env` values (see [`docs/configuration.md`](configuration.md)).                                                                                                |
| Passkey registration fails (`SecurityError` / RP ID mismatch)  | `CULINA_PUBLIC_ORIGIN` or `WebAuthn:ServerDomain` does not match browser URL, or `X-Forwarded-Proto` is untrusted                                        | Set `CULINA_PUBLIC_ORIGIN=https://<domain>` and `CULINA_FORWARDED_HEADERS_KNOWN_PROXIES` to your proxy IP/CIDR.                                                     |
| Login loops / cookies not sticking over HTTP on LAN            | Production mode sets `Secure` on cookies when `CULINA_PUBLIC_ORIGIN` uses `https://`                                                                     | Use HTTPS via reverse proxy/Tailscale, or set `CULINA_PUBLIC_ORIGIN=http://<lan-ip>:8080` for plain HTTP LAN access.                                                |
| Local Ollama unreachable from container (`Connection refused`) | `localhost` inside container refers to the container itself                                                                                              | Use `http://host.docker.internal:11434` (with `extra_hosts: ["host.docker.internal:host-gateway"]` on Linux) and set `CULINA_ALLOW_LOOPBACK_OLLAMA=true` if needed. |
| Recipe images return `404` after migration                     | `/app/uploads` volume was not mounted or restored with wrong UID                                                                                         | Mount `culina-uploads:/app/uploads` and run `docker exec culina chown -R culina:culina /app/uploads`.                                                               |
