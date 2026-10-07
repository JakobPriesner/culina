import { type MeasurementSystem } from './measurement';
import type { Quantity, ScaledQuantity } from './quantityMath';
import { counted } from './roundCounted';
import { customary, measured } from './roundMeasured';
import { spooned } from './roundSpoons';
import { familyOf, scales, toCanonical } from './units';

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

/** The yield at which this ingredient comes out at the amount on hand (e.g. leftover 600 g of flour). */
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

  // Not rounded to tidy servings (370 g would become 380 g); `yieldLabel` rounds for display.
  // Only the float tail is trimmed, so URLs don't carry `7.400000000000001`.
  return clampYield(Number((((to / from) * baseYield) as number).toFixed(6)));
}

/** Servings for display, rounded to halves; never used in arithmetic. */
export const yieldLabel = (value: number): number =>
  Number.isFinite(value) ? Math.round(value * 2) / 2 : 0;
