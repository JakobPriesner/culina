import { afterEach, describe, expect, it, vi } from 'vitest';

import { createScaling } from '$features/recipes/surface/scaled.svelte';
import type { RecipeReading, Step } from '$features/recipes/types';

import { readAloud, spokenStep } from './readAloud.svelte';

/* What is heard matches what is shown: the step's name, its words, the amounts at the servings cooked. */
const butter = {
  kind: 'ingredient',
  ingredientId: 'b',
  name: 'butter',
  quantity: { value: 100, unit: 'g' }
};
const step: Step = {
  id: 's1',
  title: null,
  segments: [
    { kind: 'text', text: 'Melt the ' },
    butter,
    { kind: 'text', text: ' **slowly**.' }
  ] as Step['segments'],
  uses: [],
  durationSeconds: null
};
const recipe = { language: 'en', yieldAmount: 2 } as RecipeReading;

afterEach(() => vi.unstubAllGlobals());

describe('reading a step aloud', () => {
  it('says the step at the servings on screen, without the marks of its formatting', () => {
    const doubled = createScaling(
      () => recipe,
      () => 4
    );

    expect(spokenStep(step, 2, doubled, 'en')).toBe('Step 2. Melt the 200 grams butter slowly.');
  });

  it('interrupts the last step, in the recipe’s language', () => {
    const speak = vi.fn();
    const cancel = vi.fn();

    vi.stubGlobal('speechSynthesis', { speak, cancel, getVoices: () => [] });
    vi.stubGlobal(
      'SpeechSynthesisUtterance',
      class {
        lang = '';
        voice = null;
        constructor(public text: string) {}
      }
    );

    readAloud.speak('Step 1. Chop.', 'de');

    expect(cancel).toHaveBeenCalled();
    expect(speak).toHaveBeenCalledWith(
      expect.objectContaining({ text: 'Step 1. Chop.', lang: 'de-DE' })
    );
  });
});
