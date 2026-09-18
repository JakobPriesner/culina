<script lang="ts">
  import { session } from '$features/auth/session.svelte';
  import { m } from '$shell/i18n';

  import RecipeCard from './RecipeCard.svelte';
  import { suggestions } from './stores/suggestions.svelte';

  /**
   * Recipes close to the one being read.
   *
   * The only place in this feature that needed new markup, because nothing on
   * the recipe page was a list of other recipes before. It borrows the
   * cookbook shelf's behaviour rather than inventing a carousel: a horizontal
   * scroller inside the page, focus revealing the whole card, and snapping
   * disabled while focus is inside it.
   *
   * Similarity is shared ingredients and tags, not "people who cooked this also
   * cooked" — with two to eight people the co-occurrence between two recipes is
   * zero or a coincidence, whereas "uses eleven of the same twelve ingredients"
   * is a fact, and one that can be explained.
   */
  interface Props {
    recipeId: string;
  }

  let { recipeId }: Props = $props();

  const householdId = $derived(session.activeHouseholdId);
  const query = $derived({ likeRecipeId: recipeId, limit: 3 });
  const items = $derived(suggestions.for(householdId, query));

  $effect(() => {
    if (householdId && recipeId) {
      void suggestions.ask(householdId, query);
    }
  });
</script>

<!--
  Nothing at all below three.

  A section that sometimes shows two weak matches is worse than one that is
  sometimes absent: the page is complete without it, so its absence costs
  nothing and its presence would have to be earned. A failed request renders
  nothing too — this is pure enhancement, and an enhancement should fail by
  leaving a whole page behind rather than by putting an error on it.
-->
{#if items.length >= 3}
  <section class="similar" aria-labelledby="similar-heading">
    <h2 id="similar-heading">{m['suggestions.similar.title']()}</h2>

    <ul class="shelf">
      {#each items as suggestion (suggestion.id)}
        <li><RecipeCard recipe={suggestion} /></li>
      {/each}
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
    /* The focus ring needs room; without the padding it is clipped by the
       scroll container on the first and last card. */
    padding: var(--space-1);
    overflow-x: auto;
    scroll-snap-type: x proximity;
    list-style: none;
  }

  /* Snapping fights a keyboard walking the shelf: each Tab would be undone by
     the browser settling the scroll position somewhere else. */
  .shelf:focus-within {
    scroll-snap-type: none;
  }

  .shelf > li {
    scroll-snap-align: start;
    min-width: 0;
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
