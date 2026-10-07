import { type MeasurementSystem } from './measurement';
import type { Quantity, ScaledQuantity } from './quantityMath';
import { counted } from './roundCounted';
import { customary, measured } from './roundMeasured';
import { spooned } from './roundSpoons';
import { familyOf, scales, toCanonical } from './units';

export type { Quantity, ScaledQuantity };

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
 *
 * This file chooses the rule for an amount; the rules are `roundMeasured.ts`
 * (mass and volume), `roundSpoons.ts` and `roundCounted.ts`.
 */

/** Beyond these, times and tins stop being right and the app says so. */
export const trustedFactorRange = { lowest: 0.6, highest: 1.75 } as const;

/** A yield of zero is not a recipe, and a thousand portions is a typo. */
const yieldLimits = { lowest: 0.0001, highest: 1000 } as const;

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
