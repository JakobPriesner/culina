# Culina

A self-hosted recipe app for the people you actually cook with.

![A recipe in Culina: the photograph, the ingredient list, and steps whose
amounts come from that list](docs/images/recipe.webp)

Culina is built around one observation: a person using a recipe app is never in
a single state. They move through **finding → deciding → shopping → cooking →
remembering**, and almost every recipe app treats those as separate screens.
All the pain lives in the transitions between them.

## Three commitments

**1. One recipe surface. Cooking is a change in emphasis, not a change of
screen.** Reading and cooking are the same component at two weightings. The
ingredient list and the servings control keep their screen position; the step
you were reading grows in place. You never have to re-find anything.

**2. Steps know which ingredients they use.** A step references its
ingredients, so changing the servings changes the amounts *inside the step
text* — "Melt **180 g butter** in the pan", not "melt the butter" with the
number 400 px away. Scaling is honest instead of cosmetic, and the app rounds
the way a cook writes: `4–5 cloves`, never `4.5`.

**3. The app never loses your place.** Servings are remembered per recipe.
Leave mid-cook and come back to the same step with your timers still running —
they count from a deadline, not from a page that has to stay open, so ten
minutes in a pocket is ten minutes gone. They do not ring while the app is
closed, and nothing in the app pretends otherwise. A slim bar anywhere brings
you straight back.

## What it deliberately does not do

No pantry inventory (nobody maintains one, so it goes stale and poisons
everything built on it). No calorie estimates (a wrong number is worse than
none): nutrition is a lookup in the German food composition table, cited on the
page, and when it cannot count a line it says "at least" and names what it left
out, rather than guessing what an onion weighs. No social feed, ratings or
comments — this is your kitchen, not a network.

## The assistant

Culina can connect to a language model, and then it will do four things: tidy
the wording and structure of a recipe you wrote, turn an idea into a draft,
read a recipe out of a photograph of a cookbook page, and draw a picture for a
recipe that has none. Nothing it produces is saved until you have read it beside
what was there before and accepted it, field by field.

It is off until an administrator connects one, and an instance with nothing
connected is the app exactly as it was before any of this existed — the buttons
are absent, not greyed out. Culina has to work completely without it, and it
does.

Three providers — **Ollama** for a model on your own hardware, **Gemini** and
**OpenAI** for a hosted one — and you can connect all of them and give each job
to whichever suits it. Reading a cookbook photograph wants good vision; tidying
wording wants something cheap you will ask twenty times an evening; drawing
wants a provider that draws at all, which a local model does not.

A job given to Ollama costs nothing per recipe and sends nothing off the
machine. A job given to Gemini or OpenAI sends that recipe's text, or the
photograph you pointed it at, to that company to be read — and the settings
screen tracks tokens and spend per person, and refuses requests once you have
hit the monthly ceiling you set.

## Stack

| | |
| --- | --- |
| Backend | C# / .NET 10, minimal APIs, Npgsql + Dapper, PostgreSQL |
| Frontend | SvelteKit 2 / Svelte 5 runes, TypeScript, static SPA, installable PWA |
| Shipping | One container serving both from the same origin |
| i18n | German and English, compile-time via Paraglide |

## Running it

There are two ways to run Culina with your **local code**, plus running the **published production image**:

### 1. Locally from source (development with hot reload)

Best for developing, experimenting, or running the latest source directly.

**Prerequisites:** Docker, .NET 10 SDK, Node 22, and pnpm.

```bash
git clone https://github.com/jakobpriesner/culina.git
cd culina
cp .env.example .env
make dev
```

PostgreSQL starts in a container (port 5433), the API runs with hot reload (port 5000), and the Vite dev server runs at <http://localhost:5173>. Vite proxies `/api` to the backend, so development is same-origin exactly like production — which is why Culina has no CORS policy anywhere.

