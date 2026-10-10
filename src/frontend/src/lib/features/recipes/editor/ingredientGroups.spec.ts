import { describe, expect, it } from 'vitest';

import {
  shownGroups,
  withGroupName,
  withIngredients,
  withNewGroup,
  withoutEmptyGroup
} from './ingredientGroups';
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

const dough = group('g1', 'For the dough', [one('flour')]);
const sauce = group('g2', 'For the sauce', [one('tomatoes')]);

describe('shownGroups', () => {
  it('shows one empty group for a recipe that has none, and the groups otherwise', () => {
    expect(shownGroups([])).toEqual([{ id: null, name: null, ingredients: [] }]);
    expect(shownGroups([dough])).toEqual([dough]);
  });
});

describe('withIngredients', () => {
  it('writes the edited list into the group asked for and keeps the others as they are', () => {
    const written = withIngredients([dough, sauce], 1, [one('tomatoes'), one('basil')]);

    expect(written[0]).toBe(dough);
    expect(written[1]!.id).toBe('g2');
    expect(written[1]!.name).toBe('For the sauce');
    expect(written[1]!.ingredients.map((i) => i.name)).toEqual(['tomatoes', 'basil']);
  });

  it('makes a first group for a recipe that has none', () => {
    expect(withIngredients([], 0, [one('salt')])).toEqual([
      { id: null, name: null, ingredients: [one('salt')] }
    ]);
  });
});

describe('withGroupName', () => {
  it('renames one group', () => {
    const written = withGroupName([dough, sauce], 1, 'For the filling');

    expect(written[0]).toBe(dough);
    expect(written[1]).toEqual({ ...sauce, name: 'For the filling' });
  });

  it('keeps a group whose name is cleared, unnamed', () => {
    const written = withGroupName([dough, sauce], 0, '');

    expect(written).toHaveLength(2);
    expect(written[0]).toEqual({ ...dough, name: null });
  });
});

describe('withNewGroup', () => {
  it('adds an empty group with no id at the end', () => {
    const written = withNewGroup([dough, sauce]);

    expect(written).toHaveLength(3);
    expect(written[2]).toEqual({ id: null, name: null, ingredients: [] });
  });
});

describe('withoutEmptyGroup', () => {
  it('removes a group with nothing in it', () => {
    expect(withoutEmptyGroup([dough, group('g3', 'To serve', [])], 1)).toEqual([dough]);
  });

  it('never removes a group that still holds a line', () => {
    expect(withoutEmptyGroup([dough, sauce], 1)).toEqual([dough, sauce]);
  });
});
