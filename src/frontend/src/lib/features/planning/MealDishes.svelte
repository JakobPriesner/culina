<script lang="ts">
  import { resolve } from '$app/paths';

  import { hrefAtYield } from '$features/recipes/surface/yieldInUrl';
  import { m } from '$shell/i18n';

  import { cookedOn, asDate } from './mealPlan.svelte';
  import { upcoming } from './upcoming.svelte';

  /**
   * The other dishes of the meal being cooked (the rice beside the curry), one tap away in cook mode. Switching
   * sets the dish on screen aside, and coming back picks it up at its step, so two pans can be cooked at once.
   * Nothing when the recipe is the meal's only dish, or not on today's plan.
   */
  interface Props {
    recipeId: string;
  }

  let { recipeId }: Props = $props();

  const dishes = $derived(upcoming.dishesWith(recipeId));
  const today = asDate(new Date());
</script>

{#if dishes.length > 1}
  <nav class="dishes" aria-label={m['plan.dishes.label']()}>
    <!-- eslint-disable svelte/no-navigation-without-resolve -- The route is resolved; only the yield is appended. -->
    {#each dishes as dish (dish.entryId)}
      <a
        class="dish"
        class:done={cookedOn(dish, today)}
        aria-current={dish.recipeId === recipeId ? 'page' : undefined}
        href={hrefAtYield(
          resolve('/(app)/recipes/[recipeId]/cook', { recipeId: dish.recipeId }),
          dish.servings
        )}>{dish.title}</a
      >
    {/each}
    <!-- eslint-enable svelte/no-navigation-without-resolve -->
    <span class="hint">{m['plan.dishes.hint']()}</span>
  </nav>
{/if}

<style>
  .dishes {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
    margin-bottom: var(--space-4);
  }

  .dish {
    display: inline-flex;
    align-items: center;
    min-height: var(--control-sm);
    padding: 0 var(--space-3);
    border: 1px solid var(--border);
    border-radius: var(--radius-full);
    color: var(--text);
    font-size: var(--text-sm);
    text-decoration: none;
    overflow-wrap: normal;
  }

  .dish[aria-current='page'] {
    border-color: var(--accent);
    background: var(--surface-accent-subtle);
    font-weight: var(--weight-medium);
  }

  .done {
    color: var(--text-muted);
    text-decoration: line-through;
  }

  .hint {
    color: var(--text-muted);
    font-size: var(--text-xs);
  }
</style>
