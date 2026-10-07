import { closest, drifted, multiples, trim, type ScaledQuantity } from './quantityMath';
import type { Unit } from './units';

/** Rounds spoons to halves and real spoon sizes (thirds, and quarter/eighth below a half so small amounts never become none). */
export function spooned(exact: number, unit: Unit): ScaledQuantity {
  // Halves are the grid; a third is used only when the arithmetic genuinely produced one (1.7 tbsp is not five thirds).
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

const thirdTolerance = 0.02;

const smallSpoons = [0.125, 0.25, 0.5];
