---
name: sveltekit-loading-and-skeletons
description: Loading-state conventions for the culina-v2 SvelteKit app — skeletons that mirror the real layout instead of spinners, the delay/minimum-duration rules that stop flicker, empty and error states, and pending feedback for mutations. Use whenever a component or route waits on data or on a mutation.
---

# Loading states and skeletons

**Prefer a skeleton to a spinner.** A skeleton shows the shape of what is
coming, so the layout does not jump when data arrives and the wait feels
shorter. A spinner says only "something is happening" and hides the page.

Every screen that loads data has four states, and all four are designed and
tested: **loading · ready · empty · error**. A component with only the ready
state is unfinished.

## Skeletons mirror the real thing

```svelte
{#if recipes.status === 'loading'}
  <ul class="recipe-grid" aria-busy="true">
    {#each { length: 6 } as _}
      <RecipeCardSkeleton />
    {/each}
  </ul>
{:else if recipes.status === 'error'}
  <ErrorState error={recipes.error} onretry={() => recipes.load()} />
{:else if recipes.items.length === 0}
  <EmptyState title="No recipes yet" action="Add your first recipe" href="/recipes/new" />
{:else}
  <ul class="recipe-grid">{#each recipes.items as recipe (recipe.id)}<RecipeCard {recipe} />{/each}</ul>
{/if}
```

- The skeleton lives **next to the component it stands in for**
  (`RecipeCard.svelte` → `RecipeCardSkeleton.svelte`) and uses the same
  container, spacing and dimensions. If the card changes height, the skeleton
  changes with it.
- Build skeletons from one `<Skeleton />` primitive in `lib/design-system`
  (a block with the shimmer animation, sized by props), not from bespoke CSS
  per feature.
- Repeat a realistic number of placeholder rows — roughly what a typical
  response contains, capped at what fits the viewport. Never one.
- Reserve space for images with the correct aspect ratio so nothing shifts
  (cumulative layout shift is the thing this prevents).
- Skeletons are decorative: `aria-hidden="true"` on the placeholders, with
  `aria-busy="true"` on the container and a polite live-region announcement
  when the content arrives. A screen reader should hear "loading", not
  thirty empty boxes.
- Respect `prefers-reduced-motion`: drop the shimmer to a static tint.

## Timing rules

Flicker is worse than waiting:

- **Delay ~150 ms before showing any loading UI.** A fast response should never
  flash a skeleton.
- **Once shown, keep it ~300 ms minimum**, so it cannot appear and vanish in
  the same frame.
- Both live in one helper (`createLoadingState`) in `lib/app`, used everywhere
  — never re-implemented per component (`code-simplicity`).
- Beyond ~10 s, replace the skeleton with an explicit "this is taking longer
  than usual" message plus a retry, rather than an endless shimmer.

## Where each pattern applies

| Situation | Pattern |
| --- | --- |
| Initial load of a list or detail page | Skeleton mirroring the layout. |
| Refetch of data already on screen | Keep the old data, add a subtle progress hint. **Never** replace content with a skeleton. |
| Route transition | SvelteKit's navigating state → a thin top progress bar; skeletons inside the new page. |
| A mutation | The affected row/button shows pending (dimmed, spinner in the button, disabled), not a full-page overlay. Most mutations are optimistic and show nothing at all (`sveltekit-state-and-optimistic-ui`). |
| Infinite scroll / next page | Skeleton rows appended below existing content. |
| A tiny inline value | A short dash or shimmering text block, not a spinner. |

A full-page spinner is acceptable in exactly one place: the very first app boot
while the session is resolved.

## Empty and error states

- An empty state explains **why it is empty and what to do next**, with the
  action as a real button or link. "No results" alone is not an empty state.
  Distinguish "you have nothing yet" from "your filter matched nothing" —
  the second offers to clear the filter.
- An error state keeps the page frame, says what failed in plain language,
  offers retry, and shows the `requestId` in small print for support.
- Neither is an afterthought component invented per page; both come from
  `lib/design-system` (`EmptyState`, `ErrorState`) with props.

## Checklist

- [ ] All four states implemented and covered by a component test.
- [ ] Skeleton mirrors the real layout and dimensions; built from the shared
      `Skeleton` primitive; sits beside the component it replaces.
- [ ] 150 ms delay and 300 ms minimum, from the shared helper.
- [ ] Refetches keep existing content instead of blanking it.
- [ ] `aria-busy`, hidden placeholders, reduced-motion respected.
- [ ] Empty and error states say what happened and what to do next.
