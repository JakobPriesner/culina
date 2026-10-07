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

export function useRecipeDeletion() {
  /** The recipe being asked about, held here because the store drops it on delete and the dialog still needs its title. */
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

    // Stay open on failure so the user can retry.
    if (ui.failure) {
      return;
    }

    // Drop the cached answers that would not be re-fetched; everything else reloads on open.
    cooking.forget(recipe.id);
    suggestions.forget(recipe.id);
    related.forget(recipe.id);

    ui.doomed = null;
    toaster.show({
      message: () => m['recipe.delete.done']({ title: recipe.title }),
      action: { label: () => m['trash.undo'](), run: () => void undoDelete(recipe.id) }
    });

    await goto(resolve('/(app)'));
  }

  return { ui, ask, cancel, confirm };
}
