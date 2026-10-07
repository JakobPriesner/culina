import { saysAnything } from '$features/assistance/draftToRecipe';
import { drafts } from '$features/assistance/stores/drafts.svelte';
import { session } from '$features/auth/session.svelte';
import type { RecipeDraft } from '$features/recipes/editor/createRecipeDraft.svelte';
import type { Recipe } from '$features/recipes/types';

/**
 * Asking the assistant to tidy the recipe being edited, and reviewing its answer.
 *
 * Nothing is applied until the review is accepted. The answer opens a review,
 * and only what somebody ticks there reaches `change()` — which matters more in
 * this editor than it would in most, because there is no Save button: anything
 * that reached `change()` would be on its way to the server 800 ms later.
 */
export function useRecipeImprovement(editor: RecipeDraft, recipeId: () => string) {
  /**
   * Asks for the review.
   *
   * It opens on the first thing the assistant says rather than on the last: the
   * suggestion is worth reading as it is written, and thirty seconds of a
   * spinner on a button is thirty seconds of wondering. Accepting stays shut
   * until it has finished.
   */
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

  /**
   * Whether the review is open.
   *
   * While the assistant is writing, and afterwards only if it wrote something.
   * A request that failed before a word arrived used to open the review anyway
   * and say "no changes suggested" — which hid a model that was never there
   * behind a sentence about the recipe. It fails beside the button instead,
   * the same way the idea and the photograph do.
   */
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
