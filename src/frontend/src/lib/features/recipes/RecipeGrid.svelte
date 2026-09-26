<script lang="ts">
  import { m } from '$shell/i18n';
  import { whenVisible } from '$shell/whenVisible';

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
    /**
     * Asked for the next page when the end of the list comes into view.
     *
     * Given only while there is a next page to ask for. It is called once per
     * row of placeholders and again whenever they come back into view, so it
     * has to be free to call while its own request is still running.
     */
    onmore?: () => void;
    /** What was searched for, marked in each result it found. */
    query?: string;
    /**
     * The households this one inherits recipes from, by id, with their names.
     * A recipe from one of them says so on its card.
     */
    inherited?: Readonly<Record<string, string>>;
  }

  let { recipes, loading = false, pending = [], onmore, query, inherited = {} }: Props = $props();

  /** Enough to fill the visible area without pretending to know the count. */
  const placeholders = [0, 1, 2, 3, 4, 5];

  /** One row's worth: the next page is on its way before these are read. */
  const next = [0, 1, 2];
</script>

{#if loading}
  <!-- A status rather than a bare div: a plain element is generic, and a
       generic element may not carry a name at all — the label was there for
       assistive technology and was being dropped on the floor by it. -->
  <div class="grid" role="status" aria-busy="true" aria-label={m['recipes.list.loading']()}>
    {#each placeholders as row (row)}
      <RecipeCardSkeleton />
    {/each}
  </div>
{:else}
  <ul class="grid">
    {#each recipes as recipe (recipe.id)}
      <li>
        <RecipeCard
          {recipe}
          {query}
          pending={pending.includes(recipe.id)}
          from={recipe.householdId ? (inherited[recipe.householdId] ?? null) : null}
        />
      </li>
    {/each}

    <!-- The end of the list, drawn as the rows that are coming. Reaching them
         is what fetches them, so there is no button to find and no moment
         where the list looks finished when it is not. -->
    {#if onmore}
      {#each next as row (row)}
        <li aria-hidden="true" {@attach whenVisible(onmore)}><RecipeCardSkeleton /></li>
      {/each}
    {/if}
  </ul>
{/if}

<style>
  .grid {
    display: grid;
    grid-template-columns: minmax(0, 1fr);
    gap: var(--layout-section-gap);
    margin: 0;
    padding: 0;
    list-style: none;
  }

  li {
    min-width: 0;
  }

  @media (min-width: 40rem) {
    .grid {
      grid-template-columns: repeat(2, minmax(0, 1fr));
    }
  }

  @media (min-width: 64rem) {
    .grid {
      grid-template-columns: repeat(3, minmax(0, 1fr));
    }
  }
</style>
