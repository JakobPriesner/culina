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
same startup validation as before. Nothing is applied half-way. The file is
written readable by the app's own user only, from its first byte, because it
can hold the database password. If you edited it by hand and it no longer
reads as a JSON object, saving is refused (`settings.file_unreadable`) and the
file is left exactly as it is — correct or remove it, then save again.

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
   database it could not run in: nothing answering at the address, a refused
   user, password or database name, a TLS requirement the server cannot meet,
   a superuser role, a role that may not install the `citext`, `pg_trgm` and
   `unaccent` extensions (trusted extensions, which the first migration
   installs as the application role given `CREATE` on the database), or a role
   that may not create tables — the last two with the SQL a superuser has to
   run. A connection that fails is only named by its kind: the server's own
   message, with the address it tried, is in Culina's log as a warning
   (event 1960), because anybody may ask for this check during setup and the
   exact message would tell them what listens on any address. For the same
   reason one address may try at most ten times a minute — a fixed limit, not
   one of the `RateLimits__*` settings, which are part of what is being set up
   — here and in Settings → Server alike (`429 request.rate_limited`).
   Then it restarts into the real app.
2. **How people reach it**: secure cookies (on whenever the browser would keep
   a Secure cookie — on `https://`, and on `http://localhost`) and which proxy
   to trust (it shows the address requests actually arrive from). Everything
   else is folded away with its defaults.
3. **The first account**, which administers the instance.

If the database is configured through the environment, the first step is
skipped. The production compose file sets only where the database is — host,
port and name — so the step still asks for the role, the password and TLS, and
they stay editable under Settings → Server afterwards. **Whoever finishes setup
first becomes the administrator**, exactly as the first registration always
did, so finish it before the instance is reachable by anyone else.

## Undoing a setting that locked you out

A setting that stops Culina starting is named in its log. Set that setting's
variable in the environment — it overrides the file — or delete the key from
`culina.json` (or the whole file) and restart the container. The classic case
is turning secure cookies on while Culina is reached over plain `http://`:
nobody can sign in, and `Cookies__Secure=false` with
`Cookies__AllowInsecureOutsideDevelopment=true` gets you back in.

## Database

| Variable | Default | In the app | |
| --- | --- | --- | --- |
| `Database__Host` | — | yes | **Required**, here or on the setup screen. Host name or address. |
| `Database__Port` | `5432` | yes | |
| `Database__Name` | — | yes | **Required**, here or on the setup screen. |
| `Database__Username` | — | yes | **Required**, here or on the setup screen. The application role. Never a superuser — the migrations do not need one, and a compromised app should not be able to drop the cluster. The setup and settings screens refuse a superuser. |
| `Database__Password` | — | yes | **Required**, here or on the setup screen. Never logged, never echoed by an endpoint, never in a problem document. Entered in the app, it is stored in `culina.json`, which only the app's own user may read. |
| `Database__RequireSsl` | `true` | yes | Set to `false` only when the database is on the same private network and nothing else is. |
| `Database__MaxPoolSize` | `20` | yes | |

Configured as parts rather than one connection string so each part can be
validated and the password can come from a different place — a Docker secret,
say — than the rest.

Changing the database in the app connects to the new one first. Culina copies
nothing between databases: pointing it at an empty one starts an empty
instance, and the setup screen with it. Leaving the password box empty keeps
the stored password only while the host, port, database name and user stay
the same; change any of them and the password has to be typed again, because
the stored one is only ever sent to the server it was saved for. A value the
environment pins is tried as pinned, whatever the form sends.

**Culina signs in with SCRAM-SHA-256 only.** It refuses a server that asks for
the password in clear text or as MD5, or for no password at all (`trust`) —
`settings.database_insecure_auth` on the settings screen, a failed start
otherwise — so a server that merely pretends to be PostgreSQL is never handed
the password. PostgreSQL has defaulted to SCRAM
since version 14, and the bundled `postgres:18` image uses it. A cluster
upgraded from an older version may still hold an MD5 password for the role:
run `SET password_encryption = 'scram-sha-256';` and set the role's password
again (`ALTER ROLE culina_app PASSWORD '…';`), and make sure `pg_hba.conf`
says `scram-sha-256` rather than `md5` or `password`.

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
| `Cookies__Secure` | `true` | In the app. Only plain-HTTP access justifies `false`. With it true the session cookie takes the `__Host-` prefix, which requires HTTPS — over plain `http://` the browser refuses it and signing in cannot work (`localhost` is the exception browsers make). Changing it signs everybody out once, because the cookie changes name. |
| `Cookies__AllowInsecureOutsideDevelopment` | `false` | Not in the app. Outside the `Development` environment Culina refuses to start with `Cookies__Secure=false`, and refuses to save it from the app, unless this is `true` — a session cookie in cleartext, without its `__Host-` prefix, is the deployment's risk to accept, not an administrator's switch. Every start with insecure cookies logs a warning (event id 1603). |
| `Cookies__SessionDays` | `30` | In the app. How long a session survives without activity. Activity slides it, up to `Cookies__MaxSessionDays`. |
| `Cookies__RenewAfterHours` | `24` | In the app. How long a session may sit unused before the next request extends it and re-issues both cookies. Culina has no refresh token — the cookie is an opaque reference, so this renewal is what takes its place. Lower costs a write per request for nothing; `0` renews on every request and only a test wants that. |
| `Cookies__MaxSessionDays` | `90` | In the app. How long a session may last at all, counted from signing in, however often it is used; then a fresh sign-in is needed. Without a ceiling a stolen cookie that keeps being used never expires. At least `Cookies__SessionDays`; unset, it is 90 or `Cookies__SessionDays` if that is longer. |

