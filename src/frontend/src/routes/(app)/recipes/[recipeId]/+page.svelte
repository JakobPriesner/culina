<script lang="ts">
  import { goto, replaceState } from '$app/navigation';
  import { resolve } from '$app/paths';
  import { page } from '$app/state';
  import { Button, ErrorState, Skeleton } from '$ds';
  import RecipeSurface from '$features/recipes/surface/RecipeSurface.svelte';
  import { recipes } from '$features/recipes/stores/recipes.svelte';
  import { urlAtYield, yieldFrom } from '$features/recipes/surface/yieldInUrl';
  import { m } from '$shell/i18n';
  import Page from '$shell/Page.svelte';

  /**
   * One recipe, at rest.
   *
   * The same surface the cook route renders; only the emphasis differs. See
   * RecipeSurface for why those are one component and not two pages.
   */
  const recipeId = $derived(page.params.recipeId ?? '');
  const servings = $derived(yieldFrom(page.url, recipes.detail));

  $effect(() => {
    if (recipeId) {
      void recipes.load(recipeId);
    }
  });

  /**
   * Replaced, not pushed: scaling is a view of the recipe, and every tap of the
   * stepper becoming a back-button step would bury the page you came from.
   */
  function scale(value: number) {
    replaceState(urlAtYield(page.url, value, recipes.detail), {});
  }

  /** The yield travels with you, so cooking opens at the number you chose. */
  function startCooking() {
    const target = new URL(resolve('/(app)/recipes/[recipeId]/cook', { recipeId }), page.url);

    void goto(urlAtYield(target, servings, recipes.detail));
  }
</script>

<svelte:head>
  <title>{recipes.detail?.title ?? m['recipes.title']()}</title>
</svelte:head>

<Page>
  <p class="back">
    <a href={resolve('/(app)')}>← {m['recipe.back']()}</a>
  </p>

  {#if recipes.status === 'failed'}
    <ErrorState
      title={m['recipes.failed.title']()}
      body={m['recipes.failed.body']()}
      requestIdLabel={m['error.reference']()}
      requestId={recipes.error?.requestId}
    >
      {#snippet action()}
        <Button variant="primary" onclick={() => recipes.load(recipeId)}>
          {m['error.retry']()}
        </Button>
      {/snippet}
    </ErrorState>
  {:else if recipes.detail && recipes.detail.id === recipeId}
    <RecipeSurface
      recipe={recipes.detail}
      {servings}
      onservings={scale}
      onstartcooking={startCooking}
    />
  {:else}
    <div class="loading" aria-busy="true" aria-label={m['recipes.list.loading']()}>
      <Skeleton width="60%" height="2.5em" />
      <Skeleton width="30%" />
      <Skeleton width="100%" height="12rem" />
    </div>
  {/if}
</Page>

<style>
  .back {
    margin-bottom: var(--space-6);
    font-size: var(--text-sm);
  }

  .back a {
    color: var(--text-muted);
    text-decoration: none;
  }

  .back a:hover {
    color: var(--text);
  }

  .loading {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }
</style>
