<script lang="ts">
  import { formatList, m } from '$shell/i18n';
  import { whenVisible } from '$shell/whenVisible';

  import RecipeCard from './RecipeCard.svelte';
  import RecipeCardSkeleton from './RecipeCardSkeleton.svelte';
  import { inheritedFrom } from './recipeMeta';
  import { related } from './stores/related.svelte';
  import type { RelatedReason } from './types';

  /**
   * Recipes close to this one, on a shelf like the cookbook's; similarity is content (never "also cooked":
   * co-occurrence is noise at household size) and every card says why. The similarity floor ends paging.
   */
  interface Props {
    recipeId: string;
    /** Households this one inherits from, by id with names; a recipe from one says so. */
    inherited?: Readonly<Record<string, string>>;
  }

  let { recipeId, inherited = {} }: Props = $props();

  const items = $derived(related.of(recipeId));

  const hasMore = $derived(related.hasMore(recipeId));

  // The page asks as soon as it knows the id; load is idempotent, so this is for use without it.
  $effect(() => {
    if (recipeId) {
      void related.load(recipeId);
    }
  });

  const because = (reason: RelatedReason) =>
    reason.kind === 'kinds'
      ? m['related.reason.kinds']({ shared: formatList(reason.shared) })
      : m['related.reason.ingredients']({ shared: formatList(reason.shared) });
</script>

<!-- Hidden below three: two weak matches are worse than none, and as pure enhancement a failed request renders nothing. -->
{#if items.length >= 3}
  <section class="similar" aria-labelledby="similar-heading">
    <h2 id="similar-heading">{m['suggestions.similar.title']()}</h2>

    <ul class="shelf">
      {#each items as recipe (recipe.id)}
        <li>
          <RecipeCard {recipe} from={inheritedFrom(recipe, inherited)} />
          <p class="because">{because(recipe.reason)}</p>
        </li>
      {/each}

      <!-- Skeleton of the coming card; seeing it fetches it. Keyed on the count so it is watched
           afresh after each page; no margin, so it fires at the real end. -->
      {#if hasMore}
        {#key items.length}
          <li aria-hidden="true" {@attach whenVisible(() => void related.more(recipeId), '0px')}>
            <RecipeCardSkeleton />
          </li>
        {/key}
      {/if}
    </ul>
  </section>
{/if}

<style>
  .similar {
    margin-top: var(--space-12);
    padding-top: var(--space-8);
    border-top: 1px solid var(--border);
  }

  h2 {
    margin-bottom: var(--space-4);
    font-family: var(--font-editorial);
    font-size: var(--text-lg);
    font-weight: var(--weight-regular);
  }

  .shelf {
    display: grid;
    grid-auto-flow: column;
    grid-auto-columns: minmax(14rem, 1fr);
    gap: var(--space-6);
    margin: 0;
    /* Room for the focus ring, else the scroll container clips it on the first and last card. */
    padding: var(--space-1);
    overflow-x: auto;
    scroll-snap-type: x proximity;
    list-style: none;
  }

  /* Snapping fights keyboard walking: each Tab would be undone as the browser re-settles the scroll. */
  .shelf:focus-within {
    scroll-snap-type: none;
  }

  .shelf > li {
    display: flex;
    flex-direction: column;
    scroll-snap-align: start;
    min-width: 0;
  }

  /* Under the card: the card is shared, and this line is the one thing only this list says. */
  .because {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  @media (min-width: 64rem) {
    .shelf {
      grid-auto-flow: row;
      grid-auto-columns: auto;
      grid-template-columns: repeat(3, minmax(0, 1fr));
      overflow-x: visible;
    }
  }

  /* Paper has no shelf to scroll. */
  @media print {
    .similar {
      display: none;
    }
  }
</style>
