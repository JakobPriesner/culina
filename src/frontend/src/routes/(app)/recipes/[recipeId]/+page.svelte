<script lang="ts">
  import { page } from '$app/state';
  import { Button, ErrorState } from '$ds';
  import { cookbooks } from '$features/cookbooks/stores/cookbooks.svelte';
  import PersonalNotePanel from '$features/cooking/PersonalNotePanel.svelte';
  import { cooking } from '$features/cooking/stores/cooking.svelte';
  import DietQuestion from '$features/recipes/DietQuestion.svelte';
  import RecipeBackLink from '$features/recipes/detail/RecipeBackLink.svelte';
  import RecipeSheets, { type RecipeSheet } from '$features/recipes/detail/RecipeSheets.svelte';
  import { useDietAnswer } from '$features/recipes/detail/useDietAnswer.svelte';
  import { useRecipeActions } from '$features/recipes/detail/useRecipeActions.svelte';
  import { useRecipeDeletion } from '$features/recipes/detail/useRecipeDeletion.svelte';
  import InheritedNote from '$features/recipes/InheritedNote.svelte';
  import SimilarRecipes from '$features/recipes/SimilarRecipes.svelte';
  import { recipes } from '$features/recipes/stores/recipes.svelte';
  import RecipeSurface from '$features/recipes/surface/RecipeSurface.svelte';
  import RecipeSurfaceSkeleton from '$features/recipes/surface/RecipeSurfaceSkeleton.svelte';
  import { yieldFrom } from '$features/recipes/surface/yieldInUrl';
  import { session } from '$features/auth/session.svelte';
  import { m } from '$shell/i18n';
  import NotFound from '$shell/NotFound.svelte';
  import Page from '$shell/Page.svelte';

  /**
   * One recipe, at rest.
   *
   * The same surface the cook route renders; only the emphasis differs. See
   * RecipeSurface for why those are one component and not two pages.
   */
  const recipeId = $derived(page.params.recipeId ?? '');
  const servings = $derived(yieldFrom(page.url, recipes.detail));

  /** The sheet that is up over the recipe, if any. */
  let sheet = $state<RecipeSheet | null>(null);

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

  const actions = useRecipeActions({
    recipeId: () => recipeId,
    servings: () => servings,
    householdId: () => session.activeHouseholdId
  });
  const deletion = useRecipeDeletion();
  const diet = useDietAnswer({ recipeId: () => recipeId, inherited: () => inheritedFrom !== null });

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
</script>

<svelte:head>
  <title>{recipes.detail?.title ?? m['recipes.title']()}</title>
</svelte:head>

<Page>
  <RecipeBackLink />

  {#if recipes.detailStatus === 'failed' && recipes.detailError?.status === 404}
    <NotFound kind="recipe" level={1} />
  {:else if recipes.detailStatus === 'failed'}
    <ErrorState
      title={m['recipes.failed.title']()}
      body={m['recipes.failed.body']()}
      requestIdLabel={m['error.reference']()}
      requestId={recipes.detailError?.requestId}
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
        oncopy={() => void actions.copy()}
      />
    {/if}

    {#if diet.presumed}
      <DietQuestion
        diet={diet.presumed}
        busy={diet.answering}
        onanswer={(keeps) => void diet.answer(keeps)}
      />
    {/if}

    <RecipeSurface
      recipe={recipes.detail}
      {servings}
      onservings={actions.scale}
      onstartcooking={alreadyCooking ? undefined : actions.startCooking}
      onaddtolist={actions.addToShoppingList}
      onaddtoplan={() => (sheet = 'plan')}
      onaddtocookbook={() => (sheet = 'cookbook')}
      onshare={inheritedFrom ? undefined : () => (sheet = 'share')}
      oncopy={inheritedFrom ? () => void actions.copy() : undefined}
      ondelete={inheritedFrom ? undefined : () => deletion.ask(recipes.detail)}
      editable={!inheritedFrom}
      cookbooks={shelves}
    />

    <PersonalNotePanel {recipeId} />

    <SimilarRecipes {recipeId} inherited={session.inheritedFrom} />
  {:else}
    <RecipeSurfaceSkeleton />
  {/if}
</Page>

<RecipeSheets
  {recipeId}
  title={recipes.detail?.title ?? ''}
  householdId={session.activeHouseholdId}
  {servings}
  bind:open={sheet}
  {deletion}
/>
