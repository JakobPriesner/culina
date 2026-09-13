# Operating Culina

For the person who runs the instance. Culina is meant to be run by one of the
people who cooks in the kitchen, not by a platform team, so everything here is
a handful of commands.

## What it is made of

```
            ┌────────────────────────────────────────────┐
  browser ──┤ your reverse proxy (TLS, HSTS)             │
            └───────────────────┬────────────────────────┘
                                │ http, X-Forwarded-*
                   ┌────────────┴─────────────┐
                   │  culina  (one container) │
                   │  Kestrel :8080           │
                   │   /api/*  → the API      │
                   │   /*      → the app      │
                   │  volumes: /data/images   │
                   │           /data/keys     │
                   └────────────┬─────────────┘
                                │
                        ┌───────┴────────┐
                        │  postgres:18   │
                        └────────────────┘
```

**TLS is your proxy's job.** Culina sets no HSTS, performs no HTTPS redirect and
compresses nothing — compressing a cookie-authenticated response invites BREACH.
It trusts `X-Forwarded-*` only from the proxies you name.

## Installing

```bash
git clone https://github.com/jakobpriesner/culina.git
cd culina
cp .env.example .env
```

Fill in `.env`. Three values have no sensible default and the app will refuse to
start without them:

- `Database__Password` — anything long and random.
- `ForwardedHeaders__KnownNetworks` (or `__KnownProxies`) — where your proxy
  sits. `docker network inspect` gives you the subnet.
- `Cookies__Secure=true` — which the production compose file sets for you.

Then:

```bash
docker compose -f compose.yaml -f compose.prod.yaml up -d
```

The database starts, the app waits for it to be healthy, applies its migrations
and begins serving on `${CULINA_PORT:-8080}`. Point your proxy at that.

**The first account you create becomes the administrator** and gets a household
of its own. Whether anyone else may register is that administrator's decision,
made in the app, not a setting in a file.

### A reverse proxy, minimally

```nginx
location / {
    proxy_pass http://127.0.0.1:8080;
    proxy_set_header Host              $host;
    proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;

    # Recipe photographs are the large thing anyone uploads.
    client_max_body_size 12m;
}
```

The two `X-Forwarded-*` headers matter: without them Culina sees your proxy as
every visitor and marks cookies for the wrong scheme.

## Upgrading

```bash
docker compose -f compose.yaml -f compose.prod.yaml pull
docker compose -f compose.yaml -f compose.prod.yaml up -d
```

Migrations run at boot, inside an advisory lock so a restart cannot race
itself, and each in its own transaction. A migration that fails logs `Critical`
and the process exits non-zero: a half-migrated database must not serve traffic.

**Zero downtime is not a goal.** A single-instance self-hosted recipe app
restarts in a second or two, and engineering around that would cost more than it
returns.

## Backing up

Three things, and all three are needed:

```bash
# 1. The database.
docker compose -f compose.yaml -f compose.prod.yaml exec -T db \
    pg_dump -U postgres --format=custom culina > culina-$(date +%F).dump

# 2. The photographs. Not in the database, and not rebuildable.
docker run --rm -v culina_culina-images:/data -v "$PWD":/backup alpine \
    tar czf /backup/culina-images-$(date +%F).tar.gz -C /data .

# 3. The key ring.
docker run --rm -v culina_culina-keys:/data -v "$PWD":/backup alpine \
    tar czf /backup/culina-keys-$(date +%F).tar.gz -C /data .
```

Volume names are prefixed with your compose project name — `docker volume ls`
shows the real ones.

## Restoring

This procedure is run against a real instance as part of the release checklist,
because an untested backup is a hope rather than a backup.

```bash
# Stop the app, leaving the database up.
docker compose -f compose.yaml -f compose.prod.yaml stop app

# Recreate the database empty and load the dump into it.
docker compose -f compose.yaml -f compose.prod.yaml exec -T db \
    psql -U postgres -d postgres -c "drop database if exists culina;"
docker compose -f compose.yaml -f compose.prod.yaml exec -T db \
    psql -U postgres -d postgres -c "create database culina owner culina_app;"
docker compose -f compose.yaml -f compose.prod.yaml exec -T db \
    pg_restore -U postgres -d culina --no-owner --role=culina_app < culina-2026-09-13.dump

# The photographs back into their volume.
docker run --rm -v culina_culina-images:/data -v "$PWD":/backup alpine \
    tar xzf /backup/culina-images-2026-09-13.tar.gz -C /data

docker compose -f compose.yaml -f compose.prod.yaml up -d
```

`--no-owner --role=culina_app` matters: the dump is taken as the superuser and
the running app is not one, so without it every restored table belongs to a role
the app cannot write to.

On the way back up the app finds the schema already at the version the dump was
taken at and applies nothing. Signing in works, and every recipe is there.

## Rolling back

Redeploy the previous tag. Safe as long as no migration since then was
destructive — which is why migrations are additive by default, and why a
destructive one is called out in its release notes.

## Keeping an eye on it

- **Health.** `/health/live` says a process exists; `/health/ready` says it can
  reach the database. Your load balancer wants `ready`.
- **Logs.** JSON on stdout, one line per request, each carrying the request id
  that the app also shows to whoever hit the error. Ask them for it.
- **Telemetry.** Set `OTEL_EXPORTER_OTLP_ENDPOINT` and traces, metrics and logs
  all export. Unset, nothing leaves the machine.
- **`Cannot load library libgssapi_krb5.so.2` at boot is expected.** Npgsql
  probes for Kerberos and the chiseled runtime ships none. Culina authenticates
  to PostgreSQL with a password, so nothing needs it. Two lines at startup and
  never again; adding six system libraries to the image to silence them is not a
  trade worth making.

## When something is wrong

| | |
| --- | --- |
| The app exits at boot with a configuration message | It is telling you exactly which value it cannot work with. Every setting is validated at startup on purpose. |
| Sign-in works and then every action fails | Almost always `Cookies__Secure=true` behind a proxy that is not forwarding `X-Forwarded-Proto`. |
| Rate limits trigger for everyone at once | `ForwardedHeaders__KnownProxies`/`KnownNetworks` is not set, so every request looks like it comes from the proxy. |
| Photographs vanished after a deploy | `/data/images` was not a volume. There is no recovering them without a backup. |
