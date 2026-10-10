import { SvelteSet } from 'svelte/reactivity';

import {
  shownGroups,
  withGroupName,
  withIngredients,
  withNewGroup,
  withoutEmptyGroup
} from '$features/recipes/editor/ingredientGroups';
import type { RecipeDraft } from '$features/recipes/editor/createRecipeDraft.svelte';
import { withoutIngredients } from '$features/recipes/editor/stepUsage';
import type { Ingredient } from '$features/recipes/types';

/** Edits every ingredient group: its lines, its name, and which groups there are. */
export function useIngredientEdits(editor: RecipeDraft) {
  const groups = $derived(shownGroups(editor.recipe?.groups ?? []));

  // From the groups alone, so the steps get a stable array until an ingredient changes.
  const all = $derived(groups.flatMap((group) => group.ingredients));

  /** Writes a group list, and takes any ingredient it no longer holds off the steps. */
  function write(next: ReturnType<typeof withIngredients>) {
    const kept = new SvelteSet(next.flatMap((group) => group.ingredients.map((one) => one.id)));
    const gone = new SvelteSet(
      all.filter((one) => one.id && !kept.has(one.id)).map((one) => one.id)
    );

    // A deleted ingredient comes off its steps too (the server refuses dangling needs).
    // `change` spreads the patch, so `steps` is only named when it changed.
    editor.change({
      groups: next,
      ...(gone.size > 0 ? { steps: withoutIngredients(editor.recipe?.steps ?? [], gone) } : {})
    });
  }

  /** Adds an ingredient named from inside a step, with no amount, to the last group. */
  function add(name: string) {
    const last = groups.length - 1;

    write(
      withIngredients(groups, last, [
        ...groups[last]!.ingredients,
        { id: '', quantity: { value: null, unit: null }, name, note: null }
      ])
    );
  }

  return {
    get groups() {
      return groups;
    },
    get all() {
      return all;
    },
    setGroup: (index: number, ingredients: Ingredient[]) =>
      write(withIngredients(groups, index, ingredients)),
    rename: (index: number, name: string) => write(withGroupName(groups, index, name)),
    addGroup: () => write(withNewGroup(groups)),
    removeGroup: (index: number) => write(withoutEmptyGroup(groups, index)),
    add
  };
}

export type IngredientEdits = ReturnType<typeof useIngredientEdits>;
