import { closest, drifted, type ScaledQuantity } from './quantityMath';
import type { Unit } from './units';

/** How close to a whole number a count has to be before it is simply that number. */
const countSnap = 0.15;

/**
 * The pieces of one thing a kitchen has words for.
 *
 * A quarter is the floor: below that the honest answer for a countable
 * ingredient stops existing, and a quarter of an onion is the smallest piece
 * anybody is going to cut.
 */
const kitchenFractions = [0.25, 1 / 3, 0.5, 2 / 3, 0.75, 1];

/**
 * Countable things become an honest range rather than a fraction.
 *
 * Half of three cloves is not one and a half cloves; it is one or two, and the
 * cook decides.
 */
export function counted(exact: number, unit: Unit | null): ScaledQuantity {
  // Below one, the fraction is the answer. Half a recipe wants half an onion,
  // and saying "1 onion" instead is not a rounding — it is two and a half
  // times the onion, silently, in the one direction nobody checks. It is
  // written as a fraction a kitchen recognises rather than as `0.4`, and never
  // as nothing: a quarter is the smallest piece of a thing worth asking for.
  if (exact < 1) {
    const fraction = closest(exact, kitchenFractions);

    return {
      value: fraction,
      upper: null,
      unit,
      isApproximate: drifted(exact, fraction),
      isRange: false,
      exact
    };
  }

  const nearest = Math.round(exact);

  if (Math.abs(exact - nearest) <= countSnap) {
    const value = Math.max(1, nearest);

    return {
      value,
      upper: null,
      unit,
      isApproximate: drifted(exact, value),
      isRange: false,
      exact
    };
  }

  const lower = Math.max(1, Math.floor(exact));
  const upper = Math.max(lower + 1, Math.ceil(exact));

  return {
    value: lower,
    upper,
    unit,
    // A range is not an approximation: it states the truth, which is that
    // either amount will do.
    isApproximate: false,
    isRange: true,
    exact
  };
}
