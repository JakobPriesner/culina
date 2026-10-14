import type { MeasurementSystem } from '$features/recipes/measurement';
import { drifted, type ScaledQuantity } from '$features/recipes/quantityMath';
import { roundQuantity, type Quantity } from '$features/recipes/scaling';
import { familyOf } from '$features/recipes/units';

/** How close to a whole number a count has to be before it is simply that number; further off, there is a part of one to buy. */
const countSnap = 0.15;

/**
 * A line's amount as a person standing at the shelf reads it: a kitchen scale's steps for weight and volume, spoons
 * as spoons, and whole things for counts (nobody buys 1.67 packets). The list keeps the exact sum; this is only what is shown.
 */
export function amountToBuy(quantity: Quantity, system: MeasurementSystem): ScaledQuantity {
  const family = familyOf(quantity.unit);

  if (quantity.value === null || (family !== 'count' && family !== 'none')) {
    return roundQuantity(quantity, system);
  }

  const nearest = Math.round(quantity.value);
  // A part of a thing is bought as the whole thing, so a count is rounded up, never to a range or a fraction.
  const whole =
    Math.abs(quantity.value - nearest) <= countSnap ? nearest : Math.ceil(quantity.value);
  const value = Math.max(1, whole);

  return {
    value,
    upper: null,
    unit: quantity.unit,
    isApproximate: drifted(quantity.value, value),
    isRange: false,
    exact: quantity.value
  };
}
