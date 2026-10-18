<script lang="ts">
  import { tick, type Snippet } from 'svelte';

  import { page } from '$app/state';
  import { Button } from '$ds';
  import AssistFailure from '$features/assistance/AssistFailure.svelte';
  import { session } from '$features/auth/session.svelte';
  import { tags } from '$features/cookbooks/stores/tags.svelte';
  import EditorNutritionLine from '$features/nutrition/EditorNutritionLine.svelte';
  import type { RecipeDraft } from '$features/recipes/editor/createRecipeDraft.svelte';
  import ConflictBanner from '$features/recipes/editor/ConflictBanner.svelte';
  import EditorLayout from '$features/recipes/editor/EditorLayout.svelte';
  import EditorRail from '$features/recipes/editor/EditorRail.svelte';
  import EditorSection from '$features/recipes/editor/EditorSection.svelte';
  import IngredientGroups from '$features/recipes/editor/IngredientGroups.svelte';
  import PhotoField from '$features/recipes/editor/PhotoField.svelte';
  import RecipeBasicsFields from '$features/recipes/editor/RecipeBasicsFields.svelte';
  import StepEditor from '$features/recipes/editor/StepEditor.svelte';
  import TagEditor from '$features/recipes/editor/TagEditor.svelte';
  import StubFill from '$features/recipes/detail/StubFill.svelte';
  import CoachTip from '$features/onboarding/CoachTip.svelte';
  import { everyIngredient } from '$features/recipes/types';
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
    /** The original it was imported from, beside the form; see `EditorLayout`. */
    source?: Snippet;
    /** Your own notes from cooking it, to fold in while editing. */
    notes?: {
      readonly overall: string;
      readonly stepNote: (stepId: string) => string;
      readonly adopt: (stepId: string) => void;
    };
  }

  let { editor, recipe, back, ingredients, improvement, source, notes }: Props = $props();

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
    { id: 'ingredients', label: m['editor.ingredients'](), count: ingredients.all.length },
    { id: 'steps', label: m['editor.steps'](), count: recipe.steps.length },
    { id: 'tags', label: m['editor.tags'](), count: recipe.tags.length }
  ]);
</script>

<!-- Visually hidden: the title field is right there and may be empty, so the heading is a sentence about the page. -->
<h1 class="ds-clipped">{m['editor.heading']({ title: recipe.title })}</h1>

<EditorLayout {source}>
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

  <!-- Only a title so far (planned before it was written down): fill it from a paste or an idea, in place. -->
  {#if recipe.steps.length === 0 && everyIngredient(recipe).length === 0}
    <div class="stub">
      <StubFill {recipe} onfill={(patch) => editor.change(patch)} />
    </div>
  {/if}

  <EditorSection id="recipe" title={m['editor.section.recipe']()}>
    {#snippet action()}
      <!-- Absent, not disabled, when the instance has no assistant or the capability is off. -->
      {#if session.user?.assistance.improve}
        <Button
          variant="secondary"
          size="sm"
          loading={improvement.improving}
          onclick={improvement.improve}
        >
          {m['assist.improve']()}
        </Button>
      {/if}
    {/snippet}

    <!-- The review dialog opens only on an answer, so a failed ask must say so here or the button just goes quiet. -->
    {#if improvement.error && !improvement.reviewing}
      <div class="assistFailure">
        <AssistFailure error={improvement.error} />
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
    <PhotoField
      recipeId={recipe.id}
      imageId={recipe.imageId}
      drawn={recipe.imageDrawn}
      onchange={editor.photoWritten}
    />
  </EditorSection>

  <EditorSection id="ingredients" title={m['editor.ingredients']()} count={ingredients.all.length}>
    <EditorNutritionLine
      {recipe}
      unsaved={editor.saveState.tone !== 'idle' && editor.saveState.tone !== 'saved'}
    />

    <IngredientGroups
      groups={ingredients.groups}
      steps={recipe.steps}
      onchange={ingredients.setGroup}
      onrename={ingredients.rename}
      onadd={ingredients.addGroup}
      onremove={ingredients.removeGroup}
      householdId={recipe.householdId}
      language={recipe.language}
      {focusId}
    />
  </EditorSection>

  <EditorSection id="steps" title={m['editor.steps']()} count={recipe.steps.length}>
    <!-- Once, where steps are written: the rules the tour's step card gives, in a line each. -->
    <CoachTip id="steps" title={m['tips.steps.title']()}>
      <ul>
        <li>{m['tour.steps.one']()}</li>
        <li>{m['tour.steps.length']()}</li>
        <li>{m['tour.steps.mention']()}</li>
        <li>{m['tour.steps.timer']()}</li>
      </ul>
    </CoachTip>

    {#if notes?.overall.trim()}
      <aside class="personal" aria-label={m['notes.editor.overall']()}>
        <span class="personal-label">{m['notes.editor.overall']()}</span>
        <p class="personal-text">{notes.overall}</p>
        <p class="personal-hint">{m['notes.editor.hint']()}</p>
      </aside>
    {/if}

    <StepEditor
      steps={recipe.steps}
      ingredients={ingredients.all}
      onchange={(steps: Step[]) => editor.change({ steps })}
      onaddingredient={ingredients.add}
      stepNote={notes?.stepNote}
      onadoptnote={notes?.adopt}
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

  .personal {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    margin-bottom: var(--space-4);
    padding: var(--space-3) var(--space-4);
    border-inline-start: 3px solid var(--accent);
    border-radius: var(--radius-sm);
    background: var(--surface-sunken);
    font-size: var(--text-sm);
  }

  .personal-label,
  .personal-hint {
    color: var(--text-muted);
  }

  .personal-label {
    font-weight: var(--weight-medium);
  }

  .personal-text,
  .personal-hint {
    margin: 0;
    white-space: pre-wrap;
  }

  .stub {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    padding: var(--space-4);
    border-radius: var(--radius-lg);
    background: var(--surface-sunken);
  }

  .finish {
    display: flex;
    justify-content: flex-start;
    padding-top: var(--space-4);
    border-top: 1px solid var(--border);
  }
</style>
