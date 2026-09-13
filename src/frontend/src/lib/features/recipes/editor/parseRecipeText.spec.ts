import { describe, expect, it } from 'vitest';

import { parseRecipeText } from './parseRecipeText';

/*
 * Recipes as people actually paste them.
 *
 * The parse is only worth having because it is shown back in parts and can be
 * corrected, so these are not about perfection — they are about the readings
 * that would be actively misleading if they were wrong, and about never losing
 * a line to a heuristic.
 */

const names = (text: string) => parseRecipeText(text).ingredients.map((one) => one.name);

describe('a recipe pasted with headings', () => {
  const pasted = `
Zitronen-Orzo mit Zucchini

Zutaten
200 g Orzo
2 Zucchini, in Scheiben
2 EL Olivenöl
Salz

Zubereitung
1. Koche den Orzo nach Packungsangabe.
2. Brate die Zucchini goldbraun an.
3. Hebe alles unter und schmecke ab.
`;

  it('takes the first line as the name', () => {
    expect(parseRecipeText(pasted).title).toBe('Zitronen-Orzo mit Zucchini');
  });

  it('reads every ingredient, amounts and units apart', () => {
    expect(parseRecipeText(pasted).ingredients).toEqual([
      { quantity: { value: 200, unit: 'g' }, name: 'Orzo', note: null },
      { quantity: { value: 2, unit: null }, name: 'Zucchini', note: 'in Scheiben' },
      { quantity: { value: 2, unit: 'tbsp' }, name: 'Olivenöl', note: null },
      // A bare word under "Zutaten" is an ingredient with no amount, which the
      // shape of the line alone could never tell you.
      { quantity: { value: null, unit: null }, name: 'Salz', note: null }
    ]);
  });

  it('reads the steps, and drops the numbers the list will supply again', () => {
    expect(parseRecipeText(pasted).steps).toEqual([
      'Koche den Orzo nach Packungsangabe',
      'Brate die Zucchini goldbraun an',
      'Hebe alles unter und schmecke ab'
    ]);
  });
});

describe('a recipe pasted with no headings at all', () => {
  const pasted = `
Pancakes
125 g flour
1 egg
250 ml milk
Whisk everything together and rest the batter for half an hour.
Fry in a hot buttered pan until the edges lift.
`;

  it('sorts the lines by what they look like', () => {
    const read = parseRecipeText(pasted);

    expect(read.title).toBe('Pancakes');
    expect(read.ingredients.map((one) => one.name)).toEqual(['flour', 'egg', 'milk']);
    expect(read.steps).toHaveLength(2);
  });
});

describe('the readings that would be misleading if they were wrong', () => {
  it('does not read a numbered step as two hundred grams of something', () => {
    const read = parseRecipeText('1. Heat the oven to 200 °C.');

    expect(read.ingredients).toEqual([]);
    expect(read.steps).toEqual(['Heat the oven to 200 °C']);
  });

  it('does not read a sentence that opens with an amount as an ingredient', () => {
    const read = parseRecipeText(
      '200 g of the flour goes in first, and the rest is folded in at the end.'
    );

    expect(read.ingredients).toEqual([]);
    expect(read.steps).toHaveLength(1);
  });

  it('reads a range as its lower bound, which is the one you can add to', () => {
    expect(parseRecipeText('Salat\n2-3 EL Olivenöl').ingredients[0]).toEqual({
      quantity: { value: 2, unit: 'tbsp' },
      name: 'Olivenöl',
      note: null
    });
  });

  it('reads an en-dash range too, because that is what a blog pastes', () => {
    expect(parseRecipeText('Salat\n2–3 EL Olivenöl').ingredients[0]?.quantity).toEqual({
      value: 2,
      unit: 'tbsp'
    });
  });

  it('keeps a fraction as a fraction', () => {
    expect(parseRecipeText('Teig\n½ TL Salz\n1/2 kg Mehl').ingredients).toEqual([
      { quantity: { value: 0.5, unit: 'tsp' }, name: 'Salz', note: null },
      { quantity: { value: 0.5, unit: 'kg' }, name: 'Mehl', note: null }
    ]);
  });

  it('strips the bullets a copied list brings with it', () => {
    expect(names('Zutaten\n- 200 g Mehl\n• 1 Ei\n* 2 EL Öl')).toEqual(['Mehl', 'Ei', 'Öl']);
  });

  it('takes no title from something that is plainly an ingredient', () => {
    // Somebody pasting only a list has not given it a name, and inventing one
    // out of their first ingredient would be worse than leaving it blank.
    expect(parseRecipeText('200 g Mehl\n1 Ei').title).toBe('');
  });

  it('reads a unit this kitchen added, the same as anywhere else', () => {
    expect(parseRecipeText('Soße\n1 Schuss Milch', ['Schuss']).ingredients[0]).toEqual({
      quantity: { value: 1, unit: 'Schuss' },
      name: 'Milch',
      note: null
    });
  });
});

describe('what it refuses to invent', () => {
  it('finds nothing in nothing', () => {
    expect(parseRecipeText('')).toEqual({ title: '', ingredients: [], steps: [] });
  });

  it('does not turn a stray word into a step', () => {
    // A copied page brings "Foto", "Drucken", "4 Portionen" with it. A step
    // called "Drucken" is worse than a step missing.
    const read = parseRecipeText('Kuchen\nZubereitung\nFoto\nRühre alles gut durch.');

    expect(read.steps).toEqual(['Rühre alles gut durch']);
  });

  it('keeps every line of the method, even the short ones', () => {
    const read = parseRecipeText('Zubereitung\nOfen auf 200 °C vorheizen.\nAlles verrühren.');

    expect(read.steps).toEqual(['Ofen auf 200 °C vorheizen', 'Alles verrühren']);
  });
});
