import { describe, expect, it } from 'vitest';

import type { RecipeReading } from '$features/recipes/types';

import { stepSpeech } from './stepSpeech';

type Step = RecipeReading['steps'][number];

const step = (segments: Step['segments']): Step => ({
  id: 's',
  title: 'Simmer',
  uses: [],
  durationSeconds: null,
  segments
});

const amountFor = (of: { quantity: { value: number | null; unit: string | null } }) => ({
  text: `${of.quantity.value} ${of.quantity.unit ?? ''}`.trim()
});
const nameFor = (of: { name: string }) => of.name;

describe('stepSpeech', () => {
  it('says ingredients at their amounts, with units spelled out', () => {
    const spoken = stepSpeech(
      step([
        { kind: 'text', text: '**Melt** ' },
        {
          kind: 'ingredient',
          ingredientId: 'butter',
          name: 'butter',
          quantity: { value: 200, unit: 'g' }
        },
        { kind: 'text', text: ' at 180°C.' }
      ]),
      amountFor,
      nameFor,
      'en'
    );

    expect(spoken).toBe('Melt 200 grams butter at 180 degrees Celsius.');
  });

  it('makes each paragraph and list item a sentence of its own', () => {
    const spoken = stepSpeech(
      step([{ kind: 'text', text: 'Prepare the pan\n\n- Heat oil\n- Add onions\n\nServe warm.' }]),
      amountFor,
      nameFor,
      'en'
    );

    expect(spoken).toBe('Prepare the pan. Heat oil. Add onions. Serve warm.');
  });

  it('says nothing for no step', () => {
    expect(stepSpeech(undefined, amountFor, nameFor, 'en')).toBe('');
  });
});
