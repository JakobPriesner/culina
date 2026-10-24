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
                   │           /data/config   │
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

**Nutrition data ships inside the image.** The food composition table
(Bundeslebensmittelschlüssel 4.0, Max Rubner-Institut) is embedded in the
backend: no volume, no download on first run, and no outbound request — Culina
still makes none that a person did not ask for. It is licensed CC BY 4.0, which
requires attribution wherever its values are shown; the recipe page's nutrition
panel carries it. Keep that line if you change the panel, and keep
`src/backend/src/Infrastructure/Nutrition/BLS-ATTRIBUTION.md` with the file.

## Installing

```bash
git clone https://github.com/jakobpriesner/culina.git
cd culina
cp .env.example .env
```

Fill in `.env`. Two values have no sensible default, and the compose file will
refuse to start without them:

- `Database__Password` — the app's own role. Anything long and random.
- `POSTGRES_SUPERUSER_PASSWORD` — the database superuser's, which the app never
  uses. Long, random and different. The setup screen accepts any role, so a
  guessable superuser password is a way to run the app as one.

Where your proxy sits (`ForwardedHeaders__KnownNetworks` or `__KnownProxies`)
matters just as much, but it can be set in `.env` or on the setup screen, which
shows the address your proxy's requests actually arrive from. Secure cookies are
on by default; leave them on behind a proxy that terminates TLS. Everything else
in the copied file is safe as it is — the development values in it are commented
out or left empty.

Then:

```bash
docker compose -f compose.yaml -f compose.prod.yaml up -d
```

The database starts, the app waits for it to be healthy, applies its migrations
and begins serving on `${CULINA_PORT:-8080}`. Point your proxy at that.

Open it in a browser and the **setup screen** walks you through the rest. First
the database: the compose file fixes where it is, and the screen asks for the
role and password the `db` service created from `.env` — `culina_app` unless you
set `Database__Username`, and your `Database__Password` — with TLS off, which
the database on the compose network does not speak. Then how people reach the
server (secure cookies, which proxy to trust), and the first account. **Whoever finishes setup becomes the administrator** and gets a
household of its own — so do it before the instance is reachable by anyone
else. Whether anyone else may register is then that administrator's decision,
made in the app, not a setting in a file.

Without the compose file — a single `docker run` against a PostgreSQL you
already have — leave out every `Database__*` variable and mount a volume at
`/data/config`. Culina then starts with only the setup screen, asks for the
database first, and saves it there. See `configuration.md` for what it checks.

Everything under **Settings → Server** — cookies, proxies, rate limits,
telemetry, the database — is saved to `/data/config/culina.json` and applied by
a restart of a second or two. A variable passed to the app still wins over it,
and is how you undo a setting that locked you out. The compose file passes the
database's role, password and TLS setting through to nobody but the `db`
service, so those three stay the app's to change.

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

With the proxy on the same host, as here, set `CULINA_PORT=127.0.0.1:8080` in
`.env`: Culina's plain-HTTP port is then published to that machine only, and
nobody on the network can go around the proxy.

## Upgrading

```bash
docker compose -f compose.yaml -f compose.prod.yaml pull
docker compose -f compose.yaml -f compose.prod.yaml up -d
```

The database image in `compose.yaml` is pinned to a digest, so `pull` moves
PostgreSQL only when you take a newer `compose.yaml` from the repository, which
is kept current with PostgreSQL 18's patch releases.

**From before `POSTGRES_SUPERUSER_PASSWORD` was required**, compose refuses to
start until `.env` sets it — and setting it changes nothing in a database that
already exists, whose superuser still has the password it was created with,
usually `postgres`. Put the new one in `.env`, then give it to the database
too:

```bash
docker compose -f compose.yaml -f compose.prod.yaml exec db \
    psql -U postgres -c '\password postgres'
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

Four things, and all four are needed. They are two kinds of thing, and they
must not travel together:

- **The data** — the database and the photographs. The dump holds every recipe,
  every account and password hash, and three kinds of secret, all encrypted:
  the model provider's API key, the push-notification key and the API tokens
  of connected recipe libraries.
- **The secrets** — the key ring, which decrypts those, and
  `culina.json`, which holds the database password in plain text when it was
  entered on the setup screen. Neither file is encrypted on the volume.

Together they are the whole instance; apart, a stolen dump decrypts nothing and
names no password. So archive the secrets separately, encrypt them with a key
of their own — not the one your data backups use — and keep them somewhere
else. The commands below use [age](https://age-encryption.org) with a recipient
kept for secrets only; anything you already encrypt backups with will do.

```bash
# 1. The database.
docker compose -f compose.yaml -f compose.prod.yaml exec -T db \
    pg_dump -U postgres --format=custom culina > culina-$(date +%F).dump

