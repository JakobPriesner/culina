import { afterEach, describe, expect, it } from 'vitest';

import { applyLocale } from '$shell/i18n';

import { formatQuantity } from './formatQuantity';
import { quantityLabels } from './quantityLabels';
import { scaleQuantity } from './scaling';

/*
 * A metric recipe in US units. The danger is confidence, not arithmetic: "2 cups" for 250 g of flour is wrong invisibly.
 * The tilde means "I rounded this", so cooks know which amounts to trust to the gram.
 */

/** Rendered text with its non-breaking space made visible, so assertions stay readable in a diff. */
const shown = (value: number, unit: string, factor = 1) =>
  formatQuantity(
    scaleQuantity({ value, unit }, factor, 'imperial'),
    'en',
    quantityLabels
  ).text.replaceAll('\u00a0', ' ');

const metric = (value: number, unit: string, factor = 1) =>
  formatQuantity(
    scaleQuantity({ value, unit }, factor, 'metric'),
    'en',
    quantityLabels
  ).text.replaceAll('\u00a0', ' ');

describe('mass, which is the same measurement said differently', () => {
  it('becomes ounces', () => {
    expect(shown(227, 'g')).toBe('8 oz');
  });

  it('becomes pounds once there are enough of them', () => {
    expect(shown(680, 'g')).toBe('1½ lb');
    expect(shown(1, 'kg')).toBe('~2¼ lb');
  });

  it('never becomes cups', () => {
    // A cup of flour is 120-150 g depending on packing, so no cup conversion.
    expect(shown(250, 'g')).not.toContain('cup');
    expect(shown(1, 'kg')).not.toContain('cup');
  });

  it('lands on a fraction a scale can show', () => {
    expect(shown(120, 'g')).toBe('4¼ oz');
  });

  it('keeps halves right up to a pound', () => {
    // 10.58 oz reads as 10½, not 11 (a four per cent lie).
    expect(shown(300, 'g')).toBe('10½ oz');
  });

  it('says how little rather than nothing', () => {
    // 0.07 oz: a quarter-ounce would be 3x, and "0 oz" would drop it.
    expect(shown(2, 'g')).toBe('0.07 oz');
    expect(shown(3, 'ml')).toBe('0.1 fl oz');
  });
});

describe('volume, which converts honestly', () => {
  it('becomes fluid ounces below a cup', () => {
    expect(shown(120, 'ml')).toBe('4 fl oz');
  });

  it('becomes cups above one', () => {
    expect(shown(240, 'ml')).toBe('1 cup');
    expect(shown(500, 'ml')).toBe('~2 cups');
  });

  it('lands on a measure that is in the drawer', () => {
    expect(shown(320, 'ml')).toBe('1⅓ cups');
  });
});

describe('what it deliberately leaves alone', () => {
  it('keeps a spoon a spoon', () => {
    expect(shown(2, 'tbsp')).toBe(metric(2, 'tbsp'));
  });

  it('keeps a count a count', () => {
    expect(shown(2, 'piece')).toBe(metric(2, 'piece'));
    expect(shown(3, 'clove')).toBe(metric(3, 'clove'));
  });

  it('keeps a pinch a pinch, unscaled', () => {
    expect(shown(1, 'pinch', 3)).toBe(metric(1, 'pinch', 3));
  });

  it('keeps a unit this kitchen invented', () => {
    expect(shown(2, 'Schuss')).toBe(metric(2, 'Schuss'));
  });

  it('leaves an ingredient with no amount alone', () => {
    expect(
      formatQuantity(
        scaleQuantity({ value: null, unit: null }, 2, 'imperial'),
        'en',
        quantityLabels
      ).text
    ).toBe('');
  });
});

describe('converting and scaling together', () => {
  it('converts the exact amount rather than a rounded one', () => {
    // Converted from the raw value, not rounded grams (rounding twice drifts); the one per cent move is under the threshold and unmarked.
    expect(shown(200, 'g', 1.4)).toBe('10 oz');
  });

  it('scales into the larger unit when the amount asks for it', () => {
    expect(shown(300, 'g', 2)).toMatch(/lb$/);
  });
});

describe('in the reader’s language', () => {
  afterEach(() => applyLocale('en'));

  const inGerman = (value: number, unit: string) => {
    applyLocale('de');

    return formatQuantity(
      scaleQuantity({ value, unit }, 1, 'imperial'),
      'de',
      quantityLabels
    ).text.replaceAll('\u00a0', ' ');
  };

  it('converts a volume, and names the cups in German', () => {
    expect(inGerman(500, 'ml')).toBe('~2 Cups');
    expect(inGerman(240, 'ml')).toBe('1 Cup');
    expect(inGerman(1.5, 'l')).toBe('6⅓ Cups');
  });

  it('converts a mass the same way in both languages', () => {
    expect(inGerman(250, 'g')).toBe('8¾ oz');
    expect(shown(250, 'g')).toBe('8¾ oz');
  });
});
