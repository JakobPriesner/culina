# Configuration

Everything Culina needs before it can reach its database comes from the
environment. Everything an administrator can change while it runs lives in the
database and is edited in the app.

**The process refuses to start on invalid configuration.** Every group below is
validated at startup, so a misconfigured deployment fails immediately and says
which value is wrong — rather than on the first request that happened to need
it, three days later.

Names use the .NET convention: a double underscore separates the section from
the key, so `Database__Host` is `Host` in the `Database` section.

## Database

| Variable | Default | |
| --- | --- | --- |
| `Database__Host` | — | **Required.** Host name or address. |
| `Database__Port` | — | **Required.** |
| `Database__Name` | — | **Required.** |
| `Database__Username` | — | **Required.** The application role. Never a superuser — the migrations do not need one, and a compromised app should not be able to drop the cluster. |
| `Database__Password` | — | **Required.** Never logged, never echoed by an endpoint, never in a problem document. |
| `Database__RequireSsl` | `true` | Set to `false` only when the database is on the same private network and nothing else is. |
| `Database__MaxPoolSize` | `20` | |

Configured as parts rather than one connection string so each part can be
validated and the password can come from a different place — a Docker secret,
say — than the rest.

## Storage

| Variable | Default | |
| --- | --- | --- |
| `Storage__ImagePath` | `/data/images` in the image | **Must be a volume.** Recipe photographs, re-encoded. Not in the database and not rebuildable: without a volume, every photo in the instance disappears on the next deploy and nothing says so. |
| `Storage__DataProtectionKeyPath` | `/data/keys` in the image | **Must be a volume.** The ASP.NET data-protection key ring. |
| `Storage__MaxImageBytes` | `10485760` | 10 MB. |

A note on the key ring, because the usual warning does not apply: Culina's
session cookie carries an opaque reference, not an encrypted payload, so the
session row is what authenticates a request and losing the keys does not sign
anyone out. The volume is still configured, because anything the framework
protects later would otherwise change key on every restart.

## Cookies

| Variable | Default | |
| --- | --- | --- |
| `Cookies__Secure` | `true` | Only local HTTP development justifies `false`. With it true the session cookie takes the `__Host-` prefix, which requires HTTPS — over plain `http://` the browser refuses it and signing in cannot work. |
| `Cookies__SessionDays` | `30` | How long a session survives without activity. Activity slides it: a device in regular use is never signed out. |
| `Cookies__RenewAfterHours` | `24` | How long a session may sit unused before the next request extends it and re-issues both cookies. Culina has no refresh token — the cookie is an opaque reference, so this renewal is what takes its place. Lower costs a write per request for nothing; `0` renews on every request and only a test wants that. |

## Behind a reverse proxy

| Variable | Default | |
| --- | --- | --- |
| `ForwardedHeaders__KnownProxies` | empty | Comma-separated addresses. |
| `ForwardedHeaders__KnownNetworks` | empty | Comma-separated CIDR ranges, for a proxy whose address is not knowable in advance — anything in a container network. |

**One of these is required in production.** Without it the app sees the proxy's
address as every client's, which makes per-IP rate limiting protect nothing and
every security log line name the wrong host. Trusting everything is worse: then
any client can forge its own address. Keep the range as small as it can be.

## Password hashing

Argon2id. Raise `MemoryKib` as far as the host tolerates; existing hashes keep
verifying and are upgraded transparently on the next successful sign-in.

| Variable | Default | |
| --- | --- | --- |
| `PasswordHashing__MemoryKib` | `65536` | 64 MB per hash. |
| `PasswordHashing__Iterations` | `3` | |
| `PasswordHashing__Parallelism` | `2` | |

## Rate limits

| Variable | Default | |
| --- | --- | --- |
| `RateLimits__LoginPerIpPerMinute` | `10` | |
| `RateLimits__LoginPerAccountPerMinute` | `5` | |
| `RateLimits__RegisterPerIpPerHour` | `5` | |
| `RateLimits__InvitationPerIpPerHour` | `10` | |
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
| `OTEL_EXPORTER_OTLP_ENDPOINT` | unset | Unset means JSON logs on stdout and nothing exported. |
| `OTEL_SERVICE_NAME` | `culina-api` | |

The standard OpenTelemetry names, because Culina has no business inventing its
own configuration vocabulary for something that already has one.

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