Signing in again from a browser that still holds a session ends that
session: the new cookie replaces the old one, so the old row would only ever
be used by somebody who copied it.

## Behind a reverse proxy

| Variable | Default | |
| --- | --- | --- |
| `ForwardedHeaders__KnownProxies` | empty | In the app. Comma-separated addresses. |
| `ForwardedHeaders__KnownNetworks` | empty | In the app. Comma-separated CIDR ranges, for a proxy whose address is not knowable in advance — anything in a container network. Nothing wider than a `/8` (IPv4) or a `/32` (IPv6) is accepted; `172.16.0.0/12` covers every Docker bridge network. |
| `ForwardedHeaders__DangerouslyTrustWideNetworks` | `false` | Not on the settings screen: set it where the deployment is configured. `true` accepts a `KnownNetworks` entry wider than that, `0.0.0.0/0` included — every client inside it can then claim any address. Almost never what you want. |

**One of these is required in production.** Without it the app sees the proxy's
address as every client's, which makes per-IP rate limiting protect nothing and
every security log line name the wrong host. Trusting everything is worse: then
any client can forge its own address. Keep the range as small as it can be. The
settings screen shows the address requests arrive from, and whether Culina
already trusts it, which is usually the whole answer.

## Password hashing

Argon2id. Raise `MemoryKib` as far as the host tolerates; existing hashes keep
verifying and are upgraded transparently on the next successful sign-in. At
most one hash per processor core runs at a time — every sign-in hashes, an
unknown address included — so the memory a burst of sign-ins can claim is
`MemoryKib` times the core count, and further sign-ins wait their turn.

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
| `RateLimits__InvitationPerIpPerHour` | `10` | Redemptions per address; reading which household a code is for counts separately against the same number. |
| `RateLimits__SharedRecipesPerIpPerMinute` | `120` | Reads of recipes shared behind a link. |
| `RateLimits__ImportsPerHour` | `30` | Imports from a web page, per person. |
| `RateLimits__SourceRequestsPerHour` | `1500` | Requests against a connected recipe library, per person. |
| `RateLimits__AssistantRequestsPerHour` | `60` | Per person. The only limit here about money rather than load. |
| `RateLimits__ArchiveExportsPerHour` | `5` | Archives taken, per person: each one streams every photograph in the household. |
| `RateLimits__RequestsPerSessionPerMinute` | `600` | Every request, per address despite the name. |

Login is limited per address **and** per account: per-address alone lets a
botnet spread an attack on one account across many addresses, and per-account
alone lets one address walk a password list across many accounts. An attempt
is counted before the password is checked, so simultaneous guesses cannot slip
past the limit. An address the account has signed in from in the last 30 days
gets a per-account budget of its own, so somebody guessing from elsewhere
cannot lock the owner out at home; every other address shares the account's.

The limits "per person" count against the signed-in account, whichever device,
session or address it uses, so signing in again does not buy a fresh
allowance.

## Importing

| Variable | Default | |
| --- | --- | --- |
| `Import__AllowPrivateSourceAddresses` | `false` | In the app. Lets a connected recipe library (a Tandoor) live on a private network: 10/8, 172.16/12, 192.168/16, 100.64/10 and IPv6 unique local addresses. Never loopback, link-local, cloud metadata, `0.0.0.0` or multicast, so a Tandoor on the same machine has to be addressed by its LAN or container-network name. Never applies to importing from a pasted link. |

Everything the server fetches for someone — a pasted link, a connected
library, a push notification — connects directly and ignores `HTTP_PROXY` and
`HTTPS_PROXY`: through a proxy, the address checked would be the proxy's, not
the one it goes on to reach. A connected library on a private address gets only
"could not be read" back when something fails, even for a wrong token, so the
error cannot be used to map the network.

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

## Site

What the instance says about itself to strangers. Both optional, neither in the
app.

| Variable | Default | |
| --- | --- | --- |
| `Site__SecurityContact` | unset | Where a vulnerability report about this instance should go — a `mailto:`, `https://` or `tel:` address you actually read. Set, `/.well-known/security.txt` (RFC 9116) names it; unset, that path is a `404`, because a contact nobody reads is worse than none. |
| `Site__Url` | unset | This instance's public address, such as `https://culina.example.com`. Gives security.txt its `Canonical:` line. |

## Telemetry

| Variable | Default | |
| --- | --- | --- |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | unset | In the app. Unset means JSON logs on stdout and nothing exported. |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | `grpc` | In the app. `grpc` (usually port 4317) or `http/protobuf` (usually 4318). The wrong one exports nothing and says nothing. |
| `OTEL_SERVICE_NAME` | `culina-api` | Has no effect at present: the app names its services itself, `culina-api` for the server and `culina-web` for what the browser reported. |

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
| `PUBLIC_SITE_URL` | No sitemap. An instance on a private network has no public address and should not invent one. security.txt is not built at all: the server writes it from `Site__SecurityContact`, above. |
| `VITE_GALLERY` | The design-system gallery is not built in. A release must leave this unset; CI asserts that what ships contains none of it. |

## What is not here

Registration policy — whether the instance accepts new accounts, whether an
invitation is required, and how many accounts it allows — is not configuration.
It is a decision an administrator makes while the instance is running, so it
lives in the database and is changed through the API. An instance that needs a
restart to stop accepting new accounts is an instance that stays open for the
length of the restart.
