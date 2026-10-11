<script lang="ts">
  import { goto } from '$app/navigation';
  import { page } from '$app/state';
  import { ErrorState } from '$ds';
  import { metaFigureOf } from '$features/nutrition/headline';
  import NutritionPanel from '$features/nutrition/NutritionPanel.svelte';
  import { nutrition } from '$features/nutrition/stores/nutrition.svelte';
  import { sharedImageSrcset, sharedImageUrl } from '$features/recipes/recipeImage';
  import { sharedRecipe } from '$features/recipes/stores/sharedRecipe.svelte';
  import RecipeSurface from '$features/recipes/surface/RecipeSurface.svelte';
  import { revealNutritionPanel } from '$features/recipes/surface/nutritionLink';
  import RecipeSurfaceSkeleton from '$features/recipes/surface/RecipeSurfaceSkeleton.svelte';
  import { urlAtYield, yieldFrom } from '$features/recipes/surface/yieldInUrl';
  import { m } from '$shell/i18n';
  import Page from '$shell/Page.svelte';

  /**
   * One recipe for a visitor without Culina: the same `RecipeSurface`, so amounts still scale, and the same nutrition panel without its corrections; props that need a kitchen are not passed.
   * No special handling of a signed-in visitor: there is no recipe id here to send them to.
   */
  const token = $derived(page.params.token ?? '');
  const recipe = $derived(sharedRecipe.recipe);
  const servings = $derived(yieldFrom(page.url, recipe));

  let nutritionOpen = $state(false);
  const nutritionFigure = $derived(metaFigureOf(nutrition.answerForShared(token)));
  const nutritionLink = $derived(
    nutritionFigure
      ? {
          label: nutritionFigure,
          ariaLabel: m['nutrition.meta.open']({
            figure: metaFigureOf(nutrition.answerForShared(token), true) ?? nutritionFigure
          }),
          onopen: showNutrition
        }
      : null
  );

  async function showNutrition() {
    nutritionOpen = true;
    await revealNutritionPanel();
  }

  $effect(() => {
    if (token) {
      void sharedRecipe.load(token);
    }
  });

  /** Scaling as on the household page, replaced not pushed so each stepper tap is not a Back step. */
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
  <!-- Sent to one person, not published: keep it out of search indexes. -->
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
      nutrition={nutritionLink}
    />

    <!-- The recipe's own id here is the token; the visitor is in no household, so nothing is correctable. -->
    <NutritionPanel {recipe} {servings} householdId={null} bind:open={nutritionOpen} readonly />
  {:else}
    <RecipeSurfaceSkeleton />
  {/if}
</Page>

<style>
  /* Quiet and above the recipe: say what kind of page this is, then be forgotten. */
  .badge {
    margin-bottom: var(--space-6);
    color: var(--text-subtle);
    font-size: var(--text-xs);
    font-weight: var(--weight-semibold);
    text-transform: uppercase;
    letter-spacing: 0.08em;
  }

  @media print {
    .badge {
      display: none;
    }
  }
</style>
