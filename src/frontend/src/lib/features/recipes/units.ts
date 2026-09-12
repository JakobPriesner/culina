import type { components } from '$api/generated/schema';

/**
 * The unit vocabulary, taken from the backend enum rather than redeclared.
 *
 * The families below are the client's business — they describe how an amount is
 * *shown*, which the server has no opinion about — but the set of units is one
 * definition on both sides.
 */
export type Unit = NonNullable<components['schemas']['RecipesIngredientContract']['unit']>;

/**
 * The same vocabulary as a value, so a test can walk it.
 *
 * A record rather than an array on purpose: the type checker insists a
 * `Record<Unit, …>` name every unit, so a unit the backend adds and this file
 * forgets is a compile error instead of a gap nothing notices.
 */
const everyUnit: Record<Unit, true> = {
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

export const units = Object.keys(everyUnit) as readonly Unit[];

export type UnitFamily = 'mass' | 'volume' | 'spoon' | 'count' | 'none';

const families: Partial<Record<Unit, UnitFamily>> = {
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

  // Everything not named above counts things: pieces, cloves, cans. A pinch is
  // handled separately — it is a gesture, not a quantity.
  return families[unit] ?? 'count';
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
