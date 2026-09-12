import { describe, expect, it } from 'vitest';

import { parseIngredientLine as parse } from './parseIngredientLine';

/*
 * The parser is only trustworthy because its result is shown back in separate
 * parts, so a wrong read is visible. These are the reads it must not get wrong.
 */
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
