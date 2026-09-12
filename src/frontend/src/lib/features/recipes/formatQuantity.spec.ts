import { describe, expect, it } from 'vitest';

import { formatQuantity, type QuantityLabels } from './formatQuantity';
import { scaleQuantity } from './scaling';
import type { Unit } from './units';

/*
 * Formatting is the only part of scaling that knows about language, so this is
 * where `1,5 kg` and `1.5 kg` are pinned down.
 */
const labels: QuantityLabels = {
  // Stands in for the real, localised labels. Spoons are abbreviated and never
  // pluralised; anything else that needs a word beside the number is spelled
  // out, and a word does pluralise.
  unitName: (unit: Unit, count: number) =>
    unit === 'tsp' || unit === 'tbsp' ? unit : count === 1 ? unit : `${unit}s`,
  approximately: (amount) => `~${amount}`
};

/**
 * Amount and unit are joined by a non-breaking space, so `250` never wraps away
 * from `g`. Spelled out here because it is invisible in a diff and in an
 * assertion message.
 */
const nbsp = '\u00a0';

const show = (value: number | null, unit: Unit | null, factor = 1, locale = 'en') =>
  formatQuantity(scaleQuantity({ value, unit }, factor), locale, labels).text;

describe('numbers', () => {
  it('trims a decimal that says nothing', () => {
    expect(show(1, 'kg')).toBe('1' + nbsp + 'kg');
    expect(show(1.5, 'kg')).toBe('1.5' + nbsp + 'kg');
  });

  it('uses the separator the reader expects', () => {
    expect(show(1.5, 'kg', 1, 'en')).toBe('1.5' + nbsp + 'kg');
    expect(show(1.5, 'kg', 1, 'de')).toBe('1,5' + nbsp + 'kg');
  });
});

describe('fractions', () => {
  it('renders as glyphs where the unit is measured in them', () => {
    expect(show(1, 'tsp', 0.5)).toBe('½' + nbsp + 'tsp');
    expect(show(3, 'tbsp', 0.5)).toBe('1½' + nbsp + 'tbsp');
    expect(show(1, 'tsp', 1 / 3)).toBe('⅓' + nbsp + 'tsp');
    expect(show(1, 'tsp', 2 / 3)).toBe('⅔' + nbsp + 'tsp');
  });

  it('does not, where nobody writes them', () => {
    // Half a gram is 0.5 g, not ½ g.
    expect(show(1, 'g', 0.5)).toBe('0.5' + nbsp + 'g');
  });
});

describe('ranges', () => {
  it('use an en dash with no spaces, the way a range is written', () => {
    expect(show(3, 'clove', 1.5)).toBe('4–5' + nbsp + 'cloves');
  });

  it('name the unit in the plural of the larger bound', () => {
    expect(show(1, 'piece', 1.5)).toBe('1–2' + nbsp + 'pieces');
  });
});

describe('approximations', () => {
  it('are marked, so they cannot be mistaken for a measurement', () => {
    expect(show(7.3, 'g', 1.0001)).toBe('~7.5' + nbsp + 'g');
  });

  it('are not marked when the number is exactly what it says', () => {
    expect(show(500, 'g', 2)).toBe('1' + nbsp + 'kg');
  });
});

describe('things with nothing to show', () => {
  it('render as nothing at all, rather than as a zero', () => {
    expect(show(null, null)).toBe('');
    expect(show(null, 'g')).toBe('');
  });
});

describe('the parts', () => {
  it('are available separately, so the number can be animated on its own', () => {
    const parts = formatQuantity(scaleQuantity({ value: 250, unit: 'g' }, 1), 'en', labels);

    expect(parts).toMatchObject({ amount: '250', unit: 'g', text: `250${nbsp}g` });
  });
});

describe('units whose abbreviation is a word', () => {
  /*
   * A German recipe says EL, not tbsp. Spoons used to carry a hard-coded
   * English abbreviation, which no translation could reach; they now come from
   * the labels like every other unit that needs a word.
   */
  it('take their short form from the labels, so a translation can reach it', () => {
    const german: QuantityLabels = {
      unitName: (unit: Unit) => ({ tsp: 'TL', tbsp: 'EL' })[unit as 'tsp' | 'tbsp'] ?? '',
      approximately: (amount) => `~${amount}`
    };

    expect(formatQuantity(scaleQuantity({ value: 2, unit: 'tbsp' }, 1), 'de', german).text).toBe(
      `2${nbsp}EL`
    );
  });
});
