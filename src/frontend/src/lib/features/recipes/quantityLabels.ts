import { m } from '$shell/i18n';

import type { QuantityLabels } from './formatQuantity';
import { builtInUnits, isBuiltIn, type BuiltInUnit, type Unit } from './units';

/**
 * The words that go beside an amount, in the reader's language.
 *
 * One copy for the whole app. Three screens used to keep their own — and the
 * shopping list, whose copy said every unit was nameless, showed a pinch of
 * salt as a bare `1`. Anywhere a quantity is rendered, it is rendered with
 * these.
 *
 * Every unit but `piece` is named. A bare number is only unambiguous when the
 * thing itself is the unit — four courgettes are `4 Zucchini` — and for
 * everything else the word carries information the number does not: `2 Feta` is
 * not the same shopping trip as `2 Packungen Feta`.
 *
 * Singular and plural are separate messages rather than a suffix: `Dose` and
 * `Dosen` are not `can` and `cans`, and a rule that appends an `s` is a rule
 * that only works in one language.
 */
const unitNames: Partial<Record<BuiltInUnit, readonly [one: () => string, many: () => string]>> = {
  // Spoons are abbreviated, but not the same way in every language: a German
  // recipe says EL and TL, and `2 tbsp` in an otherwise German list is the kind
  // of half-translated detail that makes an app feel imported. Neither language
  // pluralises the abbreviation.
  tsp: [m['units.tsp'], m['units.tsp']],
  tbsp: [m['units.tbsp'], m['units.tbsp']],
  pinch: [m['units.pinch'], m['units.pinch']],
  clove: [m['units.clove'], m['units.cloves']],
  bunch: [m['units.bunch'], m['units.bunches']],
  slice: [m['units.slice'], m['units.slices']],
  can: [m['units.can'], m['units.cans']],
  pack: [m['units.pack'], m['units.packs']]
};

export const quantityLabels: QuantityLabels = {
  unitName: (unit: Unit, count: number) => {
    // A unit a household wrote has no name here, and needs none: it is its own
    // label, and `formatQuantity` shows it as it was typed.
    const names = unitNames[unit as BuiltInUnit];

    return names ? (count === 1 ? names[0]() : names[1]()) : '';
  },
  /** A tilde, for an amount that rounding moved off the arithmetic. */
  approximately: (amount: string) => `~${amount}`
};

/**
 * What each unit is called in a picker, where there is no amount beside it.
 *
 * `formatQuantity` leaves several units blank on purpose — "3 cloves garlic"
 * reads worse than "3 Knoblauchzehen", so `clove` contributes nothing next to
 * a number and lets the ingredient carry it. A list of units to choose from
 * has no ingredient to lean on, so here every one of them needs a word.
 *
 * A `Record` rather than a partial one, for the same reason the vocabulary
 * itself is a record: a unit the backend adds and this table forgets is a
 * compile error instead of a blank row in a dropdown.
 */
const pickerNames: Record<BuiltInUnit, () => string> = {
  g: () => 'g',
  kg: () => 'kg',
  ml: () => 'ml',
  l: () => 'l',
  piece: m['units.piece'],
  tsp: m['units.tsp'],
  tbsp: m['units.tbsp'],
  pinch: m['units.pinch'],
  clove: m['units.clove'],
  bunch: m['units.bunch'],
  slice: m['units.slice'],
  can: m['units.can'],
  pack: m['units.pack']
};

/** The singular name of a unit, for a list of units to pick from. */
export const unitLabel = (unit: Unit): string => (isBuiltIn(unit) ? pickerNames[unit]() : unit);

/**
 * The unit a typed word means, which is the way back from `unitLabel`.
 *
 * A picker shows words and a recipe stores codes: somebody choosing `Zehe` has
 * chosen `clove`, and storing the German would make the same unit two units as
 * soon as an English speaker opened the recipe. The code is also accepted as
 * typed, so a person who knows `tbsp` can simply write it.
 *
 * Anything else is returned untouched, because anything else is a unit this
 * household invented and there is nothing to translate it to.
 */
export const unitFor = (written: string): Unit => {
  const wanted = written.trim();
  const folded = wanted.toLowerCase();

  return (
    builtInUnits.find(
      (unit) => unit.toLowerCase() === folded || pickerNames[unit]().toLowerCase() === folded
    ) ?? wanted
  );
};
