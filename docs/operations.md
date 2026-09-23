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
compresses no response it generates — compressing a cookie-authenticated
response invites BREACH. Its static files arrive already compressed at build
time, which is safe because they are the same for everyone. If your proxy
compresses, leave `/api` out of it. It trusts `X-Forwarded-*` only from the
proxies you name.

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

**One upgrade takes longer than the rest.** The migration that adds recipe
search builds a search document for every recipe you already have, inside its
own transaction, so that the first search after the upgrade is answered from a
complete index rather than an empty one. Budget roughly two seconds per five
hundred recipes — about nine for a library of two thousand. Nothing else in the
upgrade is affected, and it happens exactly once.

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

The key ring is the one that used to be cheap to lose and no longer is. It now
encrypts the model provider's API key, so restoring an instance without it
leaves the assistant switched off with an unreadable key stored — which the app
treats as "no assistant is configured" rather than as an error. An administrator
enters the key again and everything else is where it was. Nothing else is lost:
Culina's session cookie carries an opaque reference rather than an encrypted
payload, so the key ring still has nothing to do with who stays signed in.

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

### Rehearsing it

```bash
scripts/restore-rehearsal.sh
```

Stands up a clean instance in a project of its own, puts real data in it, takes
the three backups above, destroys every volume, restores, and then checks what
came back: the password still signs in, every recipe and every member is there,
and the photograph is served to the person who uploaded it and still refused to
a stranger. It tears itself down and touches nothing you are running.

Run it before a release. The first time it ran it found that a fresh instance
could not accept a photograph at all — the volumes arrived owned by root and the
app does not run as root — which no amount of reading the runbook would have
found.

**Measured, on a laptop with three recipes and one photograph: eight seconds.**
That number is not a service level. What it is useful for is the shape: the
restore is bounded by the size of the dump and the images, both of which are
small for a household, and there is no rebuild or reindex step hiding in it.
A household with a thousand recipes and a photograph on each should still expect
minutes rather than hours.

### How often, and where

Culina does not schedule backups; your host already has a way to run a command
nightly, and a backup system you already operate is worth more than one this app
invented. What the app can say is what a sensible target looks like for a
self-hosted instance:

- **Nightly**, which puts at most a day's cooking at risk. Recipes are written
  rarely; losing a day of them is an annoyance, and paying for anything tighter
  is not obviously worth it.
- **Kept off the machine that holds the original.** A backup on the same disk is
  a copy, not a backup.
- **Encrypted at rest if it leaves your network.** The dump contains every
  recipe, every address and every password hash. Argon2id hashes are not
  reversible, but they are not something to hand out either.
- **Readable only by whoever runs the instance.** `chmod 600`, and an object
  store bucket that is not public.
- **Watched.** A backup that silently stopped six weeks ago is the usual way
  this goes wrong. Alert on the age of the newest file, not on the exit code of
  the job — a job that succeeds at writing nothing looks fine.

## Rolling back

Redeploy the previous tag. Safe as long as no migration since then was
destructive — which is why migrations are additive by default, and why a
destructive one is called out in its release notes.

What makes that safe rather than hopeful:

- **Migrations are forward-only and never edited.** An applied migration whose
  file has changed stops the process at boot rather than running anything
  (`ApplyAsync_ShouldRefuseToStart_WhenAnAppliedMigrationWasEdited`). A mistake
  is a new migration, always.
- **Applying them twice does nothing.** The runner compares what is embedded
  against `schema_migrations` and applies only the difference
  (`ApplyAsync_ShouldChangeNothing_WhenEveryMigrationHasAlreadyRun`).
- **An older image against a newer schema works** for any additive migration:
  the older code simply does not know about the new columns. It does not work
  across a destructive one, which is why those are called out.
- **A failed migration serves no traffic.** It logs `Critical` and exits
  non-zero; earlier migrations that succeeded stay applied
  (`ApplyAsync_ShouldKeepEarlierMigrations_WhenALaterOneFails`).

There has been no release yet, so "migrations against the previous release" has
nothing to test against. From the first tag onwards, the release checklist is:
run the rehearsal, then start the new image against a database restored from the
previous release's dump.

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

## What works without a network, and what does not

Culina installs as an app and opens without a network. What that means exactly,
because a promise about offline behaviour is easy to overstate:

**It opens.** The app itself — the shell, the fonts, the icons — is stored per
build. A kitchen the wifi does not reach shows Culina saying it cannot reach the
server, rather than the browser's error page.

**A recipe you have opened stays readable.** Reading a recipe stores it, along
with its photograph and who you are. Opening it again with no network shows it.
That is the whole of the promise: recipes nobody has opened are not there, and
nothing in the app claims they are.

**Nothing can be changed.** Every change goes to the server. Offline, the app
says so — "You are offline. This will be possible again when you reconnect" —
rather than pretending to save and losing the work later. There is no queue of
pending changes, deliberately: a queue that replays into a shared household is a
conflict-resolution problem nobody asked for.

**What is stored, and for how long.** At most 120 recipe responses, oldest
first, in a store the browser may evict at any time for its own reasons. It is
emptied when anybody signs in or out on that device — both, because a browser
closed without signing out never reached the way out.

**A disconnected device cannot learn that access was revoked.** Somebody removed
from a household keeps whatever their device already stored until it next
reaches the server. There is no way around this: the device is not in contact
with anything that could tell it. It is the same limit as a printed recipe, and
it is worth knowing rather than assuming otherwise. Everything else — the
recipes they had not opened, the shopping list, the household — is gone the
moment they reconnect.

**A shared device keeps what was read on it.** A tablet on a counter that two
people use stores the recipes whoever last used it opened, until the next person
signs in. If that matters for your household, sign out rather than closing the
lid.

## When something is wrong

| | |
| --- | --- |
| The app exits at boot with a configuration message | It is telling you exactly which value it cannot work with. Every setting is validated at startup on purpose. |
| Sign-in works and then every action fails | Almost always `Cookies__Secure=true` behind a proxy that is not forwarding `X-Forwarded-Proto`. |
| Rate limits trigger for everyone at once | `ForwardedHeaders__KnownProxies`/`KnownNetworks` is not set, so every request looks like it comes from the proxy. |
| Photographs vanished after a deploy | `/data/images` was not a volume. There is no recovering them without a backup. |
