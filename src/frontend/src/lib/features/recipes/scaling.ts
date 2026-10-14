import { type MeasurementSystem } from './measurement';
import type { Quantity, ScaledQuantity } from './quantityMath';
import { counted } from './roundCounted';
import { customary, measured } from './roundMeasured';
import { spooned } from './roundSpoons';
import { canCombine, familyOf, scales, toCanonical, type Unit } from './units';

export type { Quantity, ScaledQuantity };

/**
 * Pure implementation of `docs/scaling-rules.md`; human rounding lives only here (the server stores unrounded).
 * Every amount is computed from the base amount and the factor, never from an already-scaled value, or it drifts.
 * This file picks the rule; `roundMeasured.ts`, `roundSpoons.ts` and `roundCounted.ts` apply it.
 */

/** Beyond these, times and tins stop being right and the app says so. */
export const trustedFactorRange = { lowest: 0.6, highest: 1.75 } as const;

/** A yield of zero is not a recipe, and a thousand portions is a typo. */
const yieldLimits = { lowest: 0.0001, highest: 1000 } as const;

/** The scale factor; invalid yields (zero base, `abc` from a URL) fall back to 1 instead of turning every amount into NaN. */
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
 * Scales one amount to what a cook would write down.
 * The measurement system is applied here to avoid double rounding; it affects mass and volume only.
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

  return rounded(base.value * factor, base.unit, converts);
}

/**
 * An amount the way a cook would write it, even when nothing scaled it: for a total that was summed from exact parts
 * (a shopping line), where `scaleQuantity` at a factor of one leaves the exact amount alone.
 */
export function roundQuantity(
  base: Quantity,
  system: MeasurementSystem = 'metric'
): ScaledQuantity {
  if (base.value === null || !scales(base.unit)) {
    return scaleQuantity(base, 1, system);
  }

  const family = familyOf(base.unit);

  return rounded(
    base.value,
    base.unit,
    system === 'imperial' && (family === 'mass' || family === 'volume')
  );
}

function rounded(exact: number, unit: Unit | null, converts: boolean): ScaledQuantity {
  const family = familyOf(unit);

  if (converts) {
    return customary(exact, unit!, family === 'mass');
  }

  switch (family) {
    case 'mass':
    case 'volume':
      return measured(exact, unit!);
    case 'spoon':
      return spooned(exact, unit!);
    default:
      return counted(exact, unit);
  }
}

/** The yield at which this ingredient comes out at the amount on hand (e.g. leftover 600 g of flour). */
export function targetYieldForAmount(
  base: Quantity,
  available: Quantity,
  baseYield: number
): number | null {
  if (!base.value || !available.value || !scales(base.unit)) {
    return null;
  }

  // Only mass and volume convert; a teaspoon is not a tablespoon, and a clove is not a piece.
  if (!canCombine(base.unit, available.unit)) {
    return null;
  }

  const from = base.value * toCanonical(base.unit);
  const to = available.value * toCanonical(available.unit);

  if (from <= 0) {
    return null;
  }

  // Not rounded to tidy servings (370 g would become 380 g); `yieldLabel` rounds for display.
  // Only the float tail is trimmed, so URLs don't carry `7.400000000000001`.
  return clampYield(Number((((to / from) * baseYield) as number).toFixed(6)));
}

/** Servings for display, rounded to halves; never used in arithmetic. */
export const yieldLabel = (value: number): number =>
  Number.isFinite(value) ? Math.round(value * 2) / 2 : 0;
