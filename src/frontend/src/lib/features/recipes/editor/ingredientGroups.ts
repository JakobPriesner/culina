import type { Ingredient, IngredientGroup } from '../types';

/** Puts an edited flat ingredient list back into a recipe's groups, leaving the other groups (imported headings) untouched; the editor only shows the first. */
export function withIngredients(
  groups: readonly IngredientGroup[],
  ingredients: readonly Ingredient[]
): IngredientGroup[] {
  const [edited, ...untouched] = groups;

  return [
    {
      id: edited?.id ?? null,
      // Kept, not blanked: a recipe whose first heading is "For the dough" still has it here.
      name: edited?.name ?? null,
      ingredients: [...ingredients]
    },
    ...untouched
  ];
}