- Run `make dev LAN=1` to open the frontend to your local network (for testing on a phone).
- If running without `make`:
  ```bash
  docker compose up -d db
  # In one terminal (backend API):
  cd src/backend/src/Api && dotnet watch run
  # In another terminal (frontend):
  cd src/frontend && pnpm dev
  ```
- Run `make` with no target to see every developer command.

### 2. Locally as a container (built from local code)

Runs the complete single-container production build (backend API + pre-compressed static SPA served on port 8080) built directly from your local checkout rather than pulling from a remote registry.

**Prerequisites:** Docker only.

```bash
cp .env.example .env
# Fill in Database__Password and POSTGRES_SUPERUSER_PASSWORD in .env
docker compose -f compose.yaml -f compose.prod.yaml up --build -d
# Or: make image-run
```

Culina builds the multi-stage image from your local `Dockerfile` and runs at <http://localhost:8080>.

### 3. With the published image (production / self-hosting)

For self-hosting an instance without compiling from source:

```bash
cp .env.example .env
# Fill in Database__Password, POSTGRES_SUPERUSER_PASSWORD and the address of
# your reverse proxy.
docker compose -f compose.yaml -f compose.prod.yaml up -d
```

Pulls `ghcr.io/jakobpriesner/culina:latest` and serves at `http://localhost:8080`.

Point your reverse proxy at it — TLS is the proxy's job, deliberately. The first visit opens a **setup screen** — the database password you put in `.env`, which proxy to trust, whether cookies need HTTPS, and your account. **Whoever finishes it becomes the administrator**, and whether anyone else may register is then that person's decision, made in the app. The server settings stay there too, under Settings → Server.

Three volumes are mandatory, and the compose file declares all of them:
`/data/images` holds the only copy of every photograph, and losing it on a
redeploy would say nothing at all; `/data/keys` holds the ASP.NET data-protection keys; `/data/config` holds what was set up in the app, the database password among it.

[`docs/operations.md`](docs/operations.md) has installing, upgrading, backup,
a tested restore, and the proxy configuration the app expects.

## Documentation

| | |
| --- | --- |
| [`docs/domain-model.md`](docs/domain-model.md) | Entities, invariants, persistence conventions |
| [`docs/api.md`](docs/api.md) | The full v1 HTTP surface |
| [`docs/design-system.md`](docs/design-system.md) | Tokens, theming, component inventory |
| [`docs/scaling-rules.md`](docs/scaling-rules.md) | How portions scale, and how amounts are rounded |
| [`docs/search-design.md`](docs/search-design.md) | Search: retrieval, query understanding, ranking and the interaction |
| [`docs/nutrition-design.md`](docs/nutrition-design.md) | Nutrition: the food table, how a line becomes grams, and how a figure says what it covers |
| [`docs/deployment.md`](docs/deployment.md) | The image, the pipeline, and why they are shaped that way |
| [`docs/operations.md`](docs/operations.md) | Installing, upgrading, backup and restore |
| [`docs/configuration.md`](docs/configuration.md) | Every environment variable and what breaks without it |
| [`CONTRIBUTING.md`](CONTRIBUTING.md) | How to work on it, and what the conventions enforce |
| [`SECURITY.md`](SECURITY.md) | Reporting a vulnerability |
| [`docs/security-review.md`](docs/security-review.md) | What was checked against ASVS 5.0 L2, and what is deliberately absent |
| [`docs/accessibility-and-performance.md`](docs/accessibility-and-performance.md) | What is checked on every change, what the audit found, and the weight budget |

## Licence

PolyForm Noncommercial License 1.0.0. See [LICENSE](LICENSE).

Nutrition values come from the Bundeslebensmittelschlüssel (BLS) 4.0 of the
Max Rubner-Institut, under CC BY 4.0; the extract and its attribution are in
[`src/backend/src/Infrastructure/Nutrition`](src/backend/src/Infrastructure/Nutrition/BLS-ATTRIBUTION.md).
