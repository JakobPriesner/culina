<script lang="ts">
  import { tick } from 'svelte';

  import { page } from '$app/state';
  import { Button } from '$ds';
  import AssistFailure from '$features/assistance/AssistFailure.svelte';
  import { drafts } from '$features/assistance/stores/drafts.svelte';
  import { session } from '$features/auth/session.svelte';
  import { tags } from '$features/cookbooks/stores/tags.svelte';
  import type { RecipeDraft } from '$features/recipes/editor/createRecipeDraft.svelte';
  import ConflictBanner from '$features/recipes/editor/ConflictBanner.svelte';
  import EditorLayout from '$features/recipes/editor/EditorLayout.svelte';
  import EditorRail from '$features/recipes/editor/EditorRail.svelte';
  import EditorSection from '$features/recipes/editor/EditorSection.svelte';
  import IngredientEditor from '$features/recipes/editor/IngredientEditor.svelte';
  import PhotoField from '$features/recipes/editor/PhotoField.svelte';
  import RecipeBasicsFields from '$features/recipes/editor/RecipeBasicsFields.svelte';
  import StepEditor from '$features/recipes/editor/StepEditor.svelte';
  import TagEditor from '$features/recipes/editor/TagEditor.svelte';
  import { tagSuggestions } from '$features/recipes/stores/tagSuggestions.svelte';
  import type { Recipe, Step } from '$features/recipes/types';
  import { m } from '$shell/i18n';
  import type { IngredientEdits } from './useIngredientEdits.svelte';
  import type { RecipeImprovement } from './useRecipeImprovement.svelte';

  interface Props {
    editor: RecipeDraft;
    recipe: Recipe;
    /** Where "done" goes. */
    back: string;
    ingredients: IngredientEdits;
    improvement: RecipeImprovement;
  }

  let { editor, recipe, back, ingredients, improvement }: Props = $props();

  // A link from the nutrition panel names what to look at: `#ingredient-<id>` or `#yield`.
  const wanted = $derived(page.url.hash.slice(1));
  const focusId = $derived(
    wanted.startsWith('ingredient-') ? wanted.slice('ingredient-'.length) : null
  );

  $effect(() => {
    if (wanted !== 'yield') {
      return;
    }

    void tick().then(() => {
      const field = document.getElementById('yield');

      field?.scrollIntoView({ block: 'center' });
      field?.focus({ preventScroll: true });
    });
  });

  const sections = $derived([
    { id: 'recipe', label: m['editor.section.recipe']() },
    { id: 'photo', label: m['editor.photo']() },
    { id: 'ingredients', label: m['editor.ingredients'](), count: ingredients.firstGroup.length },
    { id: 'steps', label: m['editor.steps'](), count: recipe.steps.length },
    { id: 'tags', label: m['editor.tags'](), count: recipe.tags.length }
  ]);
</script>

<!-- Visually hidden: the title field is right there and may be empty, so the heading is a sentence about the page. -->
<h1 class="ds-clipped">{m['editor.heading']({ title: recipe.title })}</h1>

<EditorLayout>
  {#snippet rail()}
    <EditorRail
      backHref={back}
      backLabel={m['editor.done']()}
      title={recipe.title}
      untitled={m['editor.untitled']()}
      tone={editor.saveState.tone}
      status={editor.saveState.text}
      sectionsLabel={m['editor.sections']()}
      {sections}
    />
  {/snippet}

  {#if editor.conflicted}
    <ConflictBanner onkeepmine={editor.keepMine} ontaketheirs={editor.takeTheirs} />
  {/if}

  <EditorSection id="recipe" title={m['editor.section.recipe']()}>
    {#snippet action()}
      <!-- Absent, not disabled, when the instance has no assistant or the capability is off. -->
      {#if session.user?.assistance.improve}
        <Button variant="secondary" size="sm" loading={drafts.asking} onclick={improvement.improve}>
          {m['assist.improve']()}
        </Button>
      {/if}
    {/snippet}

    <!-- The review dialog opens only on an answer, so a failed ask must say so here or the button just goes quiet. -->
    {#if drafts.error && !improvement.reviewing}
      <div class="assistFailure">
        <AssistFailure error={drafts.error} />
      </div>
    {/if}

    <RecipeBasicsFields
      {recipe}
      typed={editor.typed}
      onchange={editor.change}
      onyield={editor.writeYield}
      onminutes={editor.writeMinutes}
    />
  </EditorSection>

  <EditorSection id="photo" title={m['editor.photo']()}>
    <PhotoField recipeId={recipe.id} imageId={recipe.imageId} onchange={editor.photoWritten} />
  </EditorSection>

  <EditorSection
    id="ingredients"
    title={m['editor.ingredients']()}
    count={ingredients.firstGroup.length}
  >
    <IngredientEditor
      ingredients={ingredients.firstGroup}
      steps={recipe.steps}
      onchange={ingredients.set}
      householdId={recipe.householdId}
      language={recipe.language}
      {focusId}
    />
  </EditorSection>

  <EditorSection id="steps" title={m['editor.steps']()} count={recipe.steps.length}>
    <StepEditor
      steps={recipe.steps}
      ingredients={ingredients.all}
      onchange={(steps: Step[]) => editor.change({ steps })}
      onaddingredient={ingredients.add}
    />
  </EditorSection>

  <EditorSection id="tags" title={m['editor.tags']()} count={recipe.tags.length}>
    <TagEditor
      tags={recipe.tags}
      household={tags.items}
      suggestions={tagSuggestions.of(recipe.id, recipe.version)}
      onchange={(next) => editor.change({ tags: next })}
    />
  </EditorSection>

  <footer class="finish">
    <Button variant="primary" size="lg" href={back}>{m['editor.done']()}</Button>
  </footer>
</EditorLayout>

<style>
  /* Sits inside the section whose header holds the button, next to what failed. */
  .assistFailure {
    margin-bottom: var(--space-4);
  }

  .finish {
    display: flex;
    justify-content: flex-start;
    padding-top: var(--space-4);
    border-top: 1px solid var(--border);
  }
</style>
