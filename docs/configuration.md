# Configuration

Culina reads its settings once, at startup, from three places, each overriding
the one before it:

1. **The defaults** shipped in `appsettings.json`.
2. **`culina.json` in `Storage__ConfigPath`** (`/data/config` in the image) —
   what an administrator saved from the app: the setup screen on first start,
   and **Settings → Server** afterwards. The app writes this file; you do not
   have to.
3. **The environment** (and the command line). A variable set here always
   wins, and the settings screen shows that setting as fixed, naming the
   variable, instead of offering an edit that would do nothing.

Saving a change on the settings screen writes it to `culina.json` and
**restarts Culina in place**: the host is built again from the new
configuration, which takes a second or two, and every value goes through the
same startup validation as before. Nothing is applied half-way.

Everything an administrator changes while the app runs *without* a restart —
who may register, the assistant — lives in the database instead, and is not
configuration at all.

**The process refuses to start on invalid configuration.** Every group below is
validated at startup, so a misconfigured deployment fails immediately and says
which value is wrong — rather than on the first request that happened to need
it, three days later. The settings screen asks the same validation before it
saves, so what it saves is something the next start accepts.

Names use the .NET convention: a double underscore separates the section from
the key, so `Database__Host` is `Host` in the `Database` section. The table
column **In the app** marks what the settings screen can change.

## First start

With no database configured anywhere — no `Database__*` variables, nothing in
`culina.json` — Culina starts a small **setup host** instead of the app. It
serves the setup screen and nothing else: every other API route answers `503`
with `settings.setup_required`, `/health/ready` answers `200` so a proxy routes
to it, and the log says so as a warning. The screen asks for:

1. **The database.** Culina connects before saving anything, and refuses a
   database it could not run in: a wrong password, a host that does not
   resolve, a role that may not install the `citext`, `pg_trgm` and `unaccent`
   extensions (trusted extensions, which the first migration installs as the
   application role given `CREATE` on the database), or a role that may not
   create tables — each with the reason and, where there is one, the SQL a
   superuser has to run. Then it restarts into the real app.
2. **How people reach it**: secure cookies (defaulted from whether the browser
   is on `https://`) and which proxy to trust (it shows the address requests
   actually arrive from). Everything else is folded away with its defaults.
3. **The first account**, which administers the instance.

If the database is configured through the environment — the production compose
file does this — the first step is skipped. **Whoever finishes setup first
becomes the administrator**, exactly as the first registration always did, so
finish it before the instance is reachable by anyone else.

## Undoing a setting that locked you out

A setting that stops Culina starting is named in its log. Set that setting's
variable in the environment — it overrides the file — or delete the key from
`culina.json` (or the whole file) and restart the container. The classic case
is turning secure cookies on while Culina is reached over plain `http://`:
nobody can sign in, and `Cookies__Secure=false` gets you back in.

## Database

| Variable | Default | In the app | |
| --- | --- | --- | --- |
| `Database__Host` | — | yes | **Required**, here or on the setup screen. Host name or address. |
| `Database__Port` | `5432` | yes | |
| `Database__Name` | — | yes | **Required**, here or on the setup screen. |
| `Database__Username` | — | yes | **Required**, here or on the setup screen. The application role. Never a superuser — the migrations do not need one, and a compromised app should not be able to drop the cluster. |
| `Database__Password` | — | yes | **Required**, here or on the setup screen. Never logged, never echoed by an endpoint, never in a problem document. Entered in the app, it is stored in `culina.json`, which only the app's own user may read. |
| `Database__RequireSsl` | `true` | yes | Set to `false` only when the database is on the same private network and nothing else is. |
| `Database__MaxPoolSize` | `20` | yes | |

Configured as parts rather than one connection string so each part can be
validated and the password can come from a different place — a Docker secret,
say — than the rest.

Changing the database in the app connects to the new one first. Culina copies
nothing between databases: pointing it at an empty one starts an empty
instance, and the setup screen with it.

## Storage

