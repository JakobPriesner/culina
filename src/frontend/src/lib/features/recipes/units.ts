import type { components } from '$api/generated/schema';

/** What an amount is measured in; an open vocabulary (see `BuiltInUnit`), so a household can add one. */
export type Unit = string;

/** The units that convert, taken from the backend's units response so the two cannot drift. */
export type BuiltInUnit = components['schemas']['RecipesGetUnitsResponse']['builtIn'][number];

/** The built-in units as a value; the `Record` makes a unit the backend adds a compile error here. */
const everyBuiltIn: Record<BuiltInUnit, true> = {
  g: true,
  kg: true,
  ml: true,
  l: true,
  tsp: true,
  tbsp: true,
  piece: true,
  clove: true,
  bunch: true,
  slice: true,
  can: true,
  pack: true,
  pinch: true
};

export const builtInUnits = Object.keys(everyBuiltIn) as readonly BuiltInUnit[];

export const isBuiltIn = (unit: Unit | null | undefined): unit is BuiltInUnit =>
  unit !== null && unit !== undefined && unit in everyBuiltIn;

export type UnitFamily = 'mass' | 'volume' | 'spoon' | 'count' | 'none';

const families: Partial<Record<BuiltInUnit, UnitFamily>> = {
  g: 'mass',
  kg: 'mass',
  ml: 'volume',
  l: 'volume',
  tsp: 'spoon',
  tbsp: 'spoon'
};

export function familyOf(unit: Unit | null | undefined): UnitFamily {
  if (!unit) {
    return 'none';
  }

  // Everything else counts things (pieces, cloves, a household's own units), so arithmetic never guesses what a Schuss weighs.
  // A pinch is a gesture, handled separately.
  return (isBuiltIn(unit) ? families[unit] : undefined) ?? 'count';
}

export const canonicalOf = (unit: Unit | null | undefined): Unit | null => {
  switch (familyOf(unit)) {
    case 'mass':
      return 'g';
    case 'volume':
      return 'ml';
    default:
      return unit ?? null;
  }
};

export const toCanonical = (unit: Unit | null | undefined): number =>
  unit === 'kg' || unit === 'l' ? 1000 : 1;

/** Whether two amounts can be added: mass and volume convert within themselves, everything else must match exactly. */
export function canCombine(left: Unit | null | undefined, right: Unit | null | undefined): boolean {
  const family = familyOf(left);

  if (family !== familyOf(right)) {
    return false;
  }

  return family === 'mass' || family === 'volume' || left === right;
}

/** A pinch is a gesture and does not scale, nor does an ingredient with no amount. */
export const scales = (unit: Unit | null | undefined): boolean => unit !== 'pinch';

export const largerUnit = (unit: Unit | null | undefined): Unit | null => {
  switch (unit) {
    case 'g':
      return 'kg';
    case 'ml':
      return 'l';
    default:
      return null;
  }
};
