import { closest, drifted, multiples, trim, type ScaledQuantity } from './quantityMath';
import type { Unit } from './units';

/**
 * Spoons round to halves, and to thirds, because measuring spoons exist in
 * those sizes and in no others — and below a half, to the quarter and eighth
 * spoons, so a small amount stays a small amount instead of becoming none.
 */
export function spooned(exact: number, unit: Unit): ScaledQuantity {
  // Halves are the grid. Thirds are not an alternative grid to round onto —
  // they exist so that a recipe scaled by a third lands on the spoon that is
  // actually in the drawer, rather than being marked approximate for no
  // reason. So a third is used only when the arithmetic genuinely produced
  // one: 1.7 tbsp is a half and a half, not five thirds.
  const nearestThird = Math.round(exact * 3) / 3;

  const rounded =
    nearestThird > 0 && Math.abs(nearestThird - exact) <= thirdTolerance
      ? nearestThird
      : closest(exact, exact < 0.5 ? smallSpoons : multiples(exact, 0.5));

  return {
    value: trim(rounded),
    upper: null,
    unit,
    isApproximate: drifted(exact, rounded),
    isRange: false,
    exact
  };
}

/** How near a third an amount has to be before it is treated as one. */
const thirdTolerance = 0.02;

/** The spoons below a half. An eighth is the floor: nothing rounds to no spoon. */
const smallSpoons = [0.125, 0.25, 0.5];
