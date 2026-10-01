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
  import DietQuestion from '$features/recipes/DietQuestion.svelte';
  import InheritedNote from '$features/recipes/InheritedNote.svelte';
  import RecipeSurface from '$features/recipes/surface/RecipeSurface.svelte';
  import ShareRecipeSheet from '$features/recipes/ShareRecipeSheet.svelte';
  import SimilarRecipes from '$features/recipes/SimilarRecipes.svelte';
  import RecipeSurfaceSkeleton from '$features/recipes/surface/RecipeSurfaceSkeleton.svelte';
  import { presumedDiets } from '$features/recipes/stores/presumedDiets.svelte';
  import { recipes } from '$features/recipes/stores/recipes.svelte';
  import { related } from '$features/recipes/stores/related.svelte';
  import { suggestions } from '$features/recipes/stores/suggestions.svelte';
  import { session } from '$features/auth/session.svelte';
  import { shopping } from '$features/shopping/stores/shopping.svelte';
  import { restoreRecipe } from '$features/trash/trash';
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
  // the sheet. This household's shelves, which an inherited recipe can be on.
  $effect(() => {
    if (recipeId) {
      void cookbooks.loadMemberships(recipeId, session.activeHouseholdId);
    }
  });

  /**
   * Whose recipe it is, when it is not this household's own.
   *
   * An inherited recipe is read, cooked, planned and shopped for here like any
   * other; editing, deleting and publishing it are its own household's. Null
   * for a recipe of the household being looked at.
   */
  const inheritedFrom = $derived(
    recipes.detail && recipes.detail.householdId !== session.activeHouseholdId
      ? recipes.detail.householdId
      : null
  );

  const shelves = $derived(cookbooks.membershipsOf(recipeId));
  const alreadyCooking = $derived(cooking.session?.recipeId === recipeId);

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
    toaster.show({
      message: () => m['recipe.delete.done']({ title: recipe.title }),
      // The bin, from the toast: the moment somebody realises it was the
      // wrong recipe is the moment this is on screen.
      action: { label: () => m['trash.undo'](), run: () => void undoDelete(recipe.id) }
    });

    await goto(resolve('/(app)'));
  }

  async function undoDelete(recipeId: string) {
    const failure = await restoreRecipe(recipeId);

    if (failure) {
      toaster.show({ message: () => explain(failure), tone: 'danger' });

      return;
    }

    await goto(resolve('/(app)/recipes/[recipeId]', { recipeId }));
  }

  /**
   * Makes the household on screen its own copy of an inherited recipe, and
   * opens it where it can be changed — the reason anybody asks for a copy.
   */
  async function copy() {
    const householdId = session.activeHouseholdId;

    if (!householdId) {
      return;
    }

    const copied = await recipes.copy(recipeId, householdId);

    if ('code' in copied) {
      toaster.show({ message: () => explain(copied), tone: 'danger' });

      return;
    }

    toaster.show({ message: () => m['recipe.copy.done'](), tone: 'success' });

    await goto(resolve('/(app)/recipes/[recipeId]/edit', { recipeId: copied.id }));
  }

  /**
   * The diet a search only presumed this recipe keeps, when this household can
   * answer for it: an inherited recipe is its own household's to tag.
   */
  const presumed = $derived(inheritedFrom ? null : presumedDiets.of(recipeId));
  let answering = $state(false);

  /**
   * Writes the answer as a tag — the diet's own name, or its negation, which
   * the search reads as ruling the diet out — so it is never presumed again.
   */
  async function answerDiet(keeps: boolean) {
    const recipe = recipes.detail;

    if (!recipe || !presumed) {
      return;
    }

    const tag =
      presumed === 'vegan'
        ? keeps
          ? m['recipe.diet.tag.vegan']()
          : m['recipe.diet.tag.notVegan']()
        : keeps
          ? m['recipe.diet.tag.vegetarian']()
          : m['recipe.diet.tag.notVegetarian']();

    answering = true;

    const failure = await recipes.update({ ...recipe, tags: [...recipe.tags, tag] });

    answering = false;

    if (failure) {
      toaster.show({ message: () => m['recipe.diet.failed'](), tone: 'danger' });

      return;
    }

    presumedDiets.settle(recipeId);
    toaster.show({ message: () => m['recipe.diet.answered'](), tone: 'success' });
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
    <a href={resolve('/(app)')} aria-label={m['recipe.back']()}>
      <span aria-hidden="true">←</span>
      <span class="back-label">{m['recipe.back']()}</span>
    </a>
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
    {#if inheritedFrom}
      {@const owner = inheritedFrom}
      <InheritedNote
        household={session.householdName(owner)}
        onswitch={session.households.some((h) => h.householdId === owner)
          ? () => session.selectHousehold(owner)
          : undefined}
        oncopy={() => void copy()}
      />
    {/if}

    {#if presumed}
      <DietQuestion diet={presumed} busy={answering} onanswer={(keeps) => void answerDiet(keeps)} />
    {/if}

    <RecipeSurface
      recipe={recipes.detail}
      {servings}
      onservings={scale}
      onstartcooking={alreadyCooking ? undefined : startCooking}
      onaddtolist={addToShoppingList}
      onaddtoplan={() => (addingToPlan = true)}
      onaddtocookbook={() => (addingToCookbook = true)}
      onshare={inheritedFrom ? undefined : () => (sharing = true)}
      oncopy={inheritedFrom ? () => void copy() : undefined}
      ondelete={inheritedFrom
        ? undefined
        : () => {
            doomed = recipes.detail;
            deleteFailure = null;
          }}
      editable={!inheritedFrom}
      cookbooks={shelves}
    />

    <PersonalNotePanel {recipeId} />

    <SimilarRecipes {recipeId} inherited={session.inheritedFrom} />
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

  /* Keep the explicit way back that preserves the library's search state, but
     let the arrow carry it on a phone. The link's aria-label remains the full
     name, so compact is only visual. */
  @media (width < 52rem) {
    .back {
      margin-bottom: var(--space-2);
    }

    .back a {
      display: inline-grid;
      place-items: center;
      width: var(--control-sm);
      min-height: var(--control-sm);
      border: 1px solid var(--border);
      border-radius: var(--radius-full);
      background: var(--surface);
      font-size: var(--text-lg);
    }

    .back-label {
      display: none;
    }
  }

  /* Paper cannot be navigated. */
  @media print {
    .back {
      display: none;
    }
  }
</style>
