import { describe, expect, it } from 'vitest';

import { formatQuantity } from './formatQuantity';
import { quantityLabels, unitFor, unitLabel } from './quantityLabels';
import { scaleQuantity } from './scaling';
import { builtInUnits } from './units';

/* The app's real labels, not the stubs of `formatQuantity.spec.ts`; every amount screen shares this one object (the shopping list once kept a copy and showed a pinch of salt as a bare `1`). */
const show = (value: number, unit: Parameters<typeof scaleQuantity>[0]['unit']) =>
  formatQuantity(scaleQuantity({ value, unit }, 1), 'en', quantityLabels).text;

describe('the shared quantity labels', () => {
  it('name a pinch, because "1 salt" is not a thing anyone writes', () => {
    // Joined by a non-breaking space, escaped rather than typed: an invisible character is invisible in a diff.
    expect(show(1, 'pinch')).toMatch(/^1\u00a0\S+/);
  });

  it('leave a piece count bare, because the thing itself is the unit', () => {
    expect(show(2, 'piece')).toBe('2');
  });

  it('name every other unit, because the word carries what the number cannot', () => {
    expect(show(2, 'pack')).toMatch(/^2\u00a0\S+/);
    expect(show(3, 'clove')).toMatch(/^3\u00a0\S+/);
  });

  it('pluralise, because "2 Dose" is not German', () => {
    expect(show(1, 'can')).not.toBe(show(2, 'can').replace('2', '1'));
  });

  it('give a word or a short form to every unit but the bare count', () => {
    // A unit that renders as nothing silently drops information: `600` on a shopping list is no amount.
    for (const unit of builtInUnits.filter((candidate) => candidate !== 'piece')) {
      expect(show(2, unit), unit).not.toBe('2');
    }
  });

  it('show a unit a household wrote exactly as it was written', () => {
    // No translation or abbreviation: it is its own label, and anything else renames somebody's kitchen.
    expect(show(2, 'Schuss')).toBe('2\u00a0Schuss');
  });
});

describe('the words a unit picker shows', () => {
  it('gives every unit a word, including the ones an amount leaves bare', () => {
    // `piece` and `clove` add nothing beside a number (the ingredient carries them), but a picker has no ingredient, so a blank row is unpickable.
    for (const unit of builtInUnits) {
      expect(unitLabel(unit)).not.toBe('');
    }
  });

  it('reads its own words back as the units they name', () => {
    // The picker shows words and the recipe stores codes: storing "Zehe" would split one unit into two for an English reader. Asserted over every unit.
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

  it('reads a built-in written out as the built-in, so it converts like one', () => {
    expect(unitFor('Milliliter')).toBe('ml');
    expect(unitFor('Esslöffel')).toBe('tbsp');
    expect(unitFor('Gramm')).toBe('g');
  });
});
