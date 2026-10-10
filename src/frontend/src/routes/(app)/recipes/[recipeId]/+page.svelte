<script lang="ts">
  import { tick } from 'svelte';

  import { page } from '$app/state';
  import { Button, ErrorState } from '$ds';
  import { cookbooks } from '$features/cookbooks/stores/cookbooks.svelte';
  import PersonalNotePanel from '$features/cooking/PersonalNotePanel.svelte';
  import { cookLog } from '$features/cooking/stores/cookLog.svelte';
  import { cooking } from '$features/cooking/stores/cooking.svelte';
  import { notes } from '$features/cooking/stores/notes.svelte';
  import { metaFigureOf } from '$features/nutrition/headline';
  import NutritionPanel from '$features/nutrition/NutritionPanel.svelte';
  import { nutrition } from '$features/nutrition/stores/nutrition.svelte';
  import DietQuestion from '$features/recipes/DietQuestion.svelte';
  import RecipeBackLink from '$features/recipes/detail/RecipeBackLink.svelte';
  import RecipeSheets, { type RecipeSheet } from '$features/recipes/detail/RecipeSheets.svelte';
  import { useDietAnswer } from '$features/recipes/detail/useDietAnswer.svelte';
  import { useRecipeActions } from '$features/recipes/detail/useRecipeActions.svelte';
  import { useRecipeDeletion } from '$features/recipes/detail/useRecipeDeletion.svelte';
  import InheritedNote from '$features/recipes/InheritedNote.svelte';
  import SimilarRecipes from '$features/recipes/SimilarRecipes.svelte';
  import { recipes } from '$features/recipes/stores/recipes.svelte';
  import { related } from '$features/recipes/stores/related.svelte';
  import RecipeSurface from '$features/recipes/surface/RecipeSurface.svelte';
  import RecipeSurfaceSkeleton from '$features/recipes/surface/RecipeSurfaceSkeleton.svelte';
  import { yieldFrom } from '$features/recipes/surface/yieldInUrl';
  import { session } from '$features/auth/session.svelte';
  import { m } from '$shell/i18n';
  import NotFound from '$shell/NotFound.svelte';
  import Page from '$shell/Page.svelte';

  const recipeId = $derived(page.params.recipeId ?? '');
  const servings = $derived(yieldFrom(page.url, recipes.detail));

  let sheet = $state<RecipeSheet | null>(null);
  let titleInView = $state(true);
  let nutritionOpen = $state(false);

  // Only once there is a figure: no placeholder to jump over, and nothing at all when nothing was counted or it failed.
  const nutritionFigure = $derived(
    metaFigureOf(nutrition.answerFor(recipeId, session.activeHouseholdId))
  );
  const nutritionLink = $derived(
    nutritionFigure
      ? {
          label: nutritionFigure,
          ariaLabel: m['nutrition.meta.open']({ figure: nutritionFigure }),
          onopen: showNutrition
        }
      : null
  );

  /** Opens the panel and brings it into view, with the summary focused so the keyboard follows. */
  async function showNutrition() {
    nutritionOpen = true;
    await tick();

    const panel = document.getElementById('nutrition');
    const calm = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    panel?.scrollIntoView({ behavior: calm ? 'auto' : 'smooth', block: 'start' });
    panel?.querySelector('summary')?.focus({ preventScroll: true });
  }

  /** The owning household of an inherited recipe (read-only here), else null. */
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

  // The note, the attempts and the similar shelf do not wait for the recipe: they belong to the id, and their
  // components only render once the recipe has arrived.
  $effect(() => {
    if (recipeId) {
      void notes.load(recipeId);
      void cookLog.load(recipeId);
    }
  });

  // Apart from the two above: `related.load` reads its answers, so this effect reruns when they land, and
  // that must not read the note and the attempts again.
  $effect(() => {
    if (recipeId) {
      void related.load(recipeId);
    }
  });

  // Shelves for the line under the title, needed before the sheet opens.
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
  <RecipeBackLink title={recipes.detail?.title} condensed={!titleInView} />

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
      ontitleview={(visible) => (titleInView = visible)}
      cookbooks={shelves}
      nutrition={nutritionLink}
    />

    <NutritionPanel
      recipe={recipes.detail}
      {servings}
      householdId={session.activeHouseholdId}
      bind:open={nutritionOpen}
    />

    <!-- Keyed so a pending autosave is flushed for the recipe it was typed on, not the next one. -->
    {#key recipeId}
      <PersonalNotePanel {recipeId} />
    {/key}

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