# 2. The photographs. Not in the database, and not rebuildable.
docker run --rm -v culina_culina-images:/data -v "$PWD":/backup alpine \
    tar czf /backup/culina-images-$(date +%F).tar.gz -C /data .

# 3. The key ring, encrypted on its way out, never written in the clear.
docker run --rm -v culina_culina-keys:/data alpine tar czf - -C /data . \
    | age -r "$CULINA_SECRETS_RECIPIENT" > culina-keys-$(date +%F).tar.gz.age

# 4. What was set up in the app, the database password among it. The same.
docker run --rm -v culina_culina-config:/data alpine tar czf - -C /data . \
    | age -r "$CULINA_SECRETS_RECIPIENT" > culina-config-$(date +%F).tar.gz.age
```

Then send 1 and 2 to wherever your backups go, and 3 and 4 somewhere else.

With photos in an S3-compatible bucket (`Storage__S3__*`), step 2 has nothing
to archive. Back up the bucket instead, with the provider's versioning or
replication or with `aws s3 sync` / `mc mirror` to somewhere else.

Volume names are prefixed with your compose project name — `docker volume ls`
shows the real ones.

The key ring is the one that used to be cheap to lose and no longer is. It now
encrypts the model provider's API key, so restoring an instance without it
leaves the assistant switched off with an unreadable key stored — which the app
treats as "no assistant is configured" rather than as an error, and an
administrator enters the key again. It also encrypts the key notifications are
signed with, and without it a finished import no longer notifies anyone. And it
encrypts the API tokens of connected recipe libraries (a Tandoor): without the
key ring each connection still lists, but browsing or importing from it says to
disconnect it and connect it again. Nothing else is lost: Culina's session
cookie carries an opaque reference rather than an encrypted payload, so the key
ring still has nothing to do with who stays signed in.

The key ring is not encrypted on its own volume. Anyone who can read
`/data/keys` on the host can already read the database it protects, so
encrypting it there would only move the question to wherever its key lived;
what matters is that no copy of it ever sits next to a copy of the dump.

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

# The key ring, decrypted on its way in. Without it the assistant's key and the
# mail password have to be entered again, and import notifications stop.
age -d -i culina-secrets.key culina-keys-2026-09-13.tar.gz.age \
    | docker run --rm -i -v culina_culina-keys:/data alpine tar xzf - -C /data

# What was set up in the app. Without it Culina comes back to the setup
# screen, asking for the database's password again.
age -d -i culina-secrets.key culina-config-2026-09-13.tar.gz.age \
    | docker run --rm -i -v culina_culina-config:/data alpine tar xzf - -C /data

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
the backups above, destroys every volume, restores, and then checks what
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
- **The secrets apart from the data.** The key ring and `culina.json` under a
  different key and in a different place from the dump, as above — one
  stolen backup should never be the whole instance.
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
- **Logs.** JSON on stdout, one line per API request: method, path, route,
  status, duration and, when it failed, the error code the client was shown
  (`auth.invalid_credentials`, `recipes.not_found`). Every line carries
  `TraceId`, which is the request id the app shows to whoever hit the error
  (ask them for it), plus the span, the client address and the user. A share
  token or invitation code in a path is logged, and traced, as `***`
  (`/api/v1/shared-recipes/***/image`): read from a log, either would open
  what it was made for. For the same reason the web server's own per-request
  scope (`RequestPath`, `RequestId`) is switched off.
  The first line after a start names the version, the database, the image path
  and where telemetry goes. Set `Logging__LogLevel__Default=Debug` to see each import
  recipe, each fetch hop and each assistant call as it is asked for.
- **Event ids worth alerting on.** 1001 refused sign-in, 1002 refused password
  confirmation, 1003 refused recovery code, and 1801 for a request rejected by
  a rate limit, the CSRF check or the origin check, with the reason. Each line
  carries `ClientAddress` and never the email address that was tried, so
  fail2ban or CrowdSec can ban on it. 1501 is an AI provider refusing a call,
  with the provider's own reason attached; 1502 is every settled assistant
  call with its tokens, cost and duration. Exported, the id is the log
  attribute `logrecord.event.id`. Ready-made rules for SigNoz are under
  [Alerts](#alerts).
- **The database pool running dry, before it does.** 1970 is a request that
  waited longer than half a second for a database connection; 1971 is a
  connection kept out of the pool for longer than five seconds, naming the use
  case that kept it (`Recipes.Import`). Each is logged at most once a minute,
  with a count of the ones in between, so a busy instance cannot flood its own
  log. With a collector, Npgsql's metrics show the pool itself:
  `db.client.connection.count` by state against `db.client.connection.max`,
  and `db.client.connection.npgsql.pending_requests` for requests waiting.
- **Mail that does not go out.** 1801 is a mail the server or network refused,
  to be tried again (a warning, with what the server said); 1802 is one given up
  on after its eighth attempt, about a day later (an error); 1803 is one that
  could not be decrypted because the key ring changed, and was dropped. A burst
  of 1801 right after changing Settings → Mail is a wrong password or port;
  **Send a test mail** on that screen says the same thing immediately.
- **Telemetry.** Set a collector under Settings → Server, or
  `OTEL_EXPORTER_OTLP_ENDPOINT`, and traces, metrics and logs all export. Unset,
  nothing leaves the machine.
- **What broke in somebody's browser.** The web app sends its uncaught errors,
  failed renders, service-worker failures and content-security-policy
  violations to the server, which logs them under the category
  `Culina.WebApp.Untrusted`, event id 1700 (`UntrustedWebAppReport`), always at
  `Warning` and with every field named `Client…` — anybody can send one, so
  none of it is the server's word, and none of it pages anyone as an `Error`.
  Line breaks and other control characters in what a browser sent are logged
  as spaces. They reach stdout with everything else,
  and the collector as a service of their own, `culina-web`, beside the
  server's `culina-api` (both follow `OTEL_SERVICE_NAME`; see
  configuration.md), carrying the same request id, user and trace.
  `culina-web` has no `service.version`, as the server's would be wrong for a tab still on an
  older build; filter on each record's `culina.web.app_version` instead. Nothing
  goes from a browser to the collector directly, and nothing new needs
  configuring. Each line names the build, the browser and the route
  (`/recipes/[recipeId]`, never the address with the id in it); a stack, when
  there is one, is the exported exception. Background work — migrations, the
  session sweep, the search reindex and each library import — is a trace of
  its own.
- **`Cannot load library libgssapi_krb5.so.2` at boot is expected.** Npgsql
  probes for Kerberos and the chiseled runtime ships none. Culina authenticates
  to PostgreSQL with a password, so nothing needs it. Two lines at startup and
  never again; adding six system libraries to the image to silence them is not a
  trade worth making.

## Alerts

The rules under `deploy/observability/signoz/` turn what the section above says
to look for into alerts in [SigNoz](https://signoz.io), which evaluates traces,
metrics and logs in one place, so you hear about a problem before the people
cooking do. Jaeger, when the collector mirrors traces to it, stays a
trace viewer: it has no alerting, and every trace an alert links to in SigNoz
is the same trace in Jaeger, by its `TraceId`.

**Two tiers.** Most rules have a `warning` and a `critical` threshold; the
critical one silences the warning while both hold. `routing.json` decides who
hears what:

| Tier | Goes to | Meaning |
| --- | --- | --- |
| `critical` | push on ntfy topic `culina-alerts` (urgent), Telegram, email | Users are affected now, or will be within the hour |
| `warning` | email | Worth a look today |

Email goes through ntfy too: the channel posts to the topic `culina-mail`, which
nobody subscribes to, with `email=` set, so it only ever arrives as mail. SigNoz
then needs no mail server of its own; your ntfy server does (its `smtp-sender-*`
settings).

**What is covered.**

| Area | Rules |
| --- | --- |
| Availability | no telemetry from the API for 10 minutes; share of failed requests |
| Performance | API p95 latency (event streams excluded: they stay open while watched); p95 per use case (imports, AI calls and archives excluded: slow by design) |
| Security | refused sign-ins, recovery codes, CSRF, origin and rate-limit rejections |
| Resources | memory against the container limit; storage per directory; thread pool backlog |
| Database | pool in use, requests waiting, timeouts, a connection held too long (1971) |
| Errors and jobs | error and critical lines; failed imports, session sweeps and trash purges |
| Assistant | an AI provider refusing calls (1501) |
| Web app | bursts of browser reports (1700), as a warning only: anybody can send one |
| Configuration | started without a database (1600) or with insecure cookies (1603) |

Thresholds are starting points for a household, not a fleet. Each is a number in
its file; change it there and apply again rather than in the SigNoz UI, which the
next apply would overwrite.

**Dashboards.** The same apply keeps five dashboards in `dashboards/`, each a
file: *Culina · API* (traffic, failures, latency per route and use case, outbound
calls), *Database* (pool, waits, timeouts, query duration, long holds),
*Security* (refusals over time, by reason and by client address), *Resources*
(memory, storage, CPU, .NET heap, GC, thread pool, connections) and *Logs and
jobs* (lines by level and event, background work, the assistant, browser
reports). A panel with an alert behind it draws the alert's thresholds. Edit
the file rather than the dashboard in SigNoz: apply replaces the whole
dashboard, and leaves alone only one made by hand or locked. Panels about events
stay empty until the event first happens.

**Setting it up.**

1. **ntfy.** The server's address, reachable from the SigNoz container, and an
   access token if its topics are protected (one token for both topics).
   Subscribe your phone to `culina-alerts`, not to `culina-mail`. The channels
   ask ntfy (2.14 or later) to format each message with its built-in
   `alertmanager` template. The topic names are in `channels/ntfy*.json`.
2. **Telegram.** SigNoz has no Telegram channel, so a relay turns its webhook into
   bot messages. [prometheus_bot](https://github.com/inCaller/prometheus_bot) is
   a small one: run `ghcr.io/incaller/prometheus_bot` beside SigNoz, pinned by
   digest, with a `config.yaml` holding the bot token, and point the channel at
   `http://prometheus-bot:9087/alert/<chat id>`. Leave it out and the critical
   tier goes to ntfy and email only.
