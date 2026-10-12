import { describe, expect, it } from 'vitest';

import { createScaling } from './scaled.svelte';
import type { RecipeReading } from '../types';

const recipe = { language: 'de', yieldAmount: 2 } as RecipeReading;

describe('a name at a scale', () => {
  it('follows its count across one', () => {
    const doubled = createScaling(
      () => recipe,
      () => 4
    );

    expect(doubled.nameFor({ name: 'Zwiebel', quantity: { value: 1, unit: null } })).toBe(
      'Zwiebeln'
    );
  });

  it('stays as written after a unit', () => {
    const doubled = createScaling(
      () => recipe,
      () => 4
    );

    expect(doubled.nameFor({ name: 'Brot', quantity: { value: 2, unit: 'slice' } })).toBe('Brot');
  });
});
