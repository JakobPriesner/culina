import { SvelteSet } from 'svelte/reactivity';

import { withIngredients } from '$features/recipes/editor/ingredientGroups';
import type { RecipeDraft } from '$features/recipes/editor/createRecipeDraft.svelte';
import { withoutIngredients } from '$features/recipes/editor/stepUsage';
import type { Ingredient } from '$features/recipes/types';

/**
 * Changing the recipe's ingredients from the editor.
 *
 * The recipe keeps them in its first group. Groups are the "for the dough" /
 * "for the sauce" headings, and they stay invisible until a recipe needs them,
 * so the editor only ever writes to the implicit first one. The rest are
 * carried through a save untouched — this editor cannot show them yet, and a
 * recipe that arrived from an import with real headings must not lose them to
 * a keystroke.
 */
export function useIngredientEdits(editor: RecipeDraft) {
  const groups = $derived(editor.recipe?.groups);
  const firstGroup = $derived(groups?.[0]?.ingredients ?? []);

  // Derived from the groups alone, so the steps are handed the same array until
  // an ingredient changes rather than a new one on every keystroke.
  const all = $derived(groups?.flatMap((group) => group.ingredients) ?? []);

  function set(ingredients: Ingredient[]) {
    const kept = new SvelteSet(ingredients.map((one) => one.id));
    const gone = new SvelteSet(firstGroup.filter((one) => !kept.has(one.id)).map((one) => one.id));

    // A deleted line comes off the steps that needed it too. The server refuses
    // a step needing an ingredient the recipe no longer has, and being told
    // that on the next autosave is no way to find out you deleted something.
    // `change` spreads its patch, so an absent key and one set to undefined are
    // not the same thing — the steps are only named when they have changed.
    editor.change({
      groups: withIngredients(editor.recipe?.groups ?? [], ingredients),
      ...(gone.size > 0 ? { steps: withoutIngredients(editor.recipe?.steps ?? [], gone) } : {})
    });
  }

  /**
   * Adds an ingredient named from inside a step.
   *
   * With no amount: the author was writing the method, not measuring, and a
   * made-up quantity would be worse than a blank one they can fill in.
   */
  const add = (name: string) =>
    set([...firstGroup, { id: '', quantity: { value: null, unit: null }, name, note: null }]);

  return {
    get firstGroup() {
      return firstGroup;
    },
    get all() {
      return all;
    },
    set,
    add
  };
}

export type IngredientEdits = ReturnType<typeof useIngredientEdits>;
