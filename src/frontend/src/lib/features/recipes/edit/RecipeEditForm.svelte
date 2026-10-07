<script lang="ts">
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
    /** The draft being edited, which the page has already checked is there. */
    recipe: Recipe;
    /** Where "done" goes: the recipe at rest. */
    back: string;
    ingredients: IngredientEdits;
    improvement: RecipeImprovement;
  }

  let { editor, recipe, back, ingredients, improvement }: Props = $props();

  const sections = $derived([
    { id: 'recipe', label: m['editor.section.recipe']() },
    { id: 'photo', label: m['editor.photo']() },
    { id: 'ingredients', label: m['editor.ingredients'](), count: ingredients.firstGroup.length },
    { id: 'steps', label: m['editor.steps'](), count: recipe.steps.length },
    { id: 'tags', label: m['editor.tags'](), count: recipe.tags.length }
  ]);
</script>

<!-- Every page says what it is. This one's title is a text field that can
     be empty and can change while it is read aloud, so the heading is a
     sentence about the page rather than the field's value. Not shown: the
     field is right there, and printing the title twice above itself is how
     a screen fills up with things nobody asked for. -->
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
      <!-- Only when the instance has an assistant and this capability is
           on. Absent rather than disabled: an instance with no model must
           look exactly like Culina looked before there was one. -->
      {#if session.user?.assistance.improve}
        <Button variant="secondary" size="sm" loading={drafts.asking} onclick={improvement.improve}>
          {m['assist.improve']()}
        </Button>
      {/if}
    {/snippet}

    <!-- An ask that failed has to say so here. The review dialog only
         opens on an answer, so without this the button spends a few
         seconds looking busy and then goes quiet — which is what a
         budget that is spent, a provider that is down and a bug all
         looked like. -->
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

  <!-- The end of the method is where somebody finishes writing, so it is
       where the way out belongs. The same words as the rail's quiet link
       above: one action, offered where each of the two hands is. -->
  <footer class="finish">
    <Button variant="primary" size="lg" href={back}>{m['editor.done']()}</Button>
  </footer>
</EditorLayout>

<style>
  /* Inside the section whose header holds the button, so the answer to
     "why did nothing happen" is next to the thing that did nothing. */
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
