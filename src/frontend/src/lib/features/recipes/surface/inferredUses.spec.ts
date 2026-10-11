import { describe, expect, it } from 'vitest';

import type { Ingredient, Step } from '../types';
import { inferUses } from './inferredUses';

const ingredient = (id: string, name: string): Ingredient => ({
  id,
  name,
  note: null,
  quantity: null as never
});

const step = (text: string): Step => ({
  id: null,
  title: null,
  segments: [{ kind: 'text', text }],
  uses: [],
  durationSeconds: null
});

const bolognese = [
  ingredient('onion', 'Zwiebel'),
  ingredient('garlic', 'Knoblauchzehe'),
  ingredient('tomatoes', 'Dose geschälte Tomaten'),
  ingredient('paste', 'Tomatenmark'),
  ingredient('mince', 'Rinderhack'),
  ingredient('cheese', 'italienischer Hartkäse')
];

describe('inferring what a step needs from its words', () => {
  it('finds an ingredient named with another ending', () => {
    expect(inferUses([step('Die Zwiebeln fein würfeln.')], bolognese)).toEqual([['onion']]);
  });

  it('finds a compound ingredient from its first part', () => {
    expect(inferUses([step('Den Knoblauch andrücken.')], bolognese)).toEqual([['garlic']]);
  });

  it('prefers the plain word over a compound that starts with it', () => {
    expect(inferUses([step('Die Tomaten abtropfen lassen.')], bolognese)).toEqual([['tomatoes']]);
  });

  it('still finds the compound when the step names it', () => {
    expect(inferUses([step('Tomatenmark unterrühren.')], bolognese)).toEqual([['paste']]);
  });

  it('folds umlauts and case', () => {
    expect(inferUses([step('Den HARTKASE reiben.')], bolognese)).toEqual([['cheese']]);
  });

  it('lists nothing for a step that names no ingredient', () => {
    expect(inferUses([step('Den Ofen auf 180 Grad vorheizen.')], bolognese)).toEqual([[]]);
  });

  it('keeps the recipe order of ingredients', () => {
    expect(inferUses([step('Hackfleisch? Nein: Rinderhack mit Zwiebel.')], bolognese)).toEqual([
      ['onion', 'mince']
    ]);
  });
});
