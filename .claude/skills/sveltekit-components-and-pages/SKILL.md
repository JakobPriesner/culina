---
name: sveltekit-components-and-pages
description: How the culina-v2 SvelteKit frontend is organised — the routes/lib/features split, when something is a page vs a route component vs a shared design-system component, Svelte 5 runes and props conventions, naming, accessibility and component testing. Use when adding a page or route, creating or refactoring a component, or deciding where a piece of UI belongs.
---

# Components, routes and pages (SvelteKit 5)

```
src/frontend/
  src/
    routes/                    URLs only. One folder per URL segment.
      +layout.svelte
      +page.svelte
      recipes/+page.svelte
      recipes/[recipeId]/+page.svelte
    lib/
      api/                     generated client + wrapper (frontend-api-client)
      design-system/           generic, domain-free UI: Button, Card, Skeleton, Dialog
      features/                one folder per domain, mirroring the API's domains
        recipes/
          components/RecipeCard.svelte
          stores/recipes.svelte.ts
          mappers.ts
      app/                     cross-cutting app shell: auth state, i18n, errors, telemetry
      utils/
  static/                      robots.txt, sitemap, icons (frontend-static-assets)
```

The rule that keeps this navigable: **`routes/` holds URLs, `lib/features/`
holds behaviour, `lib/design-system/` holds looks.** A route file that grows
past roughly 100 lines is moving logic into a feature component.

## What each kind of file may do

| Kind | Responsibility | Must not |
| --- | --- | --- |
| `+page.svelte` | Read route params, compose feature components, set the page title. | Contain business logic, call `fetch` directly, define reusable markup. |
| `+layout.svelte` | Shell, navigation, error boundary, things shared by a subtree. | Fetch domain data for one child page. |
| `+page.ts` load | Kick off the data fetch for the route through the API client. | Duplicate a store's logic; hold state. |
| Feature component | One coherent piece of a domain UI, owning its interactions. | Know about routing, or reach for another feature's store. |
| Design-system component | Presentation and interaction, driven entirely by props. | Import a store, call the API, or mention a domain noun. |

If a design-system component needs to know what a recipe is, it is a feature
component that landed in the wrong folder.

## Svelte 5 conventions

```svelte
<script lang="ts">
  import type { Snippet } from 'svelte';
  import type { Recipe } from '$lib/features/recipes/types';

  interface Props {
    recipe: Recipe;
    compact?: boolean;
    onselect?: (id: string) => void;
    children?: Snippet;
  }

  let { recipe, compact = false, onselect, children }: Props = $props();

  let servings = $state(recipe.servings);
  let scaled = $derived(scaleIngredients(recipe.ingredients, servings));
</script>
```

- Runes only: `$state`, `$derived`, `$props`, `$effect`. No legacy `export
  let`, no `$:` reactive statements, no writable stores imported into
  components for local concerns.
- Props are typed through one `interface Props`; every prop has a type, and
  optional props have defaults.
- Callback props (`onselect`) over `createEventDispatcher`.
- `$effect` is a last resort, for synchronising with something outside Svelte
  (focus, a subscription, the document title). Deriving state in an effect is
  a bug — use `$derived`.
- One component per file, PascalCase filename matching the component name.
  Functions and stores are camelCase; types are PascalCase.
- Components are the unit of reuse: markup or interaction written a second time
  becomes a component the first time it repeats (`code-simplicity`).

## Data flow

- A route loads data through the feature's store, which calls the API client.
  A component **never** calls `fetch` and never imports the generated client
  directly.
- Data flows down as props; changes flow up as callbacks or straight into the
  store. No shared mutable module-level variables.
- A component that needs a domain type imports it from the feature's `types.ts`,
  which maps the generated API types into the shapes the UI wants — the
  frontend mirror of the backend's mapping rule. Generated types are never
  spread through the component tree raw.
- Text is never hard-coded in a component; it comes from the i18n layer in
  `lib/app/i18n`.

## Accessibility and semantics

Non-negotiable, because retrofitting them is far more expensive:

- Real elements: `<button>` for actions, `<a>` for navigation. Never a
  `<div on:click>`.
- Every input has a `<label>`; every image has `alt`; every icon-only control
  has an accessible name.
- One `<h1>` per page, heading levels never skipped.
- Focus is visible, and focus moves to the right place after a dialog opens or
  a route changes.
- Colour is never the only carrier of meaning, and contrast meets WCAG AA.
- Dialogs, menus and tooltips come from the accessible primitives in
  `lib/design-system`, not hand-rolled per feature.

## Testing

- Component tests with Vitest + `@testing-library/svelte`, querying by role and
  accessible name — never by CSS class or test id, unless there is genuinely no
  accessible handle.
- Test what a user can do: render, interact, assert on what is shown, including
  the loading and error states (`sveltekit-loading-and-skeletons`).
- Playwright covers the flows that cross pages and the auth/cookie path.

## Checklist

- [ ] File is in `routes/` (a URL), `lib/features/<domain>/` (behaviour), or
      `lib/design-system/` (presentation) — and does only that job.
- [ ] Runes, typed `Props` interface, callback props.
- [ ] No `fetch` or generated client import outside `lib/api`.
- [ ] Semantic elements, labels, one `h1`, visible focus.
- [ ] Loading and error states exist and are tested, not just the happy path.
