<script lang="ts">
  import { onDestroy } from 'svelte';

  import { resolve } from '$app/paths';
  import { Chevron, IconButton, Image } from '$ds';

  import { imageUrl } from '$features/recipes/recipeImage';
  import { hrefAtYield } from '$features/recipes/surface/yieldInUrl';
  import { m } from '$shell/i18n';
  import type { MealSlot, PlannedMeal } from './mealPlan.svelte';
  import { provenance } from './provenance.svelte';
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
    /** Offered for a meal of today or earlier not yet cooked: "I made it", without opening anything. */
    onmadeit?: () => void;
    /** Beside the list: shows this meal's lines there. Without it, "on the list" leads to the list page. */
    onlines?: () => void;
    /** Today's meal: "Kochen" on the card itself, the hand-off from the plan to the hob (no Today card repeats it). */
    cookable?: boolean;
    /** Beside the list, for a meal not on it yet: its shopping onto the list, the way dragging it there does. */
    ontolist?: () => void;
  }

  let {
    meal,
    onremove,
    onpress,
    onedit,
    lifted = false,
    cooked = false,
    onmadeit,
    onlines,
    cookable = false,
    ontolist
  }: Props = $props();

  // Joined here, not in markup: Svelte trims the space at the start of an {#if} block, which made "Abendessen· 4 Portionen".
  const meta = $derived(
    [
      slotLabel[meal.slot as MealSlot]?.(),
      meal.servings ? m['recipes.meta.servings']({ count: meal.servings }) : undefined
    ]
      .filter(Boolean)
      .join(' · ')
  );

  /** Beside the list: pointing at the card lights its lines there, and a line pointed at lights this card. */
  const beside = $derived(onlines !== undefined);
  const related = $derived(beside && provenance.lineMeals.includes(meal.entryId));

  function point(event: PointerEvent | FocusEvent, on: boolean) {
    if (!beside || ('pointerType' in event && event.pointerType !== 'mouse')) return;
    // Focus moving between the card's own controls is still the card.
    if (!on && event instanceof FocusEvent && event.currentTarget instanceof Element) {
      if (event.relatedTarget instanceof Node && event.currentTarget.contains(event.relatedTarget))
        return;
    }
    if (on) provenance.pointMeal(meal.entryId);
    else if (provenance.meal === meal.entryId) provenance.pointMeal(null);
  }

  // A card unplanned while pointed at leaves nothing lit behind.
  onDestroy(() => {
    if (provenance.meal === meal.entryId) provenance.pointMeal(null);
  });
</script>

