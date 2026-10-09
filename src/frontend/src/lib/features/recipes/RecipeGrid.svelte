<script lang="ts">
  import { m } from '$shell/i18n';
  import { whenVisible } from '$shell/whenVisible';

  import RecipeCard from './RecipeCard.svelte';
  import RecipeCardSkeleton from './RecipeCardSkeleton.svelte';
  import { inheritedFrom } from './recipeMeta';
  import type { RecipeSummary } from './types';

  /** The recipes as columns of text rows (three wide, one on a phone); only the column count changes. */
  interface Props {
    recipes: readonly RecipeSummary[];
    /** Drawn instead of the recipes, when there are none yet to draw. */
    loading?: boolean;
    /** Ids whose change is in flight. */
    pending?: readonly string[];
    /**
     * Asked for the next page when the list end comes into view; given only while there is one, and
     * must tolerate being called while its request runs.
     */
    onmore?: () => void;
    /** What was searched for, marked in each result it found. */
    query?: string;
    /** Households this one inherits from, by id with names; a recipe from one says so. */
    inherited?: Readonly<Record<string, string>>;
  }

  let { recipes, loading = false, pending = [], onmore, query, inherited = {} }: Props = $props();

  /** Enough to fill the visible area without pretending to know the count. */
  const placeholders = [0, 1, 2, 3, 4, 5];

  /** The cards in the first screenful of a phone; their photos are fetched ahead of the rest. */
  const firstRow = 2;

  /** One row's worth: the next page is on its way before these are read. */
  const next = [0, 1, 2];
</script>

{#if loading}
  <!-- role=status: a plain element is generic and may not carry a name, so the label was dropped. -->
  <div class="grid" role="status" aria-busy="true" aria-label={m['recipes.list.loading']()}>
    {#each placeholders as row (row)}
      <RecipeCardSkeleton />
    {/each}
  </div>
{:else}
  <ul class="grid">
    {#each recipes as recipe, index (recipe.id)}
      <li class="card">
        <RecipeCard
          {recipe}
          {query}
          pending={pending.includes(recipe.id)}
          from={inheritedFrom(recipe, inherited)}
          priority={index < firstRow}
        />
      </li>
    {/each}

    <!-- Skeleton of the coming rows; reaching them fetches them, so there is no button and no false
         "finished" look. -->
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

  /* Off-screen cards aren't laid out or painted; that clips to the box, so the padding (given back by
     the negative margin) leaves room for the focus ring. */
  .card {
    content-visibility: auto;
    contain-intrinsic-size: auto 14rem;
    padding: var(--space-2);
    margin: calc(var(--space-2) * -1);
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
