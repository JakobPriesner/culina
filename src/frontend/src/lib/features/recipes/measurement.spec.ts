import { afterEach, describe, expect, it } from 'vitest';

import { applyLocale } from '$shell/i18n';

import { formatQuantity } from './formatQuantity';
import { quantityLabels } from './quantityLabels';
import { scaleQuantity } from './scaling';

/*
 * A metric recipe, shown in the units a US kitchen owns.
 *
 * The danger is not arithmetic — it is confidence. A conversion that turns 250
 * grams of flour into "2 cups" produces a recipe that is wrong in a way nobody
 * can see, and an unfamiliar recipe beats a confidently wrong one.
 *
 * The tilde in several of these is the app saying "I rounded this". It is not
 * noise to be designed away: a cook who can see that an amount was moved knows
 * which amounts to trust to the gram.
 */

/**
 * What is rendered, with its non-breaking space made visible.
 *
 * An amount and its unit are joined by one so that `250` never wraps away from
 * `g`. Swapping it for an ordinary space here keeps the expectations below
 * readable — an invisible character in an assertion is a character nobody can
 * see in a diff.
 */
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
    // 227 g is a hair over eight ounces, and eight is what a recipe says.
    expect(shown(227, 'g')).toBe('8 oz');
  });

  it('becomes pounds once there are enough of them', () => {
    // Nobody writes "24 oz of beef".
    expect(shown(680, 'g')).toBe('1½ lb');
    // A kilo is 2.2 lb, and the nearest quarter is 2¼ — a two per cent move,
    // which the tilde says out loud rather than hides.
    expect(shown(1, 'kg')).toBe('~2¼ lb');
  });

  it('never becomes cups', () => {
    // The whole point. A cup of flour is between 120 g and 150 g depending on
    // how it was packed, so 250 g "in cups" is a number nobody can act on.
    expect(shown(250, 'g')).not.toContain('cup');
    expect(shown(1, 'kg')).not.toContain('cup');
  });

  it('lands on a fraction a scale can show', () => {
    // Quarters, not decimals: "4¼ oz", never "4.23 oz".
    expect(shown(120, 'g')).toBe('4¼ oz');
  });

  it('keeps halves right up to a pound', () => {
    // 300 g is 10.58 oz. "10½ oz" is what a recipe says; rounding it to 11 is
    // a four per cent lie for no gain at all.
    expect(shown(300, 'g')).toBe('10½ oz');
  });
});

describe('volume, which converts honestly', () => {
  it('becomes fluid ounces below a cup', () => {
    expect(shown(120, 'ml')).toBe('4 fl oz');
  });

  it('becomes cups above one', () => {
    expect(shown(240, 'ml')).toBe('1 cup');
    // Half a litre is 2.11 cups, and 2⅛ is no measure anybody owns. "About two
    // cups" is the honest answer, and the tilde is how it says so.
    expect(shown(500, 'ml')).toBe('~2 cups');
  });

  it('lands on a measure that is in the drawer', () => {
    // A third-cup measure is in every set, which is exactly why a cup amount
    // must not be rounded onto quarters alone.
    expect(shown(320, 'ml')).toBe('1⅓ cups');
  });
});

describe('what it deliberately leaves alone', () => {
  it('keeps a spoon a spoon', () => {
    // A teaspoon is a teaspoon in both systems, which is why the app refuses to
    // turn one into millilitres either.
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
    // "Salt" has no amount, and there is nothing to convert.
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
    // 200 g at 1.4 is 280 g, which is 9.88 oz. Rounding to grams first would
    // reach the same answer here — the point is that it does so by luck, and
    // rounding twice drifts differently on every amount in a recipe. The move
    // to ten is one per cent, which is inside the threshold and unmarked.
    expect(shown(200, 'g', 1.4)).toBe('10 oz');
  });

  it('scales into the larger unit when the amount asks for it', () => {
    // Doubling 300 g is 600 g, which is 21 oz — past a pound, so a recipe
    // stops counting in ounces and starts counting in pounds.
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
