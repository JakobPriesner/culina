<script lang="ts">
  import { resolve } from '$app/paths';
  import { IconButton, Image } from '$ds';

  import { imageUrl } from '$features/recipes/recipeImage';
  import { m } from '$shell/i18n';
  import type { PlannedMeal } from './mealPlan.svelte';

  /**
   * One planned meal, on the day it is planned for.
   *
   * Small on purpose. A week view is seven of these side by side, and a card
   * carrying everything a recipe card carries turns a week into a wall.
   */
  interface Props {
    meal: PlannedMeal;
    onremove: () => void;
    /**
     * A press on the grip, or anywhere on the card. Whether it becomes a drag
     * is the week's business, not this card's.
     */
    onpress?: (event: PointerEvent) => void;
    /** The grip, activated rather than dragged: by a click, or by a keyboard. */
    onmove?: () => void;
    /** Dimmed, because the real one is the one under the pointer. */
    lifted?: boolean;
  }

  let { meal, onremove, onpress, onmove, lifted = false }: Props = $props();
</script>

<!-- A press anywhere on the card can become a drag, which is the whole gesture
     on a phone. It carries no role and needs none: it is an enhancement for a
     pointer over the grip below, which is a real button that a keyboard reaches
     and a screen reader announces.

     `dragstart` is refused because a card is a link wrapped round a picture,
     and both of those are things a browser starts dragging by itself. Once it
     does, it stops sending pointer events altogether and the card is left
     behind — so the native drag has to be declined before ours can happen. -->
<!-- svelte-ignore a11y_no_static_element_interactions -->
<div
  class="card"
  class:lifted
  onpointerdown={onpress}
  ondragstart={(event) => event.preventDefault()}
>
  {#if onmove}
    <!-- The handle and the button are one control. Dragging it is the quick
         way; pressing it opens the sheet, which is the only way for a keyboard
         and the easier way for anybody whose Thursday is off the screen. -->
    <IconButton label={m['plan.move.handle']({ title: meal.title })} size="sm" onclick={onmove}>
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

  <a class="body" href={resolve('/(app)/recipes/[recipeId]', { recipeId: meal.recipeId })}>
    {#if meal.imageId}
      <div class="thumb">
        <Image src={imageUrl(meal.recipeId, 400)} alt="" ratio={1} />
      </div>
    {/if}

    <div class="words">
      <span class="name">{meal.title}</span>
      <span class="meta">
        {m[`plan.slot.${meal.slot as 'dinner'}`]?.() ?? ''}{#if meal.servings}
          · {m['recipes.meta.servings']({ count: meal.servings })}{/if}
      </span>
      <!-- Part of the link's name, so a screen reader hears it with the meal
           rather than as a stray word between cards. -->
      {#if meal.isOnShoppingList}
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

  /* A narrow day gives the title its own row. Keep the whole name readable
     so similar recipes remain distinguishable in the seven-day view. */
  .name {
    display: block;
    font-size: var(--text-sm);
    font-weight: var(--weight-medium);
    line-height: var(--leading-tight);
    /* A long unbroken word would otherwise push out of the card rather than
       wrap inside it, and a column this narrow meets one eventually. */
    overflow-wrap: anywhere;
  }

  .remove {
    margin-inline-start: auto;
  }

  /* Left behind while its copy is under the pointer, rather than removed: the
     gaps a drop is aimed at are the gaps of the day as it looks right now, and
     a day that reflows as you cross it is a day you cannot aim at. */
  .lifted {
    opacity: 0.4;
  }

  /* The grip is quiet until it is wanted. Always drawn, though — on a phone
     there is no hover to reveal it with, and a control that appears only on a
     pointer is a control a phone does not have. */
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
