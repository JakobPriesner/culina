<script lang="ts">
  import { m } from '$shell/i18n';
  import RecipeCard from './RecipeCard.svelte';
  import RecipeCardSkeleton from './RecipeCardSkeleton.svelte';
  import type { RecipeSummary } from './types';

  /**
   * The recipes, in columns.
   *
   * Three across on a wide screen, one on a phone — the rows are text, so they
   * read as columns of a page rather than as a gallery of tiles. The number of
   * columns is the only thing that changes; the row itself is identical
   * everywhere.
   */
  interface Props {
    recipes: readonly RecipeSummary[];
    /** Drawn instead of the recipes, when there are none yet to draw. */
    loading?: boolean;
    /** Ids whose change is in flight. */
    pending?: readonly string[];
  }

  let { recipes, loading = false, pending = [] }: Props = $props();

  /** Enough to fill the visible area without pretending to know the count. */
  const placeholders = [0, 1, 2, 3, 4, 5];
</script>

{#if loading}
  <div class="grid" aria-busy="true" aria-label={m['recipes.list.loading']()}>
    {#each placeholders as row (row)}
      <RecipeCardSkeleton />
    {/each}
  </div>
{:else}
  <ul class="grid">
    {#each recipes as recipe (recipe.id)}
      <li><RecipeCard {recipe} pending={pending.includes(recipe.id)} /></li>
    {/each}
  </ul>
{/if}

<style>
  .grid {
    display: grid;
    grid-template-columns: repeat(3, minmax(0, 1fr));
    gap: 0 var(--space-8);
    margin: 0;
    padding: 0;
    list-style: none;
  }

  li {
    min-width: 0;
  }

  @media (max-width: 63.999rem) {
    .grid {
      grid-template-columns: repeat(2, minmax(0, 1fr));
    }
  }

  @media (max-width: 40rem) {
    .grid {
      grid-template-columns: 1fr;
    }
  }
</style>
