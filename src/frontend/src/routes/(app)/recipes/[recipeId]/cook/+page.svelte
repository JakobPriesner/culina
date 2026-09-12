<script lang="ts">
  import { goto, replaceState } from '$app/navigation';
  import { resolve } from '$app/paths';
  import { page } from '$app/state';
  import { Skeleton } from '$ds';
  import RecipeSurface from '$features/recipes/surface/RecipeSurface.svelte';
  import { recipes } from '$features/recipes/stores/recipes.svelte';
  import { urlAtYield, yieldFrom } from '$features/recipes/surface/yieldInUrl';
  import { m } from '$shell/i18n';
  import Page from '$shell/Page.svelte';

  /**
   * The same recipe, being cooked.
   *
   * A separate route rather than a flag on the page, so cooking has a URL: it
   * survives a reload, it can be resumed on the phone propped against the
   * bowl, and the back button means what it looks like it means.
   */
  const recipeId = $derived(page.params.recipeId ?? '');

  let currentStep = $state(0);

  const servings = $derived(yieldFrom(page.url, recipes.detail));

  function scale(value: number) {
    replaceState(urlAtYield(page.url, value, recipes.detail), {});
  }

  $effect(() => {
    if (recipeId) {
      void recipes.load(recipeId);
    }
  });

  function stopCooking() {
    const target = new URL(resolve('/(app)/recipes/[recipeId]', { recipeId }), page.url);

    void goto(urlAtYield(target, servings, recipes.detail));
  }
</script>

<svelte:head>
  <title>{recipes.detail?.title ?? m['recipes.title']()}</title>
</svelte:head>

<Page>
  {#if recipes.detail && recipes.detail.id === recipeId}
    <RecipeSurface
      recipe={recipes.detail}
      emphasis="cook"
      {servings}
      onservings={scale}
      {currentStep}
      onstep={(index) => (currentStep = index)}
      onstopcooking={stopCooking}
    />
  {:else}
    <div aria-busy="true" aria-label={m['recipes.list.loading']()}>
      <Skeleton width="100%" height="12rem" />
    </div>
  {/if}
</Page>
