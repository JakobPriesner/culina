import { describe, expect, it } from 'vitest';

import type { RecipeDraft } from '$features/recipes/editor/createRecipeDraft.svelte';
import type { Ingredient, Recipe, Step } from '$features/recipes/types';

import { useIngredientEdits } from './useIngredientEdits.svelte';

const one = (name: string, id = name): Ingredient => ({
  id,
  quantity: { value: null, unit: null },
  name,
  note: null
});

const step = (uses: string[]): Step => ({
  id: 's',
  title: null,
  segments: [],
  uses,
  durationSeconds: null
});

/** Just enough of the draft: the recipe it holds and the `change` that writes to it. */
function editing(groups: Recipe['groups'], steps: Step[] = []) {
  const held = { recipe: { groups, steps } as unknown as Recipe };
  const editor = {
    get recipe() {
      return held.recipe;
    },
    change: (patch: Partial<Recipe>) => {
      held.recipe = { ...held.recipe, ...patch };
    }
  } as unknown as RecipeDraft;

  return { held, edits: useIngredientEdits(editor) };
}

const twoGroups = (): Recipe['groups'] => [
  { id: 'g1', name: 'For the dough', ingredients: [one('flour'), one('salt')] },
  { id: 'g2', name: 'For the filling', ingredients: [one('apples')] }
];

describe('ingredient edits across groups', () => {
  it('edits a line in the second group and leaves the first alone', () => {
    const { held, edits } = editing(twoGroups());
    const first = held.recipe.groups[0];

    edits.setGroup(1, [{ ...one('apples'), note: 'diced' }]);

    expect(held.recipe.groups[0]).toBe(first);
    expect(held.recipe.groups[1]!.ingredients[0]!.note).toBe('diced');
  });

  it('adds a line to the group asked for', () => {
    const { held, edits } = editing(twoGroups());

    edits.setGroup(1, [one('apples'), one('', '')]);

    expect(held.recipe.groups[0]!.ingredients).toHaveLength(2);
    expect(held.recipe.groups[1]!.ingredients).toHaveLength(2);
  });

  it('puts an ingredient named from a step in the last group', () => {
    const { held, edits } = editing(twoGroups());

    edits.add('cinnamon');

    expect(held.recipe.groups[1]!.ingredients.map((i) => i.name)).toEqual(['apples', 'cinnamon']);
  });

  it('renames a group, and a cleared name keeps the group', () => {
    const { held, edits } = editing(twoGroups());

    edits.rename(1, 'Filling');
    expect(held.recipe.groups[1]!.name).toBe('Filling');

    edits.rename(1, '');
    expect(held.recipe.groups).toHaveLength(2);
    expect(held.recipe.groups[1]!.name).toBeNull();
  });

  it('adds a group at the end, and removes it again while it is empty', () => {
    const { held, edits } = editing(twoGroups());

    edits.addGroup();
    expect(held.recipe.groups).toHaveLength(3);

    edits.removeGroup(2);
    expect(held.recipe.groups.map((g) => g.id)).toEqual(['g1', 'g2']);
  });

  it('does not remove a group that has lines', () => {
    const { held, edits } = editing(twoGroups());

    edits.removeGroup(1);

    expect(held.recipe.groups).toHaveLength(2);
  });

  it('takes a deleted line in the second group off the steps', () => {
    const { held, edits } = editing(twoGroups(), [step(['flour', 'apples']), step(['salt'])]);

    edits.setGroup(1, []);

    expect(held.recipe.steps.map((s) => s.uses)).toEqual([['flour'], ['salt']]);
  });

  it('lists the ingredients of every group for the steps', () => {
    const { edits } = editing(twoGroups());

    expect(edits.all.map((i) => i.name)).toEqual(['flour', 'salt', 'apples']);
  });
});
