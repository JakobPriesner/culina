import { fromGrams, fromMillilitres, toMeasure, type MeasurementSystem } from './measurement';
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
  /**
   * The arithmetic, before any of it was made readable — in the unit of
   * `value`, so the two can be compared directly.
   *
   * Kept so that an approximation can say what it approximated, and so that
   * nothing downstream has to re-derive it from a number that was already
   * rounded. Scaling an already-scaled amount drifts, and drifts differently
   * depending on how many times somebody tapped the stepper.
   */
  readonly exact: number | null;
}

/** Beyond these, times and tins stop being right and the app says so. */
export const trustedFactorRange = { lowest: 0.6, highest: 1.75 } as const;

/** A yield of zero is not a recipe, and a thousand portions is a typo. */
const yieldLimits = { lowest: 0.0001, highest: 1000 } as const;

/** Rounding that moves an amount by more than this is an approximation. */
const approximationThreshold = 0.02;

/** How close to a whole number a count has to be before it is simply that number. */
const countSnap = 0.15;

/**
 * How much of the recipe is being made.
 *
 * Both numbers are checked rather than trusted: a base yield of zero, or a
 * target that arrived from a URL as `abc`, would otherwise produce a factor of
 * `NaN` and turn every amount on the page into `NaN` — which looks like the app
 * forgetting the recipe rather than like bad input.
 */
export function factorFor(baseYield: number, targetYield: number): number {
  if (!Number.isFinite(baseYield) || baseYield <= 0 || !Number.isFinite(targetYield)) {
    return 1;
  }

  return clampYield(targetYield) / baseYield;
}

export const clampYield = (value: number): number =>
  Number.isFinite(value)
    ? Math.min(yieldLimits.highest, Math.max(yieldLimits.lowest, value))
    : yieldLimits.lowest;

/**
 * Scales one amount and makes it something a person can act on.
 *
 * `133.333 g` is arithmetic; `1⅓ eggs` is honest and useless. This returns what
 * a cook would write down.
 *
 * The measurement system is applied here rather than afterwards, because
 * converting a number that has already been rounded rounds it twice — and a
 * quarter-ounce of drift on each of a recipe's twelve amounts is a different
 * recipe. It touches mass and volume only: a teaspoon is a teaspoon in both
 * systems, and two onions are two onions.
 */
export function scaleQuantity(
  base: Quantity,
  factor: number,
  system: MeasurementSystem = 'metric'
): ScaledQuantity {
  const unchanged: ScaledQuantity = {
    value: base.value,
    upper: null,
    unit: base.unit,
    isApproximate: false,
    isRange: false,
    exact: base.value
  };

  // Nothing to scale: no amount given, or a pinch, which is a gesture.
  if (base.value === null || !scales(base.unit)) {
    return unchanged;
  }

  const family = familyOf(base.unit);
  const converts = system === 'imperial' && (family === 'mass' || family === 'volume');

  if (factor === 1 && !converts) {
    return unchanged;
  }

  const exact = base.value * factor;

  if (converts) {
    return customary(exact, base.unit!, family === 'mass');
  }

  switch (family) {
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
function customary(exact: number, unit: Unit, isMass: boolean): ScaledQuantity {
  const inCanonical = exact * toCanonical(unit);
  const converted = isMass ? fromGrams(inCanonical) : fromMillilitres(inCanonical);
  const rounded = toMeasure(converted.value, converted.steps);

  return {
    value: trim(Math.max(0, rounded)),
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
    isRange: false,
    exact
  };
}

/** How near a third an amount has to be before it is treated as one. */
const thirdTolerance = 0.02;

/**
 * The pieces of one thing a kitchen has words for.
 *
 * A quarter is the floor: below that the honest answer for a countable
 * ingredient stops existing, and a quarter of an onion is the smallest piece
 * anybody is going to cut.
 */
const kitchenFractions = [0.25, 1 / 3, 0.5, 2 / 3, 0.75, 1];

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
 * cook decides.
 */
function counted(exact: number, unit: Unit | null): ScaledQuantity {
  // Below one, the fraction is the answer. Half a recipe wants half an onion,
  // and saying "1 onion" instead is not a rounding — it is two and a half
  // times the onion, silently, in the one direction nobody checks. It is
  // written as a fraction a kitchen recognises rather than as `0.4`, and never
  // as nothing: a quarter is the smallest piece of a thing worth asking for.
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
    // A range is not an approximation: it states the truth, which is that
    // either amount will do.
    isApproximate: false,
    isRange: true,
    exact
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

  // Exact, and deliberately not rounded. Rounding here to a tidy number of
  // servings would move the amount away from the one the person said they had:
  // 370 g of flour became a recipe calling for 380 g, with 370 nowhere on the
  // screen. The label rounds instead — see `yieldLabel` — so what is shown
  // stays readable and what is cooked stays exactly what was asked for.
  // Trimmed only of the floating-point tail, which is far below anything the
  // amounts can show and would otherwise put `7.400000000000001` in a URL.
  return clampYield(Number((((to / from) * baseYield) as number).toFixed(6)));
}

/**
 * The number of servings to put on screen.
 *
 * Scaling to an amount produces a yield like 7.4, which is true and is not what
 * anybody wants to read. This is the only place a yield is rounded, and it
 * rounds nothing that is used in arithmetic.
 */
export const yieldLabel = (value: number): number =>
  Number.isFinite(value) ? Math.round(value * 2) / 2 : 0;

/** True when rounding moved the amount by more than the threshold. */
const drifted = (exact: number, rounded: number): boolean =>
  exact !== 0 && Math.abs(rounded - exact) / Math.abs(exact) > approximationThreshold;

const toStep = (amount: number, step: number): number =>
  // Half away from zero, so 2.5 g at a 0.5 step is 2.5 and 7.25 is 7.5 — the
  // direction a cook rounds when they are already pouring.
  Math.sign(amount) * Math.round((Math.abs(amount) / step) * (1 + Number.EPSILON)) * step;

/** Kills the floating-point tail that multiplication leaves behind. */
const trim = (value: number): number => Number(value.toFixed(4));
