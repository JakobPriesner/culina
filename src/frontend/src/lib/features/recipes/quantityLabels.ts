import { m } from '$shell/i18n';

import type { QuantityLabels } from './formatQuantity';
import type { CustomaryUnit } from './measurement';
import { spelledUnit } from './unitSpellings';
import { builtInUnits, isBuiltIn, type BuiltInUnit, type Unit } from './units';

/**
 * Words beside an amount, one copy for the whole app. Every unit but `piece` is named, since "2 Feta" is not "2 Packungen Feta".
 * Singular and plural are separate messages: `Dose`/`Dosen` is not an `s` suffix rule.
 */
type Named = BuiltInUnit | CustomaryUnit;

const unitNames: Partial<Record<Named, readonly [one: () => string, many: () => string]>> = {
  // Spoons are abbreviated differently per language (EL/TL vs tbsp/tsp); neither pluralises.
  tsp: [m['units.tsp'], m['units.tsp']],
  tbsp: [m['units.tbsp'], m['units.tbsp']],
  pinch: [m['units.pinch'], m['units.pinch']],
  clove: [m['units.clove'], m['units.cloves']],
  bunch: [m['units.bunch'], m['units.bunches']],
  slice: [m['units.slice'], m['units.slices']],
  can: [m['units.can'], m['units.cans']],
  pack: [m['units.pack'], m['units.packs']],
  // The one customary unit that is a word, not an abbreviation: "2 cups", never "2 cup".
  cup: [m['units.cup'], m['units.cups']]
};

export const quantityLabels: QuantityLabels = {
  unitName: (unit: Unit, count: number) => {
    // A household's own unit is its own label, shown as typed.
    const names = unitNames[unit as Named];

    return names ? (count === 1 ? names[0]() : names[1]()) : '';
  },
  approximately: (amount: string) => `~${amount}`
};

/** Unit names for a picker, where there is no ingredient to lean on; a full `Record` so a new backend unit is a compile error. */
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

export const unitLabel = (unit: Unit): string => (isBuiltIn(unit) ? pickerNames[unit]() : unit);

/**
 * The unit a typed word means, the way back from `unitLabel`: picker words, codes and spelled-out names map to built-ins (`Zehe` stores `clove`).
 * Anything else is a household's own unit and is returned untouched.
 */
export const unitFor = (written: string): Unit => {
  const wanted = written.trim();
  const folded = wanted.toLowerCase();

  return (
    builtInUnits.find(
      (unit) => unit.toLowerCase() === folded || pickerNames[unit]().toLowerCase() === folded
    ) ??
    spelledUnit(wanted) ??
    wanted
  );
};
