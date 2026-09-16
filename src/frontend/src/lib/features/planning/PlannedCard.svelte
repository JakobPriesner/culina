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
  }

  let { meal, onremove }: Props = $props();
</script>

<div class="card">
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
    </div>
  </a>

  <IconButton label={m['plan.remove']({ title: meal.title })} size="sm" onclick={onremove}>
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
      <path d="m6 6 12 12M18 6 6 18" stroke-linecap="round" />
    </svg>
  </IconButton>
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

  .card :global(> button) {
    margin-inline-start: auto;
  }

  .meta {
    color: var(--text-muted);
    font-size: var(--text-xs);
  }
</style>
