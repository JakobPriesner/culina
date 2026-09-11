# Culina

A self-hosted recipe app for the people you actually cook with.

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
Leave mid-cook and come back to the same step with your timers still running.
A slim bar anywhere in the app brings you straight back.

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

## Quick start

```bash
cp .env.example .env
make dev
```

`make` with no target lists every command.

That starts PostgreSQL, the API and the frontend dev server. The app is at
<http://localhost:5173>; the API is proxied at `/api`, so development is
same-origin exactly like production.

Running the whole thing as it ships:

```bash
docker compose -f compose.yaml -f compose.prod.yaml up
```

## Documentation

| | |
| --- | --- |
| [`docs/domain-model.md`](docs/domain-model.md) | Entities, invariants, persistence conventions |
| [`docs/api.md`](docs/api.md) | The full v1 HTTP surface |
| [`docs/design-system.md`](docs/design-system.md) | Tokens, theming, component inventory |
| [`docs/scaling-rules.md`](docs/scaling-rules.md) | How portions scale, and how amounts are rounded |
| [`docs/deployment.md`](docs/deployment.md) | Image, configuration, CI/CD, operations |

## Working on Culina

Conventions are not in a wiki — they are in [`.claude/skills/`](.claude/skills/)
and they are enforced by architecture tests and the compiler. Work is tracked
as [beads](https://github.com/steveyegge/beads) issues:

```bash
bd ready          # claimable work, blockers resolved
bd show <id>      # the full ticket before starting
```

## Licence

AGPL-3.0. See [LICENSE](LICENSE).
