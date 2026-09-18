<script lang="ts">
  import { goto } from '$app/navigation';
  import { page } from '$app/state';
  import { ErrorState, Skeleton } from '$ds';
  import { sharedImageSrcset, sharedImageUrl } from '$features/recipes/recipeImage';
  import { sharedRecipe } from '$features/recipes/stores/sharedRecipe.svelte';
  import RecipeSurface from '$features/recipes/surface/RecipeSurface.svelte';
  import { urlAtYield, yieldFrom } from '$features/recipes/surface/yieldInUrl';
  import { m } from '$shell/i18n';
  import Page from '$shell/Page.svelte';

  /**
   * One recipe, to somebody who does not have Culina.
   *
   * The same `RecipeSurface` the household's own page renders, at the same
   * weighting, with the same ingredient-view switch and the same scaling. That
   * is the point of sharing a Culina recipe rather than a screenshot: the
   * amounts still move.
   *
   * What it does not offer is everything that needs a kitchen to put something
   * in — cooking, the shopping list, a cookbook shelf, the editor. Those props
   * are simply not passed, and the surface draws no bar at all rather than a
   * row of controls that would refuse.
   *
   * It deliberately does not try to be clever about a visitor who happens to be
   * signed in. There is no recipe id on this page to send them to, by design,
   * and the sender checking their own link should see exactly what they sent.
   */
  const token = $derived(page.params.token ?? '');
  const recipe = $derived(sharedRecipe.recipe);
  const servings = $derived(yieldFrom(page.url, recipe));

  $effect(() => {
    if (token) {
      void sharedRecipe.load(token);
    }
  });

  /**
   * Scaling, exactly as on the household's own page.
   *
   * Replaced rather than pushed, for the same reason: every tap of the stepper
   * becoming a back-button step would bury the message the link came from.
   */
  function scale(value: number) {
    void goto(urlAtYield(page.url, value, recipe), {
      replaceState: true,
      keepFocus: true,
      noScroll: true
    });
  }
</script>

<svelte:head>
  <title>{recipe?.title ?? m['shared.badge']()}</title>
  <!-- A link is sent to one person, not published. Nothing here should end up
       in a search index, whatever a crawler was handed. -->
  <meta name="robots" content="noindex, nofollow" />
</svelte:head>

<Page>
  {#if sharedRecipe.status === 'failed'}
    <ErrorState title={m['shared.failed.title']()} body={m['shared.failed.body']()} />
  {:else if recipe}
    <p class="badge">{m['shared.badge']()}</p>

    <RecipeSurface
      {recipe}
      {servings}
      onservings={scale}
      photo={{ src: sharedImageUrl(token, 1600), srcset: sharedImageSrcset(token) }}
    />
  {:else}
    <div class="loading" aria-busy="true" aria-label={m['shared.loading']()}>
      <Skeleton width="60%" height="2.5em" />
      <Skeleton width="30%" />
      <Skeleton width="100%" height="12rem" />
    </div>
  {/if}
</Page>

<style>
  /* Quiet, and above the recipe rather than over it: a visitor should know what
     kind of page this is before they start reading, and then forget it. */
  .badge {
    margin-bottom: var(--space-6);
    color: var(--text-subtle);
    font-size: var(--text-xs);
    font-weight: var(--weight-semibold);
    text-transform: uppercase;
    letter-spacing: 0.08em;
  }

  .loading {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }

  @media print {
    .badge {
      display: none;
    }
  }
</style>