{#snippet listedMark()}
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" aria-hidden="true">
    <path d="m5 12 5 5 9-10" stroke-linecap="round" stroke-linejoin="round" />
  </svg>
{/snippet}

<!-- A press anywhere can become a drag (the phone gesture); the real button is the grip.
     `dragstart` is refused because a native drag of the link or picture stops pointer events. -->
<!-- svelte-ignore a11y_no_static_element_interactions -->
<div
  class="card"
  class:lifted
  class:related
  onpointerdown={onpress}
  onpointerenter={(event) => point(event, true)}
  onpointerleave={(event) => point(event, false)}
  onfocusin={(event) => point(event, true)}
  onfocusout={(event) => point(event, false)}
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

  <div class="body">
    {#if meal.imageId}
      <div class="thumb">
        <Image src={imageUrl(meal.recipeId, 400, meal.imageId)} alt="" ratio={1} />
      </div>
    {/if}

    <div class="words">
      <!-- The title is the link, stretched over the card, so the status beside the meta can be a control of its own. -->
      <!-- eslint-disable svelte/no-navigation-without-resolve -- The route is resolved; only the yield is appended. -->
      <a
        class="name"
        href={hrefAtYield(
          resolve('/(app)/recipes/[recipeId]', { recipeId: meal.recipeId }),
          meal.servings
        )}>{meal.title}</a
      >
      <!-- eslint-enable svelte/no-navigation-without-resolve -->
      <span class="line">
        <span class="meta">{meta}</span>
        {#if meal.leftoverOf}
          <span class="listed">{m['plan.leftover']()}</span>
        {:else if cooked}
          <span class="listed">{@render listedMark()}{m['plan.cooked']()}</span>
        {:else if meal.isOnShoppingList}
          <!-- "On the list" leads to the meal's lines: beside the week in the pane, on a phone the list page. -->
          {#if onlines}
            <button class="listed to-lines" type="button" data-no-drag onclick={onlines}>
              {@render listedMark()}{m['plan.onList']()}
            </button>
          {:else}
            <!-- eslint-disable svelte/no-navigation-without-resolve -- The route is resolved; only the meal is appended. -->
            <a
              class="listed to-lines"
              data-no-drag
              href={`${resolve('/(app)/shopping')}?mahlzeit=${meal.entryId}`}
              >{@render listedMark()}{m['plan.onList']()}</a
            >
            <!-- eslint-enable svelte/no-navigation-without-resolve -->
          {/if}
        {:else if ontolist}
          <button
            class="listed to-lines"
            type="button"
            data-no-drag
            aria-label={m['plan.toList.label']({ title: meal.title })}
            onclick={ontolist}>+ {m['plan.toList']()}</button
          >
        {/if}
        {#if cookable && !cooked}
          <!-- eslint-disable svelte/no-navigation-without-resolve -- The route is resolved; only the yield is appended. -->
          <a
            class="cook"
            data-no-drag
            href={hrefAtYield(
              resolve('/(app)/recipes/[recipeId]/cook', { recipeId: meal.recipeId }),
              meal.servings
            )}>{m['plan.today.cook']()}<Chevron /></a
          >
          <!-- eslint-enable svelte/no-navigation-without-resolve -->
        {/if}
      </span>
    </div>
  </div>

  <span class="actions" data-no-drag>
    {#if onmadeit}
      <IconButton
        label={m['cooking.madeIt.named']({ title: meal.title })}
        size="sm"
        onclick={onmadeit}
      >
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2">
          <path d="m5 12 5 5 9-10" stroke-linecap="round" stroke-linejoin="round" />
        </svg>
      </IconButton>
    {/if}

    <IconButton label={m['plan.remove']({ title: meal.title })} size="sm" onclick={onremove}>
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        <path d="m6 6 12 12M18 6 6 18" stroke-linecap="round" />
      </svg>
    </IconButton>
  </span>
</div>

<style>
  /* A list cell: raised, a hairline edge rather than a shadow, so seven days of them stay calm. */
  .card {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-1) var(--space-2);
    width: 100%;
    padding: var(--space-1) var(--space-2) var(--space-1) var(--space-1);
    border: 1px solid var(--border);
    border-radius: var(--radius-md);
    background: var(--surface-raised);
    /* The title's link covers the card; its controls sit above it. */
    position: relative;
  }

  /* The picture stays beside the words at any width: above them it made one card twice the height of its neighbour. */
  .body {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    flex: 1 1 10rem;
    min-height: 3.25rem;
    min-width: 0;
  }

  .thumb {
    flex: 0 0 auto;
    width: 2.5rem;
    height: 2.5rem;
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
    display: -webkit-box;
    -webkit-box-orient: vertical;
    -webkit-line-clamp: 2;
    line-clamp: 2;
    overflow: hidden;
    font-size: var(--text-sm);
    font-weight: var(--weight-medium);
    line-height: var(--leading-tight);
    /* Whole words: no hyphenation ("Pizza-teig"), and only a word wider than the card itself breaks (the inherited
       'anywhere' would shrink the minimum width and cut every word mid-way). */
    hyphens: manual;
    overflow-wrap: break-word;
    color: inherit;
    text-decoration: none;
  }

  /* The whole card opens the recipe, as it did when the card was one link. */
  .name::after {
    content: '';
    position: absolute;
    inset: 0;
    border-radius: inherit;
  }

  .name:focus-visible {
    outline: none;
  }

  .name:focus-visible::after {
    outline: 2px solid var(--border-focus);
    outline-offset: 2px;
  }

  /* Above the stretched link: each is a control of its own. */
  .card :global(> button:first-child),
  .actions,
  .cook,
  .to-lines {
    position: relative;
    z-index: 1;
  }

  /* The meta and the status share a line, wrapping only when the card is narrow. */
  .line {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    column-gap: var(--space-2);
  }

  /*
   * Today's "Kochen": the hand-off from the plan to the hob, at the end of the meal's line so every card keeps one
   * shape. Tonal rather than filled, so a day of three meals is not three loud buttons; 32px drawn, 44px to press.
   */
  .cook {
    display: inline-flex;
    flex: none;
    align-items: center;
    gap: var(--space-1);
    min-height: 2rem;
    margin-inline-start: auto;
    padding-inline: var(--space-3) var(--space-2);
    border-radius: var(--radius-full);
    background: var(--surface-accent-subtle);
    color: var(--accent);
    font-size: var(--text-xs);
    font-weight: var(--weight-semibold);
    text-decoration: none;
    transition: background-color var(--duration-fast) var(--ease-out);
  }

  .cook::before {
    content: '';
    position: absolute;
    inset: -0.375rem 0;
  }

  .cook:hover {
    background: var(--surface-selected);
  }

  .cook:focus-visible {
    outline: 2px solid var(--border-focus);
    outline-offset: 2px;
  }

  .actions {
    display: inline-flex;
    margin-inline-start: auto;
  }

  /* Left behind while its copy is dragged, so the day doesn't reflow under the pointer. */
  /* A line in the list pointed at is for this meal. */
  .related {
    box-shadow: 0 0 0 2px var(--accent);
  }

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

  /* Beside the meta; a target of at least 24px. */
  .to-lines {
    min-height: 1.5rem;
    padding: 0;
    border: none;
    background: none;
    font: inherit;
    font-size: var(--text-xs);
    text-align: start;
    text-decoration: none;
    cursor: pointer;
  }

  .to-lines:hover {
    color: var(--text);
    text-decoration: underline;
    text-underline-offset: 0.2em;
  }

  .listed svg {
    flex: 0 0 auto;
    width: 0.9em;
    height: 0.9em;
    color: var(--success);
  }
</style>
