import { http, request } from '$api';
import {
  forgetLastDraft,
  recallLastDraft,
  type LastDraft
} from '$features/recipes/editor/lastDraft';
import { toRecipe } from '$features/recipes/mappers';

/** The kept draft that is still only a title, if any; one that is gone or filled in is forgotten. */
export async function findUnfinishedDraft(
  userId: string,
  householdId: string
): Promise<LastDraft | null> {
  const kept = recallLastDraft(userId, householdId);

  if (!kept) {
    return null;
  }

  const result = await request(() =>
    http.GET('/api/v1/recipes/{recipeId}', { params: { path: { recipeId: kept.recipeId } } })
  );

  if (!result.ok) {
    // Gone, or no longer visible to this household.
    if (result.error.status === 404) {
      forgetLastDraft(userId, householdId);
    }

    return null;
  }

  const recipe = toRecipe(result.value);
  const stillEmpty =
    recipe.groups.every((group) => group.ingredients.length === 0) && recipe.steps.length === 0;

  if (stillEmpty) {
    return { recipeId: recipe.id, title: recipe.title };
  }

  // Has content now, so creation is no longer unfinished.
  forgetLastDraft(userId, householdId);

  return null;
}
