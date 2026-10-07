import { describe, expect, it } from 'vitest';

import { parseIngredientLine as parse } from './parseIngredientLine';

/* Reads the parser must not get wrong: a wrong read is only visible because the result is shown back in parts. */
describe('the ordinary case', () => {
  it.each([
    ['200 g Mehl', 200, 'g', 'Mehl'],
    ['1 kg Kartoffeln', 1, 'kg', 'Kartoffeln'],
    ['250 ml Milch', 250, 'ml', 'Milch'],
    ['2 EL Olivenöl', 2, 'tbsp', 'Olivenöl'],
    ['1 TL Salz', 1, 'tsp', 'Salz'],
    ['3 Zehen Knoblauch', 3, 'clove', 'Knoblauch'],
    ['2 tbsp olive oil', 2, 'tbsp', 'olive oil'],
    ['1 bunch parsley', 1, 'bunch', 'parsley']
  ])('reads %s', (line, value, unit, name) => {
    expect(parse(line)).toMatchObject({ quantity: { value, unit }, name });
  });
});

describe('numbers people actually write', () => {
  it.each([
    ['1,5 kg Mehl', 1.5],
    ['1.5 kg Mehl', 1.5],
    ['1/2 kg Mehl', 0.5],
    ['½ kg Mehl', 0.5],
    ['1½ kg Mehl', 1.5]
  ])('reads the amount in %s', (line, value) => {
    expect(parse(line).quantity.value).toBe(value);
  });
});

describe('the preparation', () => {
  it('is whatever follows the comma', () => {
    expect(parse('2 Zwiebeln, fein gehackt')).toMatchObject({
      quantity: { value: 2, unit: null },
      name: 'Zwiebeln',
      note: 'fein gehackt'
    });
  });

  it('is absent when there is no comma', () => {
    expect(parse('2 Zwiebeln').note).toBeNull();
  });
});

describe('what has no amount', () => {
  it('is all name', () => {
    expect(parse('Salz')).toMatchObject({ quantity: { value: null, unit: null }, name: 'Salz' });
  });

  it('does not mistake the first word of a name for a unit', () => {
    expect(parse('Lorbeerblatt')).toMatchObject({ name: 'Lorbeerblatt' });
    expect(parse('Gurke').quantity.unit).toBeNull();
  });

  it('keeps a bare count without inventing a unit', () => {
    expect(parse('2 Eier')).toMatchObject({ quantity: { value: 2, unit: null }, name: 'Eier' });
  });
});

describe('what it cannot read', () => {
  it('still becomes an ingredient rather than vanishing', () => {
    expect(parse('etwas Muskatnuss').name).toBe('etwas Muskatnuss');
  });

  it('survives an empty line without throwing', () => {
    expect(parse('   ')).toMatchObject({ name: '', quantity: { value: null, unit: null } });
  });
});

describe('German as it is actually typed', () => {
  /* Phone and laptop keyboards spell the same word differently, and German plurals are no suffix rule ("2 Packungen Feta" once became an ingredient). */
  it('reads a unit written with an umlaut', () => {
    expect(parse('2 Stück Zwiebeln').quantity).toEqual({
      value: 2,
      unit: 'piece'
    });
    expect(parse('3 Esslöffel Öl').quantity.unit).toBe('tbsp');
    expect(parse('1 Päckchen Hefe').quantity.unit).toBe('pack');
  });

  it('reads the plural as the same unit', () => {
    expect(parse('2 Packungen Feta')).toMatchObject({
      quantity: { value: 2, unit: 'pack' },
      name: 'Feta'
    });
    expect(parse('2 Prisen Salz').quantity.unit).toBe('pinch');
    expect(parse('2 Dosen Tomaten').quantity.unit).toBe('can');
  });

  it('still refuses to read a name as a unit', () => {
    expect(parse('3 Zitronen')).toMatchObject({
      quantity: { value: 3, unit: null },
      name: 'Zitronen'
    });
  });
});

describe('a unit this kitchen added itself', () => {
  it('does not read a word it has never seen as a unit', () => {
    expect(parse('1 Schuss Milch')).toEqual({
      quantity: { value: 1, unit: null },
      name: 'Schuss Milch',
      note: null
    });
  });

  it('reads a unit this kitchen has written before', () => {
    expect(parse('1 Schuss Milch', ['Schuss'])).toEqual({
      quantity: { value: 1, unit: 'Schuss' },
      name: 'Milch',
      note: null
    });
  });

  it('matches a household unit however it was capitalised', () => {
    expect(parse('2 schuss Öl', ['Schuss']).quantity.unit).toBe('Schuss');
  });

  it('still needs an amount before it will call a word a unit', () => {
    expect(parse('Schuss Milch', ['Schuss'])).toEqual({
      quantity: { value: null, unit: null },
      name: 'Schuss Milch',
      note: null
    });
  });
});
