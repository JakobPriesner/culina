import { http, request, type AppError } from '$api';

/**
 * A household's bin: what was deleted in the last thirty days, and putting it
 * back.
 *
 * Plain functions rather than a store. The bin is read on the one screen that
 * shows it, and a restore is followed by opening the thing restored, which
 * reads it afresh — so there is nothing here worth keeping between visits.
 */

/** Everything in a household's bin, newest first. */
export function readTrash(householdId: string) {
  return request(() =>
    http.GET('/api/v1/households/{householdId}/trash', { params: { path: { householdId } } })
  );
}

/** Takes a recipe out of the bin. */
export async function restoreRecipe(recipeId: string): Promise<AppError | null> {
  const result = await request(() =>
    http.POST('/api/v1/recipes/{recipeId}/restorations', { params: { path: { recipeId } } })
  );

  return result.ok ? null : result.error;
}

/** Takes a cookbook out of the bin, with the recipes it held. */
export async function restoreCookbook(cookbookId: string): Promise<AppError | null> {
  const result = await request(() =>
    http.POST('/api/v1/cookbooks/{cookbookId}/restorations', { params: { path: { cookbookId } } })
  );

  return result.ok ? null : result.error;
}