3. **An API key.** SigNoz → Settings → Service Accounts: an account with the
   Admin role (creating notification channels is admin-only) and a key for it.
4. **Apply.**

   ```bash
   export SIGNOZ_URL=http://signoz.home.arpa:7890 SIGNOZ_API_KEY=…
   export CULINA_ALERT_NTFY_URL=https://ntfy.example.com CULINA_ALERT_NTFY_TOKEN=…
   export CULINA_ALERT_EMAIL_TO=you@example.com
   export CULINA_ALERT_TELEGRAM_RELAY_URL=http://prometheus-bot:9087/alert/123456
   scripts/signoz-alerts.sh plan    # what would change
   make alerts-apply                # change it
   ```

   A channel is skipped while a variable it uses is unset, and rules route to the
   ones that are set. SigNoz refuses a tier that routes nowhere, so without
   `CULINA_ALERT_EMAIL_TO` the warning tier is left out, and a rule with only a
   warning is skipped; apply says which. Running it again updates in place. Rules it made carry
   `managed_by=culina-repo`; one of those without a file any more is deleted, and
   a rule made by hand is never touched, even when its name matches.
   `CULINA_API_SERVICE` and `CULINA_WEB_SERVICE` follow `OTEL_SERVICE_NAME` when
   that is not `culina-api`.
