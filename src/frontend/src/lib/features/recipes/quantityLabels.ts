import { m } from '$shell/i18n';

import type { QuantityLabels } from './formatQuantity';
import type { Unit } from './units';

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
const unitNames: Partial<Record<Unit, readonly [one: () => string, many: () => string]>> = {
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
    const names = unitNames[unit];

    return names ? (count === 1 ? names[0]() : names[1]()) : '';
  },
  /** A tilde, for an amount that rounding moved off the arithmetic. */
  approximately: (amount: string) => `~${amount}`
};
