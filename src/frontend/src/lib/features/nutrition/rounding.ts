/**
 * Rounding as a package label does it (European Commission, guidance on tolerances for nutrition
 * labelling, 2012). Presentation only: the server sends unrounded numbers.
 * A lower bound is rounded DOWN, because a lower bound rounded up is no longer one; an exact value is
 * rounded half up.
 */
export type Nutrient = 'energy' | 'macro' | 'saturates' | 'salt';

interface Rule {
  /** At or above this, `coarse` decimals; below, `fine`. */
  readonly coarseFrom: number;
  readonly coarse: number;
  readonly fine: number;
  /** Below this, a label says "0". */
  readonly zeroBelow: number;
}

const rules: Record<Nutrient, Rule> = {
  // Kilojoules and kilocalories, to the whole number, however small.
  energy: { coarseFrom: 0, coarse: 0, fine: 0, zeroBelow: 0 },
  // Fat, carbohydrate, sugars and protein.
  macro: { coarseFrom: 10, coarse: 0, fine: 1, zeroBelow: 0.5 },
  saturates: { coarseFrom: 10, coarse: 0, fine: 1, zeroBelow: 0.1 },
  salt: { coarseFrom: 1, coarse: 1, fine: 2, zeroBelow: 0.0125 }
};

export interface Rounded {
  readonly value: number;
  /** How many decimals it is printed with, so 3 g and 3.0 g do not mix in one column. */
  readonly decimals: number;
}

/** Floating point sums sit a hair under their true value (0.29 * 100 is 28.999999999999996). */
const hair = 1e-9;

export function roundForLabel(nutrient: Nutrient, value: number, atLeast: boolean): Rounded {
  const rule = rules[nutrient];

  if (value < rule.zeroBelow) {
    return { value: 0, decimals: rule.fine };
  }

  const decimals = value >= rule.coarseFrom ? rule.coarse : rule.fine;
  const scale = 10 ** decimals;
  const steps = atLeast ? Math.floor(value * scale + hair) : Math.floor(value * scale + 0.5 + hair);

  return { value: steps / scale, decimals };
}
