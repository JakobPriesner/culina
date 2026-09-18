import { describe, expect, it } from 'vitest';

import { namedIn, usageOf, withoutIngredients } from './stepUsage';
import type { Step } from '../types';

const step = (uses: string[], segments: Step['segments'] = []): Step => ({
  id: null,
  title: null,
  segments,
  uses,
  durationSeconds: null
});

const mention = (id: string): Step['segments'][number] => ({
  kind: 'ingredient',
  ingredientId: id,
  name: id,
  quantity: { value: null, unit: null }
});

describe('which steps an ingredient ends up in', () => {
  it('numbers the steps from one, as the editor labels them', () => {
    const usage = usageOf([step(['butter']), step([]), step(['butter', 'flour'])]);

    expect(usage.get('butter')).toEqual([1, 3]);
    expect(usage.get('flour')).toEqual([3]);
  });

  it('says nothing about an ingredient no step needs', () => {
    // Salt to taste belongs to no step, and never will.
    expect(usageOf([step(['butter'])]).get('salt')).toBeUndefined();
  });
});

describe('which of them the words name', () => {
  it('separates a mention from a need', () => {
    // Both are needed; only one is said out loud, and only the said one is
    // beyond the chip row's power to remove.
    const one = step(['butter', 'salt'], [{ kind: 'text', text: 'Melt ' }, mention('butter')]);

    expect(namedIn(one)).toEqual(new Set(['butter']));
  });
});

describe('deleting an ingredient', () => {
  it('takes it off every step that needed it', () => {
    const steps = [step(['butter', 'salt']), step(['salt'])];

    expect(withoutIngredients(steps, new Set(['salt']))).toEqual([step(['butter']), step([])]);
  });

  it('leaves a step it did not touch as it was, so nothing re-renders', () => {
    const untouched = step(['butter']);

    expect(withoutIngredients([untouched], new Set(['salt']))[0]).toBe(untouched);
  });
});
