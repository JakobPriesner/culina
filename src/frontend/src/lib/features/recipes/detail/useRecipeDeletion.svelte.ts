import type { AppError } from '$api';
import { goto } from '$app/navigation';
import { resolve } from '$app/paths';
import { cooking } from '$features/cooking/stores/cooking.svelte';
import { recipes } from '$features/recipes/stores/recipes.svelte';
import { related } from '$features/recipes/stores/related.svelte';
import { suggestions } from '$features/recipes/stores/suggestions.svelte';
import type { Recipe } from '$features/recipes/types';
import { restoreRecipe } from '$features/trash/trash';
import { explain } from '$shell/explain';
import { m } from '$shell/i18n';
import { toaster } from '$shell/toaster.svelte';

async function undoDelete(recipeId: string) {
  const failure = await restoreRecipe(recipeId);

  if (failure) {
    toaster.show({ message: () => explain(failure), tone: 'danger' });

    return;
  }

  await goto(resolve('/(app)/recipes/[recipeId]', { recipeId }));
}

/** The question of whether to delete the recipe on screen, and what it is waiting for. */
export function useRecipeDeletion() {
  /**
   * The recipe the delete question is about, taken when it is asked.
   *
   * Held rather than read off the store, because the store lets go of the
   * recipe the moment it is deleted, and the question should not lose its
   * title in the instant before it closes.
   */
  const ui = $state({
    doomed: null as Recipe | null,
    deleting: false,
    failure: null as AppError | null
  });

  function ask(recipe: Recipe | null) {
    ui.doomed = recipe;
    ui.failure = null;
  }

  function cancel() {
    ui.doomed = null;
  }

  async function confirm() {
    const recipe = ui.doomed;

    if (!recipe || ui.deleting) {
      return;
    }

    ui.deleting = true;
    ui.failure = await recipes.remove(recipe.id, recipe.version);
    ui.deleting = false;

    // The question stays open on a failure: the recipe is still there, and
    // trying again is the likeliest next thing.
    if (ui.failure) {
      return;
    }

    // What the server deleted along with it, taken out of the answers this
    // browser keeps and would not ask for again. Everything else that showed
    // the recipe reads afresh when it is next opened.
    cooking.forget(recipe.id);
    suggestions.forget(recipe.id);
    related.forget(recipe.id);

    ui.doomed = null;
    toaster.show({
      message: () => m['recipe.delete.done']({ title: recipe.title }),
      // The bin, from the toast: the moment somebody realises it was the
      // wrong recipe is the moment this is on screen.
      action: { label: () => m['trash.undo'](), run: () => void undoDelete(recipe.id) }
    });

    await goto(resolve('/(app)'));
  }

  return { ui, ask, cancel, confirm };
}