| Variable | Default | |
| --- | --- | --- |
| `Storage__ImagePath` | `/data/images` in the image | **Must be a volume.** Recipe photographs, re-encoded. Not in the database and not rebuildable: without a volume, every photo in the instance disappears on the next deploy and nothing says so. |
| `Storage__DataProtectionKeyPath` | `/data/keys` in the image | **Must be a volume.** The ASP.NET data-protection key ring. |
| `Storage__MaxImageBytes` | `10485760` | 10 MB. |
| `Storage__ConfigPath` | `/data/config` in the image | **Should be a volume.** Where `culina.json` — everything saved from the setup and settings screens — lives. Not required to be writable: without it Culina still starts, and the settings screen shows the settings but says it cannot save them. |

A note on the key ring, because the usual warning does not apply: Culina's
session cookie carries an opaque reference, not an encrypted payload, so the
session row is what authenticates a request and losing the keys does not sign
anyone out. The volume is still configured, because anything the framework
protects later would otherwise change key on every restart.

## Cookies

| Variable | Default | |
| --- | --- | --- |
| `Cookies__Secure` | `true` | In the app. Only plain-HTTP access justifies `false`. With it true the session cookie takes the `__Host-` prefix, which requires HTTPS — over plain `http://` the browser refuses it and signing in cannot work. Changing it signs everybody out once, because the cookie changes name. |
| `Cookies__SessionDays` | `30` | In the app. How long a session survives without activity. Activity slides it: a device in regular use is never signed out. |
| `Cookies__RenewAfterHours` | `24` | In the app. How long a session may sit unused before the next request extends it and re-issues both cookies. Culina has no refresh token — the cookie is an opaque reference, so this renewal is what takes its place. Lower costs a write per request for nothing; `0` renews on every request and only a test wants that. |

## Behind a reverse proxy

| Variable | Default | |
| --- | --- | --- |
| `ForwardedHeaders__KnownProxies` | empty | In the app. Comma-separated addresses. |
| `ForwardedHeaders__KnownNetworks` | empty | In the app. Comma-separated CIDR ranges, for a proxy whose address is not knowable in advance — anything in a container network. |

**One of these is required in production.** Without it the app sees the proxy's
address as every client's, which makes per-IP rate limiting protect nothing and
every security log line name the wrong host. Trusting everything is worse: then
any client can forge its own address. Keep the range as small as it can be. The
settings screen shows the address requests arrive from, and whether Culina
already trusts it, which is usually the whole answer.

## Password hashing

Argon2id. Raise `MemoryKib` as far as the host tolerates; existing hashes keep
verifying and are upgraded transparently on the next successful sign-in.

| Variable | Default | |
| --- | --- | --- |
| `PasswordHashing__MemoryKib` | `65536` | 64 MB per hash. |
| `PasswordHashing__Iterations` | `3` | |
| `PasswordHashing__Parallelism` | `2` | |

## Rate limits

All of these are in the app.

| Variable | Default | |
| --- | --- | --- |
| `RateLimits__LoginPerIpPerMinute` | `10` | |
| `RateLimits__LoginPerAccountPerMinute` | `5` | |
| `RateLimits__RegisterPerIpPerHour` | `5` | |
| `RateLimits__InvitationPerIpPerHour` | `10` | |
| `RateLimits__SharedRecipesPerIpPerMinute` | `120` | Reads of recipes shared behind a link. |
| `RateLimits__ImportsPerHour` | `30` | Imports from a web page, per person. |
| `RateLimits__SourceRequestsPerHour` | `1500` | Requests against a connected recipe library. |
| `RateLimits__AssistantRequestsPerHour` | `60` | The only limit here about money rather than load. |
| `RateLimits__RequestsPerSessionPerMinute` | `600` | |

Login is limited per address **and** per account: per-address alone lets a
botnet spread an attack on one account across many addresses, and per-account
alone lets one address walk a password list across many accounts.

## The assistant

