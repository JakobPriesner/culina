import { closest, drifted, type ScaledQuantity } from './quantityMath';
import type { Unit } from './units';

/** How close to a whole number a count has to be before it is simply that number. */
const countSnap = 0.15;

/** The pieces a kitchen has words for; a quarter is the smallest worth cutting. */
const kitchenFractions = [0.25, 1 / 3, 0.5, 2 / 3, 0.75, 1];

/** Countable things become a range, not a fraction: half of three cloves is one or two. */
export function counted(exact: number, unit: Unit | null): ScaledQuantity {
  // Below one the fraction is the answer: "1 onion" for half a recipe would silently scale it 2.5x.
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
    // A range states the truth (either will do), so it is not an approximation.
    isApproximate: false,
    isRange: true,
    exact
  };
}