5. **Try it.** *Test* on each channel's page in SigNoz sends a message through
   it; *Test notification* on a rule's page sends that rule's alert through its
   channels. Wrong passwords alone are a poor test: past five a minute for one
   account the rate limit refuses them first, and those count as rate-limit
   rejections, not refused sign-ins.

The log rules need the event id exported, which Culina does by default
(`OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EVENT_LOG_ATTRIBUTES`, configuration.md).
`make alerts-check` validates the files without a network; run it after editing
one.

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
| The app exits at boot naming `Cookies__AllowInsecureOutsideDevelopment` | Secure cookies were turned off, in `.env` or in an older `culina.json`. Reach Culina through your TLS proxy and unset `Cookies__Secure`; only an instance on plain `http://` in a network you trust should set the variable it names instead. |
| Requests slow down together, or fail with `The connection pool has been exhausted` | Look for 1971 first: it names what kept connections out of the pool. Only 1970 on its own means the load outgrew the pool; raise `Database__MaxPoolSize`, keeping every instance's pools together under PostgreSQL's `max_connections`. |
| Rate limits trigger for everyone at once | `ForwardedHeaders__KnownProxies`/`KnownNetworks` is not set, so every request looks like it comes from the proxy. |
| Photographs vanished after a deploy | `/data/images` was not a volume. There is no recovering them without a backup. |
