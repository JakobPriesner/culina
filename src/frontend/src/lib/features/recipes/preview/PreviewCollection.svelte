<script lang="ts">
  import { Button, EmptyState } from '$ds';
  import { m } from '$shell/i18n';

  import RecipePreviewCard from './RecipePreviewCard.svelte';
  import type { PreviewRecipe } from './recipes';

  /** The grid of recipes the preview's filters leave, or the way back from none. */
  interface Props {
    recipes: readonly PreviewRecipe[];
    isFavourite: (id: string) => boolean;
    onopen: (recipe: PreviewRecipe) => void;
    onfavourite: (recipe: PreviewRecipe) => void;
    /** Clears whatever is narrowing the collection. */
    onreset: () => void;
  }

  let { recipes, isFavourite, onopen, onfavourite, onreset }: Props = $props();
</script>

<section class="collection" aria-labelledby="collection-title">
  <div class="collection-title">
    <h2 id="collection-title">{m['preview.collection']()}</h2>
    <span role="status">{m['preview.count']({ count: recipes.length })}</span>
  </div>
  {#if recipes.length}
    <div class="grid">
      {#each recipes as recipe (recipe.id)}<RecipePreviewCard
          {recipe}
          favourite={isFavourite(recipe.id)}
          onopen={() => onopen(recipe)}
          onfavourite={() => onfavourite(recipe)}
        />{/each}
    </div>
  {:else}
    <EmptyState title={m['preview.empty.title']()} body={m['preview.empty.body']()}
      >{#snippet action()}<Button onclick={onreset}>{m['preview.reset']()}</Button
        >{/snippet}</EmptyState
    >
  {/if}
</section>

<style>
  .collection {
    margin-top: var(--space-12);
  }

  .collection-title {
    display: flex;
    align-items: baseline;
    justify-content: space-between;
    margin-bottom: var(--space-6);
    gap: var(--space-4);
  }

  .collection-title h2 {
    font-family: var(--font-editorial);
    font-weight: var(--weight-regular);
    font-size: var(--text-2xl);
    letter-spacing: -0.025em;
  }

  .collection-title span {
    font-size: var(--text-xs);
    color: var(--text-muted);
  }

  .grid {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: var(--space-6);
  }

  @media (width < 40rem) {
    .grid {
      grid-template-columns: minmax(0, 1fr);
    }
  }

  @media (min-width: 64rem) {
    .grid {
      grid-template-columns: repeat(3, minmax(0, 1fr));
    }
  }
</style>
