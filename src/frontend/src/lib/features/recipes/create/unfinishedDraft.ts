import { http, request } from '$api';
import {
  forgetLastDraft,
  recallLastDraft,
  type LastDraft
} from '$features/recipes/editor/lastDraft';
import { toRecipe } from '$features/recipes/mappers';

/**
 * The recipe this person started here and never wrote anything into, if any.
 *
 * A kept draft that has gone, or has since been filled in, is forgotten rather
 * than offered: creation is only unfinished while it is still nothing but its
 * title.
 */
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
    // Gone, or no longer this household's to see. Either way, not worth
    // offering back.
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

  // It has ingredients or steps now — started elsewhere, or finished here
  // and simply revisited. Either way, creation is no longer unfinished.
  forgetLastDraft(userId, householdId);

  return null;
}
