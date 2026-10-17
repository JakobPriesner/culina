<script lang="ts">
  import { resolve } from '$app/paths';
  import { IconButton, Image } from '$ds';

  import { imageUrl } from '$features/recipes/recipeImage';
  import { hrefAtYield } from '$features/recipes/surface/yieldInUrl';
  import { m } from '$shell/i18n';
  import type { MealSlot, PlannedMeal } from './mealPlan.svelte';
  import { slotLabel } from './slots';

  /** One planned meal on its day; small on purpose, as a week is seven of these side by side. */
  interface Props {
    meal: PlannedMeal;
    onremove: () => void;
    /** A press on the grip or card; whether it becomes a drag is the week's business. */
    onpress?: (event: PointerEvent) => void;
    /** Opens the meal's sheet: its day, meal and servings. */
    onedit?: () => void;
    lifted?: boolean;
    /** Cooked on its day: said on the card, so the week reads as what happened. */
    cooked?: boolean;
  }

  let { meal, onremove, onpress, onedit, lifted = false, cooked = false }: Props = $props();

  // Joined here, not in markup: Svelte trims the space at the start of an {#if} block, which made "Abendessen· 4 Portionen".
  const meta = $derived(
    [
      slotLabel[meal.slot as MealSlot]?.(),
      meal.servings ? m['recipes.meta.servings']({ count: meal.servings }) : undefined
    ]
      .filter(Boolean)
      .join(' · ')
  );
</script>

<!-- A press anywhere can become a drag (the phone gesture); the real button is the grip.
     `dragstart` is refused because a native drag of the link or picture stops pointer events. -->
<!-- svelte-ignore a11y_no_static_element_interactions -->
<div
  class="card"
  class:lifted
  onpointerdown={onpress}
  ondragstart={(event) => event.preventDefault()}
>
  {#if onedit}
    <!-- Handle and button are one control: dragging is quick, pressing opens the sheet (the
         keyboard's only way). -->
    <IconButton label={m['plan.move.handle']({ title: meal.title })} size="sm" onclick={onedit}>
      <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
        <circle cx="9" cy="6" r="1.5" />
        <circle cx="15" cy="6" r="1.5" />
        <circle cx="9" cy="12" r="1.5" />
        <circle cx="15" cy="12" r="1.5" />
        <circle cx="9" cy="18" r="1.5" />
        <circle cx="15" cy="18" r="1.5" />
      </svg>
    </IconButton>
  {/if}

  <!-- eslint-disable svelte/no-navigation-without-resolve -- The route is resolved; only the yield is appended. -->
  <a
    class="body"
    href={hrefAtYield(
      resolve('/(app)/recipes/[recipeId]', { recipeId: meal.recipeId }),
      meal.servings
    )}
  >
    <!-- eslint-enable svelte/no-navigation-without-resolve -->
    {#if meal.imageId}
      <div class="thumb">
        <Image src={imageUrl(meal.recipeId, 400, meal.imageId)} alt="" ratio={1} />
      </div>
    {/if}

    <div class="words">
      <span class="name">{meal.title}</span>
      <span class="meta">{meta}</span>
      {#if cooked}
        <span class="listed">
          <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2.5"
            aria-hidden="true"
          >
            <path d="m5 12 5 5 9-10" stroke-linecap="round" stroke-linejoin="round" />
          </svg>
          {m['plan.cooked']()}
        </span>
      {:else if meal.isOnShoppingList}
        <span class="listed">
          <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2.5"
            aria-hidden="true"
          >
            <path d="m5 12 5 5 9-10" stroke-linecap="round" stroke-linejoin="round" />
          </svg>
          {m['plan.onList']()}
        </span>
      {/if}
    </div>
  </a>

  <span class="remove" data-no-drag>
    <IconButton label={m['plan.remove']({ title: meal.title })} size="sm" onclick={onremove}>
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        <path d="m6 6 12 12M18 6 6 18" stroke-linecap="round" />
      </svg>
    </IconButton>
  </span>
</div>

<style>
  .card {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
    width: 100%;
    padding: var(--space-2);
    border-radius: var(--radius-sm);
    background: var(--surface-raised);
  }

  .body {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    flex: 1 1 10rem;
    flex-wrap: wrap;
    min-height: var(--control-sm);
    min-width: 0;
    color: inherit;
    text-decoration: none;
  }

  .thumb {
    flex: 0 0 auto;
    width: var(--space-8);
    height: var(--space-8);
    overflow: hidden;
    border-radius: var(--radius-sm);
  }

  .words {
    flex: 1 1 8rem;
    display: flex;
    flex-direction: column;
    min-width: 0;
  }

  /*
   * A narrow day gives the title its own row, kept readable so similar recipes stay
   * distinguishable.
   */
  .name {
    display: block;
    font-size: var(--text-sm);
    font-weight: var(--weight-medium);
    line-height: var(--leading-tight);
    /* Long compounds hyphenate by the page's language; only a word wider than the card breaks anywhere else
       (the inherited 'anywhere' would shrink the minimum width and cut every word mid-way). */
    hyphens: auto;
    overflow-wrap: break-word;
  }

  .remove {
    display: inline-flex;
    margin-inline-start: auto;
  }

  /* Left behind while its copy is dragged, so the day doesn't reflow under the pointer. */
  .lifted {
    opacity: 0.4;
  }

  /* Always drawn: a phone has no hover to reveal it with. */
  .card :global(> button:first-child) {
    color: var(--text-subtle);
    cursor: grab;
    touch-action: manipulation;
  }

  .meta {
    color: var(--text-muted);
    font-size: var(--text-xs);
  }

  .listed {
    display: inline-flex;
    align-items: center;
    gap: var(--space-1);
    color: var(--text-muted);
    font-size: var(--text-xs);
  }

  .listed svg {
    flex: 0 0 auto;
    width: 0.9em;
    height: 0.9em;
    color: var(--success);
  }
</style>
