import { fromGrams, fromMillilitres, toMeasure } from './measurement';
import { drifted, trim, type ScaledQuantity } from './quantityMath';
import { canonicalOf, largerUnit, toCanonical, type Unit } from './units';

/**
 * Mass and volume snap to a step a kitchen scale can show, then re-express
 * upward when the number gets unwieldy.
 */
export function measured(exact: number, unit: Unit): ScaledQuantity {
  const canonical = canonicalOf(unit)!;
  const inCanonical = exact * toCanonical(unit);
  const rounded = orExact(toStep(inCanonical, stepFor(inCanonical)), inCanonical);

  const bigger = largerUnit(canonical);

  // Upward only, and only when the bigger unit reads better. `1.5 kg` is how a
  // recipe writes 1500 g; `1.35 kg` is not how anyone writes 1350 g, and
  // `0.5 kg` is not how anyone writes 500 g — a scale shows grams.
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
 * Mass and volume, in the units a US kitchen owns.
 *
 * Converted from the exact amount and rounded once, onto a measure that is
 * actually in the drawer. Mass becomes ounces and pounds and never cups: a cup
 * of flour is between 120 g and 150 g depending on how it was packed, and
 * turning 250 g into "2 cups" is a confidently wrong recipe.
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
 * Whether an amount is better said in the larger unit.
 *
 * At least one of it, and no more than one decimal place: 1500 becomes 1.5 kg,
 * 1350 stays 1350 g.
 */
const readsBetterAs = (canonicalAmount: number): boolean =>
  canonicalAmount >= 1000 && Number(((canonicalAmount / 1000) * 10).toFixed(6)) % 1 === 0;

/** The step a number of this magnitude should land on. */
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
 * Rounding never makes an amount disappear.
 *
 * Below the smallest step — 0.2 g of saffron, 2 g of yeast in ounces — the
 * grid has nothing between zero and an amount several times too much, and
 * either would change what is cooked. The arithmetic is the honest answer.
 */
const orExact = (rounded: number, exact: number): number => (rounded === 0 ? exact : rounded);

const toStep = (amount: number, step: number): number =>
  // Half away from zero, so 2.5 g at a 0.5 step is 2.5 and 7.25 is 7.5 — the
  // direction a cook rounds when they are already pouring.
  Math.sign(amount) * Math.round((Math.abs(amount) / step) * (1 + Number.EPSILON)) * step;
