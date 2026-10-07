import { SvelteSet } from 'svelte/reactivity';

import { withIngredients } from '$features/recipes/editor/ingredientGroups';
import type { RecipeDraft } from '$features/recipes/editor/createRecipeDraft.svelte';
import { withoutIngredients } from '$features/recipes/editor/stepUsage';
import type { Ingredient } from '$features/recipes/types';

/** Ingredient edits write only to the implicit first group; other (imported) groups pass through a save untouched. */
export function useIngredientEdits(editor: RecipeDraft) {
  const groups = $derived(editor.recipe?.groups);
  const firstGroup = $derived(groups?.[0]?.ingredients ?? []);

  // From the groups alone, so the steps get a stable array until an ingredient changes.
  const all = $derived(groups?.flatMap((group) => group.ingredients) ?? []);

  function set(ingredients: Ingredient[]) {
    const kept = new SvelteSet(ingredients.map((one) => one.id));
    const gone = new SvelteSet(firstGroup.filter((one) => !kept.has(one.id)).map((one) => one.id));

    // A deleted ingredient comes off its steps too (the server refuses dangling needs).
    // `change` spreads the patch, so `steps` is only named when it changed.
    editor.change({
      groups: withIngredients(editor.recipe?.groups ?? [], ingredients),
      ...(gone.size > 0 ? { steps: withoutIngredients(editor.recipe?.steps ?? [], gone) } : {})
    });
  }

  /** Adds an ingredient named from inside a step, with no amount. */
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