Nothing. There is no environment variable for it, and that is deliberate: the
model, the key, the budget and which capabilities are on are all things an
administrator changes while the app runs, from **Settings → Assistant**. A key
that could only be entered by editing a file on the server is a key nobody will
ever rotate.

Connecting one is choosing a provider and pasting a key. The address and the
model names live under **Advanced** and are empty by default — Culina knows
where Google and OpenAI are, and which of their models to use. Set the address
only if you run a gateway in front of the provider; set a model only if you want
to pin one rather than follow whatever Culina currently defaults to. Ollama is
the exception and is asked for its address up front, because a model on your own
machine is wherever you put it and there is no default that could be right.

**Several providers at once.** Connect as many as you like, then give each job
to one of them. The four jobs — improving a recipe, writing one from an idea,
reading one out of a photograph, drawing a picture — do not want the same model:
reading a cookbook page wants good vision, tidying wording wants something cheap
that will be asked twenty times an evening, and drawing wants a provider that
draws at all, which a model on your own hardware does not. A job with no
provider is not offered, and its button does not appear.

One connection per provider, not per name. Two Ollama boxes, or a direct OpenAI
key alongside a gateway, would need connections to be named and managed; nobody
has asked for that yet.

Two things about it belong in a deployment decision rather than a screen, so
they are said here instead.

**What leaves the server.** With Gemini or OpenAI connected, the text of the
recipe being worked on, the idea somebody typed, and any photograph they point
it at are sent to that company to be read. Nothing else is: not your other
recipes, not your shopping list, not who cooked what. Nothing is sent at all
until an administrator connects a key and switches a capability on, and an
instance with none behaves exactly as Culina did before the assistant existed.

With **Ollama** connected, nothing leaves the machine you point it at. That is
the reason it is supported and the reason it needs no key — a self-hosted recipe
app talking to a model already running in the same house is the arrangement this
feature is most obviously for. It does not draw pictures.

**What it costs.** Both hosted providers bill per token, so the settings screen
tracks tokens and spend per person and per capability, and refuses new requests
once the monthly ceiling you set is reached. The ceiling is in whatever currency
your provider bills in; Culina does not convert it, because a number it converted
would drift from the invoice it exists to predict. A model Culina has no price
for records its tokens and no cost rather than a guess.

The key is encrypted with the data-protection key ring before it is stored — see
`Storage__DataProtectionKeyPath` above, and `operations.md` for what losing that
directory now means.

## Telemetry

| Variable | Default | |
| --- | --- | --- |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | unset | In the app. Unset means JSON logs on stdout and nothing exported. |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | `grpc` | In the app. `grpc` (usually port 4317) or `http/protobuf` (usually 4318). The wrong one exports nothing and says nothing. |
| `OTEL_SERVICE_NAME` | `culina-api` | |

The standard OpenTelemetry names, because Culina has no business inventing its
own configuration vocabulary for something that already has one. The SDK reads
them through the app's configuration rather than straight from the process
environment, so an endpoint saved in the app works exactly like the variable —
and in `culina.json` they sit at the top level, under their own names. Anything
else the exporter understands (headers for a hosted collector, per-signal
endpoints) is set as a variable, as before.

## Build-time, for the frontend

These affect what is produced, not what runs, so they matter only if you build
the image yourself.

| Variable | Effect when unset |
| --- | --- |
| `PUBLIC_SITE_URL` | No sitemap and no security.txt. An instance on a private network has no public address and should not invent one. |
| `PUBLIC_SECURITY_CONTACT` | No security.txt. A contact nobody reads is worse than none: it tells a finder they have reported something when they have not. |
| `VITE_GALLERY` | The design-system gallery is not built in. A release must leave this unset; CI asserts that what ships contains none of it. |

## What is not here

Registration policy — whether the instance accepts new accounts, whether an
invitation is required, and how many accounts it allows — is not configuration.
It is a decision an administrator makes while the instance is running, so it
lives in the database and is changed through the API. An instance that needs a
restart to stop accepting new accounts is an instance that stays open for the
length of the restart.
