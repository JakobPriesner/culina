<script lang="ts">
  import { formatList, m } from '$shell/i18n';
  import { whenVisible } from '$shell/whenVisible';

  import RecipeCard from './RecipeCard.svelte';
  import RecipeCardSkeleton from './RecipeCardSkeleton.svelte';
  import { inheritedFrom } from './recipeMeta';
  import { related } from './stores/related.svelte';
  import type { RelatedReason } from './types';

  /**
   * Recipes close to this one, in a grid like the library's; similarity is content (never "also cooked":
   * co-occurrence is noise at household size) and every card says why. The server ends paging where the
   * rest are far less alike than the closest; {@link mostShown} ends it in a kitchen of near twins.
   */
  interface Props {
    recipeId: string;
    /** The household reading it, whose nutrition choices the calories on the cards follow; null for the recipe's own. */
    householdId?: string | null;
    /** Households this one inherits from, by id with names; a recipe from one says so. */
    inherited?: Readonly<Record<string, string>>;
  }

  let { recipeId, householdId = null, inherited = {} }: Props = $props();

  const items = $derived(related.of(recipeId, householdId));

  /** Four rows of three: below a recipe, more is a library to browse, not a suggestion. */
  const mostShown = 12;

  const hasMore = $derived(related.hasMore(recipeId, householdId) && items.length < mostShown);

  // The page asks as soon as it knows the id; load is idempotent, so this is for use without it.
  $effect(() => {
    if (recipeId) {
      void related.load(recipeId, householdId);
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

    <ul class="grid">
      {#each items.slice(0, mostShown) as recipe (recipe.id)}
        <li>
          <RecipeCard {recipe} from={inheritedFrom(recipe, inherited)} />
          <p class="because">{because(recipe.reason)}</p>
        </li>
      {/each}

      <!-- Skeleton of the coming card; seeing it fetches it. Keyed on the count so it is watched
           afresh after each page; no margin, so it fires at the real end. -->
      {#if hasMore}
        {#key items.length}
          <li
            aria-hidden="true"
            {@attach whenVisible(() => void related.more(recipeId, householdId), '0px')}
          >
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

  .grid {
    display: grid;
    grid-template-columns: minmax(0, 1fr);
    gap: var(--space-6);
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .grid > li {
    display: flex;
    flex-direction: column;
    min-width: 0;
  }

  /* Under the card: the card is shared, and this line is the one thing only this list says. */
  .because {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  /* The library's columns, so a phone scrolls on down the page instead of sideways. */
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

  /* Paper has no use for suggestions. */
  @media print {
    .similar {
      display: none;
    }
  }
</style>
