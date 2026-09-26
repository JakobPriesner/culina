<script lang="ts">
  import { goto } from '$app/navigation';
  import { resolve } from '$app/paths';
  import { page } from '$app/state';
  import { Button, ErrorState } from '$ds';
  import AddToCookbookSheet from '$features/cookbooks/AddToCookbookSheet.svelte';
  import { cookbooks } from '$features/cookbooks/stores/cookbooks.svelte';
  import PersonalNotePanel from '$features/cooking/PersonalNotePanel.svelte';
  import { cooking } from '$features/cooking/stores/cooking.svelte';
  import PlanRecipeSheet from '$features/planning/PlanRecipeSheet.svelte';
  import DeleteRecipeDialog from '$features/recipes/DeleteRecipeDialog.svelte';
  import RecipeSurface from '$features/recipes/surface/RecipeSurface.svelte';
  import ShareRecipeSheet from '$features/recipes/ShareRecipeSheet.svelte';
  import SimilarRecipes from '$features/recipes/SimilarRecipes.svelte';
  import RecipeSurfaceSkeleton from '$features/recipes/surface/RecipeSurfaceSkeleton.svelte';
  import { recipes } from '$features/recipes/stores/recipes.svelte';
  import { related } from '$features/recipes/stores/related.svelte';
  import { suggestions } from '$features/recipes/stores/suggestions.svelte';
  import { session } from '$features/auth/session.svelte';
  import { shopping } from '$features/shopping/stores/shopping.svelte';
  import { toaster } from '$shell/toaster.svelte';
  import type { Recipe } from '$features/recipes/types';
  import type { AppError } from '$api';
  import { urlAtYield, yieldFrom } from '$features/recipes/surface/yieldInUrl';
  import { explain } from '$shell/explain';
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

  let addingToCookbook = $state(false);
  let addingToPlan = $state(false);
  let sharing = $state(false);

  /**
   * The recipe the delete question is about, taken when it is asked.
   *
   * Held rather than read off the store, because the store lets go of the
   * recipe the moment it is deleted, and the question should not lose its
   * title in the instant before it closes.
   */
  let doomed = $state<Recipe | null>(null);
  let deleting = $state(false);
  let deleteFailure = $state<AppError | null>(null);

  $effect(() => {
    if (recipeId) {
      void recipes.load(recipeId);
    }
  });

  // Which shelves it is on, for the line under the title. Asked here rather
  // than by the sheet alone, because the line is visible before anybody opens
  // the sheet.
  $effect(() => {
    if (recipeId) {
      void cookbooks.loadMemberships(recipeId);
    }
  });

  const shelves = $derived(cookbooks.membershipsOf(recipeId));

  /**
   * Replaced, not pushed: scaling is a view of the recipe, and every tap of the
   * stepper becoming a back-button step would bury the page you came from.
   *
   * `goto`, not `replaceState`. `replaceState` is for shallow routing — state
   * the page carries without the URL meaning anything different — so it changes
   * the address bar and tells nothing on screen that anything happened. The
   * yield is not shallow: it is what every amount on the page is derived from,
   * and a stepper that silently moved the address bar and left the amounts
   * alone is exactly the quiet wrongness this app exists to avoid.
   */
  function scale(value: number) {
    void goto(urlAtYield(page.url, value, recipes.detail), {
      replaceState: true,
      // The thumb is still on the stepper and the eye is on the ingredient
      // list; neither should be moved by a number changing.
      keepFocus: true,
      noScroll: true
    });
  }

  /**
   * Puts the ingredients on the list at the scaling on screen.
   *
   * The scaling matters: adding a recipe you have scaled to six and getting the
   * amounts for four is the kind of quiet wrongness nobody notices until they
   * are short of butter.
   */
  async function addToShoppingList() {
    const householdId = session.activeHouseholdId;

    if (!householdId) {
      return;
    }

    const failure = await shopping.addRecipe(householdId, recipeId, servings);

    toaster.show({
      message: () => (failure ? explain(failure) : m['shopping.added']()),
      tone: failure ? 'danger' : 'success'
    });
  }

  async function remove() {
    const recipe = doomed;

    if (!recipe || deleting) {
      return;
    }

    deleting = true;
    deleteFailure = await recipes.remove(recipe.id, recipe.version);
    deleting = false;

    // The question stays open on a failure: the recipe is still there, and
    // trying again is the likeliest next thing.
    if (deleteFailure) {
      return;
    }

    // What the server deleted along with it, taken out of the answers this
    // browser keeps and would not ask for again. Everything else that showed
    // the recipe reads afresh when it is next opened.
    cooking.forget(recipe.id);
    suggestions.forget(recipe.id);
    related.forget(recipe.id);

    doomed = null;
    toaster.show({ message: () => m['recipe.delete.done']({ title: recipe.title }) });

    await goto(resolve('/(app)'));
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
      onaddtolist={addToShoppingList}
      onaddtoplan={() => (addingToPlan = true)}
      onaddtocookbook={() => (addingToCookbook = true)}
      onshare={() => (sharing = true)}
      ondelete={() => {
        doomed = recipes.detail;
        deleteFailure = null;
      }}
      editable
      cookbooks={shelves}
    />

    <PersonalNotePanel {recipeId} />

    <SimilarRecipes {recipeId} />
  {:else}
    <RecipeSurfaceSkeleton />
  {/if}
</Page>

<DeleteRecipeDialog
  open={doomed !== null}
  title={doomed?.title ?? ''}
  {deleting}
  error={deleteFailure}
  onconfirm={() => void remove()}
  onclose={() => (doomed = null)}
/>

<ShareRecipeSheet
  open={sharing}
  {recipeId}
  title={recipes.detail?.title ?? ''}
  onclose={() => (sharing = false)}
/>

{#if session.activeHouseholdId}
  <PlanRecipeSheet
    open={addingToPlan}
    householdId={session.activeHouseholdId}
    {recipeId}
    title={recipes.detail?.title ?? ''}
    {servings}
    onclose={() => (addingToPlan = false)}
  />

  <AddToCookbookSheet
    open={addingToCookbook}
    householdId={session.activeHouseholdId}
    {recipeId}
    onclose={() => (addingToCookbook = false)}
  />
{/if}

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

  /* Paper cannot be navigated. */
  @media print {
    .back {
      display: none;
    }
  }
</style>
