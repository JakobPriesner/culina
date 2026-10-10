import type { Ingredient, IngredientGroup } from '../types';

/** The group a recipe without one is shown with; the server makes the same one. */
const implicit: IngredientGroup = { id: null, name: null, ingredients: [] };

/** The groups as the editor shows them: never none, so there is always a list to write into. */
export const shownGroups = (groups: readonly IngredientGroup[]): readonly IngredientGroup[] =>
  groups.length > 0 ? groups : [implicit];

/** Puts an edited ingredient list into the group at `index`, leaving the others as they are. */
export function withIngredients(
  groups: readonly IngredientGroup[],
  index: number,
  ingredients: readonly Ingredient[]
): IngredientGroup[] {
  return shownGroups(groups).map((group, at) =>
    at === index ? { ...group, ingredients: [...ingredients] } : group
  );
}

/** Renames the group at `index`; a cleared name makes it unnamed, not gone. */
export function withGroupName(
  groups: readonly IngredientGroup[],
  index: number,
  name: string
): IngredientGroup[] {
  return shownGroups(groups).map((group, at) =>
    at === index ? { ...group, name: name === '' ? null : name } : group
  );
}

/** A new empty group at the end; it has no id until the server gives it one. */
export const withNewGroup = (groups: readonly IngredientGroup[]): IngredientGroup[] => [
  ...shownGroups(groups),
  { ...implicit }
];

/** Drops the group at `index` if it is empty, so removing it can never lose an ingredient. */
export const withoutEmptyGroup = (
  groups: readonly IngredientGroup[],
  index: number
): IngredientGroup[] =>
  shownGroups(groups).filter((group, at) => at !== index || group.ingredients.length > 0);
