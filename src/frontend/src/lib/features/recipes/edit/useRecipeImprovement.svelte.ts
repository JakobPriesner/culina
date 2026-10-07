import { saysAnything } from '$features/assistance/draftToRecipe';
import { drafts } from '$features/assistance/stores/drafts.svelte';
import { session } from '$features/auth/session.svelte';
import type { RecipeDraft } from '$features/recipes/editor/createRecipeDraft.svelte';
import type { Recipe } from '$features/recipes/types';

/**
 * Asks the assistant to tidy the recipe being edited and reviews its answer.
 * Only accepted parts reach `change()`, since the editor autosaves ~800 ms later.
 */
export function useRecipeImprovement(editor: RecipeDraft, recipeId: () => string) {
  /** Asks for a review; it opens on the first streamed words, and accepting waits until it finishes. */
  async function improve(): Promise<void> {
    const draft = editor.recipe;

    if (!draft || !session.activeHouseholdId) {
      return;
    }

    await drafts.ask({
      kind: 'revision',
      householdId: session.activeHouseholdId,
      recipeId: recipeId(),
      language: draft.language
    });
  }

  // Open while writing, then only if something was written, so a failed request fails beside the button.
  const reviewing = $derived(drafts.asking || saysAnything(drafts.draft));

  /** Folds the accepted parts in as one change, so it is one save. */
  function accept(patch: Partial<Recipe>): void {
    editor.change(patch);
    drafts.dismiss();
  }

  return {
    get reviewing() {
      return reviewing;
    },
    improve,
    accept
  };
}

export type RecipeImprovement = ReturnType<typeof useRecipeImprovement>;
