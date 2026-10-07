import { http, request, type AppError } from '$api';

/** A household's bin (deleted in the last thirty days) and restoring from it; plain functions, nothing worth keeping between visits. */

export function readTrash(householdId: string) {
  return request(() =>
    http.GET('/api/v1/households/{householdId}/trash', { params: { path: { householdId } } })
  );
}

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
