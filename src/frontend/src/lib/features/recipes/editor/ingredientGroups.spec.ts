import { describe, expect, it } from 'vitest';

import { withIngredients } from './ingredientGroups';
import type { Ingredient, IngredientGroup } from '../types';

const one = (name: string, id = name): Ingredient => ({
  id,
  quantity: { value: null, unit: null },
  name,
  note: null
});

const group = (
  id: string | null,
  name: string | null,
  ingredients: Ingredient[]
): IngredientGroup => ({ id, name, ingredients });

describe('withIngredients', () => {
  it('keeps the groups the editor cannot show', () => {
    const groups = [
      group('g1', 'For the dough', [one('flour')]),
      group('g2', 'For the sauce', [one('tomatoes')]),
      group('g3', 'To serve', [one('basil')])
    ];

    const written = withIngredients(groups, [one('flour'), one('butter')]);

    // The bug this exists for: groups 2..n used to be dropped on the first
    // keystroke, silently.
    expect(written).toHaveLength(3);
    expect(written[1]).toBe(groups[1]);
    expect(written[2]).toBe(groups[2]);
  });

  it('keeps the first group its name', () => {
    const groups = [group('g1', 'For the dough', [one('flour')])];

    expect(withIngredients(groups, [one('flour')])[0]!.name).toBe('For the dough');
  });

  it('writes the edited list into the first group', () => {
    const groups = [group('g1', null, [one('flour')]), group('g2', 'For the sauce', [])];

    const written = withIngredients(groups, [one('flour'), one('butter')]);

    expect(written[0]!.id).toBe('g1');
    expect(written[0]!.ingredients.map((i) => i.name)).toEqual(['flour', 'butter']);
  });

  it('makes a first group for a recipe that has none', () => {
    const written = withIngredients([], [one('salt')]);

    expect(written).toEqual([{ id: null, name: null, ingredients: [one('salt')] }]);
  });
});
