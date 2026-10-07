<script lang="ts">
  import { onDestroy } from 'svelte';

  import { resolve } from '$app/paths';
  import { page } from '$app/state';
  import { Button, ErrorState } from '$ds';
  import DraftReview from '$features/assistance/DraftReview.svelte';
  import { drafts } from '$features/assistance/stores/drafts.svelte';
  import { session } from '$features/auth/session.svelte';
  import { tags } from '$features/cookbooks/stores/tags.svelte';
  import RecipeEditForm from '$features/recipes/edit/RecipeEditForm.svelte';
  import { useIngredientEdits } from '$features/recipes/edit/useIngredientEdits.svelte';
  import { useRecipeImprovement } from '$features/recipes/edit/useRecipeImprovement.svelte';
  import { createRecipeDraft } from '$features/recipes/editor/createRecipeDraft.svelte';
  import EditorSkeleton from '$features/recipes/editor/EditorSkeleton.svelte';
  import InheritedNotice from '$features/recipes/editor/InheritedNotice.svelte';
  import { recipes } from '$features/recipes/stores/recipes.svelte';
  import { tagSuggestions } from '$features/recipes/stores/tagSuggestions.svelte';
  import { units } from '$features/recipes/stores/units.svelte';
  import { busy } from '$shell/busy.svelte';
  import { m } from '$shell/i18n';
  import NotFound from '$shell/NotFound.svelte';
  import Page from '$shell/Page.svelte';

  /**
   * Writing a recipe down.
   *
   * No Save button and no wizard: a recipe is added to for years, so there is
   * no moment at which someone is "done" and should have to say so. The work is
   * kept as it is typed and a small word says it was.
   *
   * The screen is the reading surface's other half, and is built to say so. The
   * same editorial headings name the same four parts of a recipe; the rail
   * beside them is the settings screens' rail; the amounts line up in one grid
   * the way they line up when the recipe is read back. An editor that invented
   * its own typography would be a second application bolted to the first, and
   * the seam is exactly where somebody stops trusting that what they type is
   * what will be cooked.
   */
  const recipeId = $derived(page.params.recipeId ?? '');

  const editor = createRecipeDraft(() => recipeId);
  const ingredients = useIngredientEdits(editor);
  const improvement = useRecipeImprovement(editor, () => recipeId);

  // Unsaved text on screen is not a moment to offer anybody a reload.
  const release = busy.hold();

  onDestroy(() => {
    editor.dispose();
    release();
  });

  $effect(() => {
    if (recipeId) {
      void recipes.load(recipeId);
    }
  });

  $effect(() => editor.takeLoaded());

  const draft = $derived(editor.recipe);

  // The units this kitchen uses, so a line that says "1 Schuss Milch" reads
  // back as a Schuss of milk rather than as an ingredient called "Schuss Milch".
  $effect(() => {
    if (draft) {
      void units.load(draft.householdId);
    }
  });

  // The kitchen's tags, for the tag field, and what the recipe as last saved
  // could be tagged with — asked again after every save, since a new title is
  // a new answer once the server has it.
  $effect(() => {
    if (draft) {
      void tags.load(draft.householdId);
      void tagSuggestions.load(draft.id, draft.version);
    }
  });

  const back = $derived(resolve('/(app)/recipes/[recipeId]', { recipeId }));
</script>

<svelte:head>
  <title
    >{draft?.title ??
      (editor.inheritedFrom ? recipes.detail?.title : null) ??
      m['editor.new']()}</title
  >
</svelte:head>

<Page>
  {#if editor.inheritedFrom}
    <InheritedNotice owner={session.householdName(editor.inheritedFrom)} backHref={back} />
  {:else if draft}
    <RecipeEditForm {editor} recipe={draft} {back} {ingredients} {improvement} />
  {:else if recipes.detailStatus === 'failed' && recipes.detailError?.status === 404}
    <NotFound kind="recipe" level={1} />
  {:else if recipes.detailStatus === 'failed'}
    <ErrorState
      title={m['editor.failed.title']()}
      body={m['editor.failed.body']()}
      requestIdLabel={m['error.reference']()}
      requestId={recipes.detailError?.requestId}
    >
      {#snippet action()}
        <Button variant="primary" onclick={() => recipes.load(recipeId)}>
          {m['error.retry']()}
        </Button>
      {/snippet}
    </ErrorState>
  {:else}
    <EditorSkeleton />
  {/if}
</Page>

{#if improvement.reviewing && draft}
  <DraftReview
    open={true}
    draft={drafts.draft}
    current={draft}
    writing={drafts.asking}
    error={drafts.error}
    onaccept={improvement.accept}
    onclose={() => drafts.dismiss()}
  />
{/if}
