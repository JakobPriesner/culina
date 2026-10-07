import type { AppError } from '$api';
import { createRecipeStore } from '$features/recipes/stores/recipes.svelte';
import { shopping } from '$features/shopping/stores/shopping.svelte';

export interface ShelfAddition {
  readonly done: number;
  readonly total: number;
}

/**
 * Adds every recipe on the shelf, one call each through the single-recipe path so merging stays in one place.
 * Reads the shelf in its own store (the on-screen list is one filtered page); a shelf not read to the end adds nothing, and a partial add is reported, not rolled back.
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
