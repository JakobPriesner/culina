import type { components } from '$api/generated/schema';

/**
 * What an amount is measured in.
 *
 * A plain code, because the vocabulary is open: thirteen units are built in and
 * a household adds one by writing it. See `BuiltInUnit` for the ones that
 * convert.
 */
export type Unit = string;

/**
 * The units that convert, taken from the backend rather than redeclared.
 *
 * A kilo is a thousand grams for everyone, so this half of the vocabulary is
 * shared and closed. The server publishes it on the units response, which is
 * the one place it has to appear — and taking it from there is what stops this
 * file and the server from drifting apart.
 */
export type BuiltInUnit = components['schemas']['RecipesGetUnitsResponse']['builtIn'][number];

/**
 * The same vocabulary as a value, so a test can walk it.
 *
 * A record rather than an array on purpose: the type checker insists a
 * `Record<BuiltInUnit, …>` name every unit, so a unit the backend adds and this
 * file forgets is a compile error instead of a gap nothing notices.
 */
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

/** Whether this is one of the units that convert. */
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

  // Everything not named above counts things: pieces, cloves, cans — and every
  // unit a household wrote itself, which is what makes an open vocabulary safe.
  // A counting unit scales and adds to itself, and the arithmetic never has to
  // guess what a Schuss weighs. A pinch is handled separately: it is a gesture,
  // not a quantity.
  return (isBuiltIn(unit) ? families[unit] : undefined) ?? 'count';
}

/** The unit a family is measured in before it is made readable again. */
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

/** How many canonical units one of this unit is worth. */
export const toCanonical = (unit: Unit | null | undefined): number =>
  unit === 'kg' || unit === 'l' ? 1000 : 1;

/**
 * Whether two amounts in these units can be added at all.
 *
 * Mass and volume convert within themselves; everything else, spoons and counts
 * and a household's own units alike, has to match exactly.
 */
export function canCombine(left: Unit | null | undefined, right: Unit | null | undefined): boolean {
  const family = familyOf(left);

  if (family !== familyOf(right)) {
    return false;
  }

  return family === 'mass' || family === 'volume' || left === right;
}

/**
 * Whether scaling this amount means anything.
 *
 * A pinch is a gesture. Doubling a recipe does not double the pinch of salt,
 * and an ingredient with no amount at all — "salt", "pepper to taste" — has
 * nothing to scale.
 */
export const scales = (unit: Unit | null | undefined): boolean => unit !== 'pinch';

/** The larger unit a family re-expresses into, when the number gets big. */
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
