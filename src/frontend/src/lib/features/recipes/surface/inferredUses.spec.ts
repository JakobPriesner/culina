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

  it('finds a compound ingredient from its last part, inflected or not', () => {
    const chili = [
      ingredient('onion', 'Gemüsezwiebel'),
      ingredient('salt', 'Meersalz'),
      ingredient('stock', 'Gemüsebrühe'),
      ingredient('beans', 'Kidneybohne')
    ];

    expect(
      inferUses([step('Zwiebeln würfeln, mit Salz würzen, Brühe und Bohnen zugeben.')], chili)
    ).toEqual([['onion', 'salt', 'stock', 'beans']]);
  });

  it('finds an oil from the short word Öl, alone or as the end of a compound', () => {
    const olive = ingredient('olive oil', 'Olivenöl');

    expect(inferUses([step('In heißem Öl anbraten.')], [olive, ingredient('oil', 'Öl')])).toEqual([
      ['oil']
    ]);
    expect(inferUses([step('In heißem Öl anbraten.')], [olive])).toEqual([['olive oil']]);
  });

  it('never reads Öl as the start of a compound', () => {
    expect(inferUses([step('Mit Öl beträufeln.')], [ingredient('olives', 'Oliven')])).toEqual([[]]);
  });

  it('prefers the plain word over a compound that ends with it', () => {
    const sugars = [ingredient('sugar', 'brauner Zucker'), ingredient('icing', 'Puderzucker')];

    expect(inferUses([step('Den Zucker einrühren.')], sugars)).toEqual([['sugar']]);
  });

  it('prefers a compound that ends with the word over one that starts with it', () => {
    const tomatoes = [ingredient('bottled', 'Flaschentomate'), ingredient('paste', 'Tomatenmark')];

    expect(inferUses([step('Die Tomaten zugeben.')], tomatoes)).toEqual([['bottled']]);
  });

  it('does not take a few trailing letters for the last part of a compound', () => {
    expect(inferUses([step('Die Eier verquirlen.')], [ingredient('sage', 'Salbei')])).toEqual([[]]);
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
