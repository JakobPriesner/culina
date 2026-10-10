import { describe, expect, it } from 'vitest';

import { adoptSaved } from './adoptSaved';
import type { Ingredient, Recipe } from '../types';

const line = (name: string, id = ''): Ingredient => ({
  id,
  name,
  note: null,
  quantity: { value: null, unit: null }
});

const recipe = (groups: Recipe['groups']): Recipe =>
  ({ version: 1, groups, steps: [] }) as unknown as Recipe;

describe('adoptSaved across groups', () => {
  it('gives a new line in the second group the id the server assigned', () => {
    const draft = recipe([
      { id: 'g1', name: null, ingredients: [line('flour', 'i-flour')] },
      { id: 'g2', name: 'Filling', ingredients: [line('apples')] }
    ]);
    const saved = recipe([
      { id: 'g1', name: null, ingredients: [line('flour', 'i-flour')] },
      { id: 'g2', name: 'Filling', ingredients: [line('apples', 'i-apples')] }
    ]);

    const { recipe: adopted } = adoptSaved(draft, saved, 1);

    expect(adopted.groups[1]!.ingredients[0]!.id).toBe('i-apples');
  });

  it('never gives one id to two groups', () => {
    const draft = recipe([
      { id: 'g1', name: null, ingredients: [line('salt')] },
      { id: 'g2', name: null, ingredients: [line('salt')] }
    ]);
    const saved = recipe([
      { id: 'g1', name: null, ingredients: [line('salt', 'a')] },
      { id: 'g2', name: null, ingredients: [line('salt', 'b')] }
    ]);

    const { recipe: adopted } = adoptSaved(draft, saved, 1);

    expect(adopted.groups.map((g) => g.ingredients[0]!.id)).toEqual(['a', 'b']);
  });

  it('gives a new group its server id, and claims its lines from that group', () => {
    const draft = recipe([
      { id: 'g1', name: null, ingredients: [line('flour', 'i-flour')] },
      { id: null, name: 'Filling', ingredients: [line('apples')] }
    ]);
    const saved = recipe([
      { id: 'g1', name: null, ingredients: [line('flour', 'i-flour')] },
      { id: 'g2', name: 'Filling', ingredients: [line('apples', 'i-apples')] }
    ]);

    const { recipe: adopted } = adoptSaved(draft, saved, 1);

    expect(adopted.groups[1]!.id).toBe('g2');
    expect(adopted.groups[1]!.ingredients[0]!.id).toBe('i-apples');
  });

  it('leaves a line unclaimed when its group is not in the save', () => {
    const draft = recipe([{ id: 'gone', name: null, ingredients: [line('flour')] }]);
    const saved = recipe([{ id: 'g1', name: null, ingredients: [line('flour', 'i-flour')] }]);

    const { recipe: adopted } = adoptSaved(draft, saved, 1);

    expect(adopted.groups[0]!.ingredients[0]!.id).toBe('');
  });
});
