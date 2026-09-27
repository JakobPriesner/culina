import { describe, expect, it } from 'vitest';

import { resetAllStores } from '$shell/stores';

import type { RecipeSummary } from '../types';
import { presumedDiets } from './presumedDiets.svelte';

const summary = (id: string, presumedDiet: RecipeSummary['presumedDiet']): RecipeSummary => ({
  id,
  title: id,
  imageId: null,
  totalMinutes: null,
  yieldAmount: 4,
  yieldKind: 'servings',
  yieldLabel: null,
  tags: [],
  cookCount: 0,
  lastCookedAt: null,
  updatedAt: '2026-09-27T00:00:00Z',
  match: null,
  presumedDiet
});

describe('which recipes a search only presumed', () => {
  it('remembers a presumption until somebody answers it', () => {
    presumedDiets.note([summary('r1', 'vegetarian'), summary('r2', null)]);

    expect(presumedDiets.of('r1')).toBe('vegetarian');
    expect(presumedDiets.of('r2')).toBeNull();

    presumedDiets.settle('r1');

    expect(presumedDiets.of('r1')).toBeNull();
  });

  it('forgets everything on sign-out', () => {
    presumedDiets.note([summary('r1', 'vegan')]);

    resetAllStores();

    expect(presumedDiets.of('r1')).toBeNull();
  });
});
