import type { Unit } from './units';

export interface Quantity {
  /** Null when the recipe does not say how much. */
  readonly value: number | null;
  readonly unit: Unit | null;
}

export interface ScaledQuantity {
  /** The rounded amount, or the lower bound of a range. */
  readonly value: number | null;
  /** The upper bound, when the honest answer is a range. */
  readonly upper: number | null;
  readonly unit: Unit | null;
  /** True when rounding moved the value by more than 2%. */
  readonly isApproximate: boolean;
  readonly isRange: boolean;
  /** The unrounded amount in the unit of `value`; kept so nothing re-scales an already-rounded number, which drifts. */
  readonly exact: number | null;
}

/** Rounding that moves an amount by more than this is an approximation. */
const approximationThreshold = 0.02;

export const drifted = (exact: number, rounded: number): boolean =>
  exact !== 0 && Math.abs(rounded - exact) / Math.abs(exact) > approximationThreshold;

/** Kills the floating-point tail that multiplication leaves behind. */
export const trim = (value: number): number => Number(value.toFixed(4));

/** The two multiples of `step` either side of `exact`, never below zero. */
export const multiples = (exact: number, step: number): number[] => [
  Math.max(0, Math.floor(exact / step) * step),
  Math.max(0, Math.ceil(exact / step) * step)
];

export const closest = (exact: number, candidates: number[]): number =>
  candidates.reduce((best, candidate) =>
    Math.abs(candidate - exact) < Math.abs(best - exact) ? candidate : best
  );
