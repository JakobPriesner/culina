import type { Unit } from './units';

export interface Quantity {
  readonly value: number | null;
  readonly unit: Unit | null;
}

export interface ScaledQuantity {
  readonly value: number | null;
  readonly upper: number | null;
  readonly unit: Unit | null;
  readonly isApproximate: boolean;
  readonly isRange: boolean;
  /** The unrounded amount in the unit of `value`; kept so nothing re-scales an already-rounded number, which drifts. */
  readonly exact: number | null;
}

const approximationThreshold = 0.02;

export const drifted = (exact: number, rounded: number): boolean =>
  exact !== 0 && Math.abs(rounded - exact) / Math.abs(exact) > approximationThreshold;

export const trim = (value: number): number => Number(value.toFixed(4));

export const multiples = (exact: number, step: number): number[] => [
  Math.max(0, Math.floor(exact / step) * step),
  Math.max(0, Math.ceil(exact / step) * step)
];

export const closest = (exact: number, candidates: number[]): number =>
  candidates.reduce((best, candidate) =>
    Math.abs(candidate - exact) < Math.abs(best - exact) ? candidate : best
  );
