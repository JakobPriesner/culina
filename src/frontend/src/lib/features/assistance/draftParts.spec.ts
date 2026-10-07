import { describe, expect, it } from 'vitest';

import { draftParts } from './draftParts';
import type { Draft } from './draftToRecipe';

import type { Recipe } from '$features/recipes/types';

const current = {
  title: 'Tomato soup',
  description: null,
  yieldAmount: 4,
  yieldLabel: null,
  prepMinutes: 10,
  cookMinutes: null,
  groups: [],
  steps: []
} as unknown as Recipe;

const draft = (overrides: Partial<Draft> = {}): Draft => ({
  draftId: 'draft-1',
  groups: [],
  steps: [],
  tags: [],
  ...overrides
});

const keys = (parts: ReturnType<typeof draftParts>) => parts.map((part) => part.key);

describe('the parts of a draft under review', () => {
  it('has none before the draft exists', () => {
    expect(draftParts(null, current)).toEqual([]);
  });

  it('offers only what the draft says something about', () => {
    expect(keys(draftParts(draft({ title: 'Roasted tomato soup' }), current))).toEqual(['title']);
  });

  it('sets what is there now beside what the draft says', () => {
    const [part] = draftParts(draft({ title: 'Roasted tomato soup' }), current);

    expect(part).toMatchObject({ before: 'Tomato soup', after: 'Roasted tomato soup' });
  });

  it('keeps the current yield where the draft names only a label', () => {
    const [part] = draftParts(draft({ yieldLabel: 'bowls' }), current);

    expect(part).toMatchObject({ key: 'yield', before: '4', after: '4 bowls' });
  });

  it('joins the times it has, leaving out the ones it has not', () => {
    const [part] = draftParts(draft({ prepMinutes: 5, cookMinutes: 25 }), current);

    expect(part?.key).toBe('times');
    expect(part?.after).toContain(' + ');
    expect(part?.before).not.toContain('+');
  });
});
