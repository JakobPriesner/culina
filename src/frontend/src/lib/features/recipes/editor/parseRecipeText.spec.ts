import { describe, expect, it } from 'vitest';

import { parseRecipeText } from './parseRecipeText';

/*
 * Recipes as people paste them: readings that would mislead if wrong, and never losing a line to a
 * heuristic.
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
    // A list pasted alone has no name; inventing one from the first ingredient would be worse.
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
    // A copied page brings "Foto", "Drucken", "4 Portionen"; a step called "Drucken" is worse than
    // a missing one.
    const read = parseRecipeText('Kuchen\nZubereitung\nFoto\nRühre alles gut durch.');

    expect(read.steps).toEqual(['Rühre alles gut durch']);
  });

  it('keeps every line of the method, even the short ones', () => {
    const read = parseRecipeText('Zubereitung\nOfen auf 200 °C vorheizen.\nAlles verrühren.');

    expect(read.steps).toEqual(['Ofen auf 200 °C vorheizen', 'Alles verrühren']);
  });
});

describe('a recipe that states its yield and time', () => {
  const pasted = `
Tomatenpasta
Für 2 Portionen
Zubereitung: 20 Minuten

Zutaten
200 g Spaghetti
400 g Tomaten
1 EL Olivenöl

Zubereitung
1. Spaghetti kochen.
2. Tomaten in Olivenöl köcheln.
3. Alles mischen und servieren.
`;

  it('keeps the yield and the time out of the title and the steps', () => {
    const read = parseRecipeText(pasted);

    expect(read.title).toBe('Tomatenpasta');
    expect(read.steps).toEqual([
      'Spaghetti kochen',
      'Tomaten in Olivenöl köcheln',
      'Alles mischen und servieren'
    ]);
    expect(read.ingredients.map((one) => one.name)).toEqual(['Spaghetti', 'Tomaten', 'Olivenöl']);
  });

  it("reports them as the recipe's own", () => {
    expect(parseRecipeText(pasted)).toMatchObject({ servings: 2, totalMinutes: 20 });
  });

  it.each([
    ['Für 4 Personen', 4],
    ['Portionen: 6', 6],
    ['4 Portionen', 4],
    ['Serves 3', 3],
    ['Makes 8 servings', 8],
    ['- 2 servings', 2]
  ])('reads "%s" as %i servings', (line, servings) => {
    const read = parseRecipeText(`Suppe\n${line}\nZubereitung\nAlles kochen.`);

    expect(read.servings).toBe(servings);
    expect(read.title).toBe('Suppe');
    expect(read.steps).toEqual(['Alles kochen']);
  });

  it('reads a count of things as pieces, in the word it was given in', () => {
    expect(parseRecipeText('Muffins\nErgibt 12 Muffins')).toMatchObject({
      servings: 12,
      yieldKind: 'pieces',
      yieldLabel: 'Muffins'
    });
  });

  it.each([
    ['Gesamtzeit: 1 Std. 15 Min.', 75],
    ['Total time: 2 hours', 120],
    ['Kochzeit ca. 45 Minuten', 45]
  ])('reads "%s" as %i minutes', (line, minutes) => {
    expect(parseRecipeText(`Eintopf\n${line}`).totalMinutes).toBe(minutes);
  });

  it('adds preparation and cooking, unless the page gave the total itself', () => {
    expect(
      parseRecipeText('Eintopf\nArbeitszeit: 15 Minuten\nKochzeit: 30 Minuten').totalMinutes
    ).toBe(45);
    expect(
      parseRecipeText(
        'Eintopf\nArbeitszeit: 15 Minuten\nKochzeit: 30 Minuten\nGesamtzeit: 60 Minuten'
      ).totalMinutes
    ).toBe(60);
  });

  it('states nothing when the page did not', () => {
    const read = parseRecipeText('Pancakes\n125 g flour\nWhisk everything together.');

    expect(read).not.toHaveProperty('servings');
    expect(read).not.toHaveProperty('totalMinutes');
  });

  it('still reads a method that says how long something takes', () => {
    const read = parseRecipeText('Zubereitung\n1. Zeit: 5 Minuten ruhen lassen.\nWarten.');

    expect(read.steps).toEqual(['Zeit: 5 Minuten ruhen lassen', 'Warten']);
  });
});
