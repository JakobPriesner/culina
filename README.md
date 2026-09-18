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
everything built on it). No calorie calculation (a wrong number is worse than
none). No social feed, ratings or comments — this is your kitchen, not a
network. No AI assistant.

## Stack

| | |
| --- | --- |
| Backend | C# / .NET 10, minimal APIs, Npgsql + Dapper, PostgreSQL |
| Frontend | SvelteKit 2 / Svelte 5 runes, TypeScript, static SPA, installable PWA |
| Shipping | One container serving both from the same origin |
| i18n | German and English, compile-time via Paraglide |

## Running it

```bash
git clone https://github.com/jakobpriesner/culina.git
cd culina
cp .env.example .env
# Fill in Database__Password and the address of your reverse proxy.
docker compose -f compose.yaml -f compose.prod.yaml up -d
```

Culina is at `http://localhost:8080`. Point your proxy at it — TLS is the
proxy's job, deliberately. **The first account you create becomes the
administrator**, and whether anyone else may register is then that person's
decision, made in the app.

Two volumes are mandatory, and the compose file declares both: `/data/images`
holds the only copy of every photograph, and losing it on a redeploy would say
nothing at all.

[`docs/operations.md`](docs/operations.md) has installing, upgrading, backup,
a tested restore, and the proxy configuration the app expects.

### Working on it instead

```bash
cp .env.example .env
make dev
```

PostgreSQL in a container, the API with hot reload, the frontend dev server.
The app is at <http://localhost:5173> with `/api` proxied to the backend, so
development is same-origin exactly like production — which is why Culina has no
CORS policy anywhere. `make` with no target lists every command.

## Documentation

| | |
| --- | --- |
| [`docs/domain-model.md`](docs/domain-model.md) | Entities, invariants, persistence conventions |
| [`docs/api.md`](docs/api.md) | The full v1 HTTP surface |
| [`docs/design-system.md`](docs/design-system.md) | Tokens, theming, component inventory |
| [`docs/scaling-rules.md`](docs/scaling-rules.md) | How portions scale, and how amounts are rounded |
| [`docs/search-design.md`](docs/search-design.md) | Search: retrieval, query understanding, ranking and the interaction |
| [`docs/deployment.md`](docs/deployment.md) | The image, the pipeline, and why they are shaped that way |
| [`docs/operations.md`](docs/operations.md) | Installing, upgrading, backup and restore |
| [`docs/configuration.md`](docs/configuration.md) | Every environment variable and what breaks without it |
| [`CONTRIBUTING.md`](CONTRIBUTING.md) | How to work on it, and what the conventions enforce |
| [`SECURITY.md`](SECURITY.md) | Reporting a vulnerability |
| [`docs/security-review.md`](docs/security-review.md) | What was checked against ASVS 5.0 L2, and what is deliberately absent |
| [`docs/accessibility-and-performance.md`](docs/accessibility-and-performance.md) | What is checked on every change, what the audit found, and the weight budget |

## Licence

AGPL-3.0. See [LICENSE](LICENSE).
