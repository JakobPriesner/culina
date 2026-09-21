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

  it('turns a mention of it back into the word it was showing', () => {
    // The server rebuilds `uses` from the sentence, so a mention left behind
    // put the deleted id straight back and failed the next autosave.
    const sentence = step(
      ['butter'],
      [{ kind: 'text', text: 'Melt ' }, mention('butter'), { kind: 'text', text: ' in the pan.' }]
    );

    const [written] = withoutIngredients([sentence], new Set(['butter']));

    expect(written!.uses).toEqual([]);
    expect(written!.segments).toEqual([{ kind: 'text', text: 'Melt butter in the pan.' }]);
  });

  it('leaves other ingredients mentioned in the same sentence alone', () => {
    const sentence = step(
      ['butter', 'salt'],
      [mention('butter'), { kind: 'text', text: ' and ' }, mention('salt')]
    );

    const [written] = withoutIngredients([sentence], new Set(['salt']));

    expect(written!.segments).toEqual([mention('butter'), { kind: 'text', text: ' and salt' }]);
  });

  it('leaves a step it did not touch as it was, so nothing re-renders', () => {
    const untouched = step(['butter']);

    expect(withoutIngredients([untouched], new Set(['salt']))[0]).toBe(untouched);
  });
});
