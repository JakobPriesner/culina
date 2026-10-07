<script lang="ts">
  import { Button } from '$ds';
  import { sortLabel } from '$features/recipes/filters/labels';
  import type { RecipeSort } from '$features/recipes/stores/libraryView.svelte';
  import { recipes } from '$features/recipes/stores/recipes.svelte';
  import { m } from '$shell/i18n';

  interface Props {
    /** Whether a filter is on. */
    filtered: boolean;
    order: RecipeSort;
    onreset: () => void;
  }

  let { filtered, order, onreset }: Props = $props();
</script>

<div class="collection-summary" aria-live="polite" aria-atomic="true">
  <p class="count">
    {#if recipes.status === 'loading' || recipes.status === 'idle'}
      {m['recipes.list.loading']()}
    {:else if recipes.status === 'ready'}
      {m['recipes.list.count']({ count: recipes.total })}
    {/if}
  </p>
  {#if filtered}
    <Button size="sm" variant="ghost" onclick={onreset}>
      {m['recipes.filter.reset']()}
    </Button>
  {:else if recipes.status === 'ready' && recipes.items.length > 0}
    <!-- The order is always named. A list whose order changed without
         saying so is the thing that makes people stop trusting an app. -->
    <p class="collection-note">{sortLabel(order)}</p>
  {/if}
</div>

<style>
  .collection-summary {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
  .count {
    color: var(--text);
    font-weight: var(--weight-medium);
    font-variant-numeric: tabular-nums;
  }
  .collection-note {
    font-size: var(--text-xs);
  }
  @media (max-width: 40rem) {
    .collection-summary {
      width: 100%;
      justify-content: space-between;
    }
  }
</style>
