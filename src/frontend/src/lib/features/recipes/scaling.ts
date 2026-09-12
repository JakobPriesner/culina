import { canonicalOf, familyOf, largerUnit, scales, toCanonical, type Unit } from './units';

/**
 * Every rule in `docs/scaling-rules.md`, and nothing else.
 *
 * Pure: no I/O, no formatting decisions that depend on a store, no clock. The
 * server does exact decimal arithmetic and stores shopping-list amounts
 * unrounded; human rounding is presentation and lives here, once, so the two
 * cannot drift.
 *
 * The rule underneath all of the others: **every amount is computed from the
 * base amount and the factor.** Scaling a value that was already scaled — and
 * therefore already rounded — drifts, and drifts differently depending on how
 * many times someone tapped the stepper.
 */
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
}

/** Beyond these, times and tins stop being right and the app says so. */
export const trustedFactorRange = { lowest: 0.6, highest: 1.75 } as const;

/** A yield of zero is not a recipe, and a thousand portions is a typo. */
const yieldLimits = { lowest: 0.0001, highest: 1000 } as const;

/** Rounding that moves an amount by more than this is an approximation. */
const approximationThreshold = 0.02;

/** How close to a whole number a count has to be before it is simply that number. */
const countSnap = 0.15;

export function factorFor(baseYield: number, targetYield: number): number {
  if (baseYield <= 0) {
    return 1;
  }

  return clampYield(targetYield) / baseYield;
}

export const clampYield = (value: number): number =>
  Math.min(yieldLimits.highest, Math.max(yieldLimits.lowest, value));

/**
 * Scales one amount and makes it something a person can act on.
 *
 * `133.333 g` is arithmetic; `1⅓ eggs` is honest and useless. This returns what
 * a cook would write down.
 */
export function scaleQuantity(base: Quantity, factor: number): ScaledQuantity {
  const unchanged: ScaledQuantity = {
    value: base.value,
    upper: null,
    unit: base.unit,
    isApproximate: false,
    isRange: false
  };

  // Nothing to scale: no amount given, or a pinch, which is a gesture.
  if (base.value === null || !scales(base.unit)) {
    return unchanged;
  }

  if (factor === 1) {
    return unchanged;
  }

  const exact = base.value * factor;

  switch (familyOf(base.unit)) {
    case 'mass':
    case 'volume':
      return measured(exact, base.unit!);
    case 'spoon':
      return spooned(exact, base.unit!);
    default:
      return counted(exact, base.unit ?? null);
  }
}

/**
 * Mass and volume snap to a step a kitchen scale can show, then re-express
 * upward when the number gets unwieldy.
 */
function measured(exact: number, unit: Unit): ScaledQuantity {
  const canonical = canonicalOf(unit)!;
  const inCanonical = exact * toCanonical(unit);
  const rounded = toStep(inCanonical, stepFor(inCanonical));

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
      isRange: false
    };
  }

  return {
    value: trim(rounded),
    upper: null,
    unit: canonical,
    isApproximate: drifted(inCanonical, rounded),
    isRange: false
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
 * Spoons round to halves, and to thirds, because measuring spoons exist in
 * those sizes and in no others.
 */
function spooned(exact: number, unit: Unit): ScaledQuantity {
  // Halves are the grid. Thirds are not an alternative grid to round onto —
  // they exist so that a recipe scaled by a third lands on the spoon that is
  // actually in the drawer, rather than being marked approximate for no
  // reason. So a third is used only when the arithmetic genuinely produced
  // one: 1.7 tbsp is a half and a half, not five thirds.
  const nearestThird = Math.round(exact * 3) / 3;

  const rounded =
    Math.abs(nearestThird - exact) <= thirdTolerance
      ? nearestThird
      : closest(exact, multiples(exact, 0.5));

  return {
    value: trim(Math.max(0, rounded)),
    upper: null,
    unit,
    isApproximate: drifted(exact, rounded),
    isRange: false
  };
}

/** How near a third an amount has to be before it is treated as one. */
const thirdTolerance = 0.02;

/** The two multiples of `step` either side of `exact`, never below zero. */
const multiples = (exact: number, step: number): number[] => [
  Math.max(0, Math.floor(exact / step) * step),
  Math.max(0, Math.ceil(exact / step) * step)
];

const closest = (exact: number, candidates: number[]): number =>
  candidates.reduce((best, candidate) =>
    Math.abs(candidate - exact) < Math.abs(best - exact) ? candidate : best
  );

/**
 * Countable things become an honest range rather than a fraction.
 *
 * Half of three cloves is not one and a half cloves; it is one or two, and the
 * cook decides. A count never rounds to zero: a recipe that needs an onion
 * still needs an onion at half scale.
 */
function counted(exact: number, unit: Unit | null): ScaledQuantity {
  // Below one there is no range to offer: the recipe needs the onion, and it
  // needs one of them, not "one or two".
  if (exact < 1) {
    return {
      value: 1,
      upper: null,
      unit,
      isApproximate: drifted(exact, 1),
      isRange: false
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
      isRange: false
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
    isRange: true
  };
}

/**
 * Chooses the yield at which this ingredient comes out at the amount you have.
 *
 * The leftover 600 g of flour, the odd package size. The same machinery, driven
 * from the other end.
 */
export function targetYieldForAmount(
  base: Quantity,
  available: Quantity,
  baseYield: number
): number | null {
  if (!base.value || !available.value || !scales(base.unit)) {
    return null;
  }

  if (familyOf(base.unit) !== familyOf(available.unit)) {
    return null;
  }

  const from = base.value * toCanonical(base.unit);
  const to = available.value * toCanonical(available.unit);

  if (from <= 0) {
    return null;
  }

  // Rounded to a half portion, and the factor is then recomputed from this
  // number — so what the person sees and what the amounts do agree.
  return clampYield(Math.round((to / from) * baseYield * 2) / 2);
}

/** True when rounding moved the amount by more than the threshold. */
const drifted = (exact: number, rounded: number): boolean =>
  exact !== 0 && Math.abs(rounded - exact) / Math.abs(exact) > approximationThreshold;

const toStep = (amount: number, step: number): number =>
  // Half away from zero, so 2.5 g at a 0.5 step is 2.5 and 7.25 is 7.5 — the
  // direction a cook rounds when they are already pouring.
  Math.sign(amount) * Math.round((Math.abs(amount) / step) * (1 + Number.EPSILON)) * step;

/** Kills the floating-point tail that multiplication leaves behind. */
const trim = (value: number): number => Number(value.toFixed(4));
