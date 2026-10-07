import type { AppError } from '$api';
import { createRecipeStore } from '$features/recipes/stores/recipes.svelte';
import { shopping } from '$features/shopping/stores/shopping.svelte';

export interface ShelfAddition {
  readonly done: number;
  readonly total: number;
}

/**
 * The whole shelf, onto the shopping list.
 *
 * The shelf is read from the server page by page, in a list of its own: the one
 * on screen is only its first page, narrowed by whatever is typed in the search
 * box, and neither is "the shelf". Reading it through the recipe list, with the
 * shelf as the only filter, is also what makes a shelf that fills itself come
 * out complete — its recipes are whatever its rules match, and only the server
 * can say which those are.
 *
 * One call per recipe, through the path a single recipe already takes. A
 * second endpoint that merged a shelf at once would be a second place for
 * merging to behave differently, and merging is the entire value of the list.
 * It is also why a partial failure is reported rather than rolled back: some
 * of it is genuinely on the list, and somebody may be reading it in a shop.
 *
 * A shelf that could not be read to its end is an error and adds nothing:
 * "added the shelf" over half of it is the lie this exists to avoid.
 */
export async function addShelfToShoppingList(
  householdId: string,
  cookbookId: string
): Promise<ShelfAddition | AppError> {
  const shelf = createRecipeStore();
  const filters = { cookbookId };

  await shelf.list(householdId, filters);

  while (shelf.status === 'ready' && shelf.hasMore && !shelf.moreFailed) {
    await shelf.loadMore(householdId, filters);
  }

  if (shelf.error && (shelf.status === 'failed' || shelf.moreFailed)) {
    return shelf.error;
  }

  let done = 0;

  for (const recipe of shelf.items) {
    const failure = await shopping.addRecipe(householdId, recipe.id, recipe.yieldAmount);

    if (!failure) {
      done += 1;
    }
  }

  return { done, total: shelf.items.length };
}
