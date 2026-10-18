<script lang="ts">
  import { IconButton } from '$ds';
  import { m } from '$shell/i18n';
  import { whenVisible } from '$shell/whenVisible';

  import RecipeCard from './RecipeCard.svelte';
  import RecipeCardSkeleton from './RecipeCardSkeleton.svelte';
  import { inheritedFrom } from './recipeMeta';
  import { recipeLayout } from './stores/recipeLayout.svelte';
  import type { RecipeSummary } from './types';

  /**
   * The recipes as columns of cards (three wide, more on large screens), and on a phone as a list of rows or,
   * when the reader browses by photo, one card under another.
   */
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
    /** Takes a recipe out of the collection on screen (a chosen cookbook), from its card. */
    onremove?: (recipe: RecipeSummary) => void;
    /** The remove button's name for one recipe, said by the caller who knows what it is removed from. */
    removeLabel?: (recipe: RecipeSummary) => string;
  }

  let {
    recipes,
    loading = false,
    pending = [],
    onmore,
    query,
    inherited = {},
    onremove,
    removeLabel
  }: Props = $props();

  /** Enough to fill the visible area without pretending to know the count. */
  const placeholders = [0, 1, 2, 3, 4, 5];

  /** The cards in the first screenful of a phone; their photos are fetched ahead of the rest. */
  const firstRow = 2;

  /** One row's worth: the next page is on its way before these are read. */
  const next = [0, 1, 2];

  const photo = $derived(recipeLayout.current === 'photos');
</script>

{#if loading}
  <!-- role=status: a plain element is generic and may not carry a name, so the label was dropped. -->
  <div
    class="grid"
    class:rows={!photo}
    role="status"
    aria-busy="true"
    aria-label={m['recipes.list.loading']()}
  >
    {#each placeholders as row (row)}
      <RecipeCardSkeleton {photo} />
    {/each}
  </div>
{:else}
  <ul class="grid" class:rows={!photo}>
    {#each recipes as recipe, index (recipe.id)}
      <li class="card">
        <RecipeCard
          {recipe}
          {query}
          pending={pending.includes(recipe.id)}
          from={inheritedFrom(recipe, inherited)}
          priority={index < firstRow}
          {photo}
        />
        {#if onremove}
          <span class="remove">
            <IconButton
              label={removeLabel?.(recipe) ?? recipe.title}
              size="sm"
              bordered
              onclick={() => onremove(recipe)}
            >
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <path d="m6 6 12 12M18 6 6 18" stroke-linecap="round" />
              </svg>
            </IconButton>
          </span>
        {/if}
      </li>
    {/each}

    <!-- Skeleton of the coming rows; reaching them fetches them, so there is no button and no false
         "finished" look. -->
    {#if onmore}
      {#each next as row (row)}
        <li aria-hidden="true" {@attach whenVisible(onmore)}><RecipeCardSkeleton {photo} /></li>
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
  /* Over the card's corner, on the surface so it reads over a photo too. */
  .remove {
    position: absolute;
    top: var(--space-3);
    right: var(--space-3);
    z-index: 1;
    border-radius: var(--radius-full);
    background: var(--surface-overlay);
  }

  .card {
    position: relative;
    content-visibility: auto;
    contain-intrinsic-size: auto 14rem;
    padding: var(--space-2);
    margin: calc(var(--space-2) * -1);
  }

  /* Rows on a phone sit closer than cards do, and are shorter while off screen. */
  @media (width < 40rem) {
    .rows {
      gap: var(--space-4);
    }

    .rows .card {
      contain-intrinsic-size: auto 5rem;
    }
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

  /* Wider pages fit more cards, not bigger ones. */
  @media (min-width: 100rem) {
    .grid {
      grid-template-columns: repeat(auto-fill, minmax(20rem, 1fr));
    }
  }
</style>
