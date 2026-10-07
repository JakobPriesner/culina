import { fromGrams, fromMillilitres, toMeasure } from './measurement';
import { drifted, trim, type ScaledQuantity } from './quantityMath';
import { canonicalOf, largerUnit, toCanonical, type Unit } from './units';

/**
 * Mass and volume snap to a step a kitchen scale shows, then move to the larger unit when it reads
 * better.
 */
export function measured(exact: number, unit: Unit): ScaledQuantity {
  const canonical = canonicalOf(unit)!;
  const inCanonical = exact * toCanonical(unit);
  const rounded = orExact(toStep(inCanonical, stepFor(inCanonical)), inCanonical);

  const bigger = largerUnit(canonical);

  // Upward only: 1.5 kg reads like a recipe, 1.35 kg and 0.5 kg do not (a scale shows grams).
  if (bigger && readsBetterAs(rounded)) {
    return {
      value: trim(rounded / 1000),
      upper: null,
      unit: bigger,
      isApproximate: drifted(inCanonical, rounded),
      isRange: false,
      exact: inCanonical / 1000
    };
  }

  return {
    value: trim(rounded),
    upper: null,
    unit: canonical,
    isApproximate: drifted(inCanonical, rounded),
    isRange: false,
    exact: inCanonical
  };
}

/**
 * Mass and volume in US kitchen units, converted from the exact amount and rounded once.
 * Mass never becomes cups: a cup of flour is 120-150 g depending on packing.
 */
export function customary(exact: number, unit: Unit, isMass: boolean): ScaledQuantity {
  const inCanonical = exact * toCanonical(unit);
  const converted = isMass ? fromGrams(inCanonical) : fromMillilitres(inCanonical);
  const rounded = orExact(toMeasure(converted.value, converted.steps), converted.value);

  return {
    value: trim(rounded),
    upper: null,
    unit: converted.unit,
    isApproximate: drifted(converted.value, rounded),
    isRange: false,
    exact: converted.value
  };
}

/**
 * At least 1 of the larger unit with at most one decimal: 1500 becomes 1.5 kg, 1350 stays grams.
 */
const readsBetterAs = (canonicalAmount: number): boolean =>
  canonicalAmount >= 1000 && Number(((canonicalAmount / 1000) * 10).toFixed(6)) % 1 === 0;

function stepFor(amount: number): number {
  const magnitude = Math.abs(amount);

  if (magnitude < 10) {
    return 0.5;
  }

  if (magnitude < 100) {
    return 5;
  }

  return magnitude < 1000 ? 10 : 50;
}

/**
 * Rounding never turns a small amount (0.2 g of saffron) into zero, which would change what is
 * cooked.
 */
const orExact = (rounded: number, exact: number): number => (rounded === 0 ? exact : rounded);

const toStep = (amount: number, step: number): number =>
  // Half away from zero, the way a cook rounds while pouring.
  Math.sign(amount) * Math.round((Math.abs(amount) / step) * (1 + Number.EPSILON)) * step;
