import type { Ingredient, IngredientGroup } from '../types';

/**
 * Puts an edited ingredient list back into a recipe's groups.
 *
 * Groups are the "for the dough" / "for the sauce" headings. The editor shows
 * one flat list, which is the first group, and has no way yet to show the
 * others — so the others have to survive the write untouched.
 *
 * They did not. The editor replaced every group with a single unnamed one, so a
 * recipe that arrived from an import with real headings lost all of them, and
 * the first one lost its name, on the first keystroke.
 */
export function withIngredients(
  groups: readonly IngredientGroup[],
  ingredients: readonly Ingredient[]
): IngredientGroup[] {
  const [edited, ...untouched] = groups;

  return [
    {
      id: edited?.id ?? null,
      // Kept, not blanked: the first group is usually the unnamed one, but a
      // recipe whose first heading is "For the dough" still has it here.
      name: edited?.name ?? null,
      ingredients: [...ingredients]
    },
    ...untouched
  ];
}
