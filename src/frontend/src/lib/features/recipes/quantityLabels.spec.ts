import { describe, expect, it } from 'vitest';

import { formatQuantity } from './formatQuantity';
import { quantityLabels, unitFor, unitLabel } from './quantityLabels';
import { scaleQuantity } from './scaling';
import { builtInUnits } from './units';

/*
 * The app's real labels, as opposed to the stubs `formatQuantity.spec.ts` uses.
 *
 * Every screen that shows an amount shares this one object. It did not always:
 * the shopping list kept a copy that said no unit had a name, and a pinch of
 * salt appeared on it as a bare `1`.
 */
const show = (value: number, unit: Parameters<typeof scaleQuantity>[0]['unit']) =>
  formatQuantity(scaleQuantity({ value, unit }, 1), 'en', quantityLabels).text;

describe('the shared quantity labels', () => {
  it('name a pinch, because "1 salt" is not a thing anyone writes', () => {
    // Joined by a non-breaking space, escaped rather than typed: an invisible
    // character in source is a character nobody can see in a diff.
    expect(show(1, 'pinch')).toMatch(/^1\u00a0\S+/);
  });

  it('leave a piece count bare, because the thing itself is the unit', () => {
    expect(show(2, 'piece')).toBe('2');
  });

  it('name every other unit, because the word carries what the number cannot', () => {
    // `2 Feta` and `2 packs Feta` are different shopping trips.
    expect(show(2, 'pack')).toMatch(/^2\u00a0\S+/);
    expect(show(3, 'clove')).toMatch(/^3\u00a0\S+/);
  });

  it('pluralise, because "2 Dose" is not German', () => {
    expect(show(1, 'can')).not.toBe(show(2, 'can').replace('2', '1'));
  });

  it('give a word or a short form to every unit but the bare count', () => {
    // A unit that renders as nothing is information silently dropped: `600` on
    // a shopping list is not an amount of anything.
    for (const unit of builtInUnits.filter((candidate) => candidate !== 'piece')) {
      expect(show(2, unit), unit).not.toBe('2');
    }
  });

  it('show a unit a household wrote exactly as it was written', () => {
    // It has no translation and no abbreviation — it is its own label, and
    // anything else would be the app renaming somebody's kitchen.
    expect(show(2, 'Schuss')).toBe('2\u00a0Schuss');
  });
});

describe('the words a unit picker shows', () => {
  it('gives every unit a word, including the ones an amount leaves bare', () => {
    // `piece` and `clove` contribute nothing beside a number, because the
    // ingredient carries them — "3 garlic cloves". A list of units to pick from
    // has no ingredient to lean on, so a blank row there is an unpickable row.
    for (const unit of builtInUnits) {
      expect(unitLabel(unit)).not.toBe('');
    }
  });

  it('reads its own words back as the units they name', () => {
    // The picker shows words and the recipe stores codes, so the trip has to
    // survive both ways: somebody choosing "Zehe" has chosen `clove`, and
    // storing the German would make one unit into two the moment an English
    // speaker opened the recipe. Asserted over every unit rather than a sample,
    // because it is the whole table that has to hold.
    for (const unit of builtInUnits) {
      expect(unitFor(unitLabel(unit))).toBe(unit);
    }
  });

  it('takes the code itself, for somebody who simply types "tbsp"', () => {
    expect(unitFor('tbsp')).toBe('tbsp');
    expect(unitFor('  KG ')).toBe('kg');
  });

  it('leaves a word it does not know alone, which is how a unit is invented', () => {
    expect(unitFor('Schuss')).toBe('Schuss');
    expect(unitLabel('Schuss')).toBe('Schuss');
  });
});
