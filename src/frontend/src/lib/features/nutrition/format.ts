import { formatNumber, m } from '$shell/i18n';

import { roundForLabel, type Nutrient } from './rounding';
import type { NutritionValue } from './types';

/** Keeps a number with its unit, and "at least" with its number, on one line. */
export const nbsp = '\u00a0';

/** A number as a package prints it, in the reader's language: "520", "7,4", "0,05". */
export function labelNumber(nutrient: Nutrient, value: NutritionValue): string {
  const { value: rounded, decimals } = roundForLabel(nutrient, value.value, value.atLeast);

  return formatNumber(rounded, {
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals
  });
}

/** Written out, never a ≥ glyph: a screen reader says "mind. 520", and does not say "≥". */
export const withBound = (text: string, atLeast: boolean): string =>
  atLeast ? `${m['nutrition.atLeast']()}${nbsp}${text}` : text;

/** Grams on a line: tenths below 10 g, whole numbers above. A line is only ever an estimate of this, so no false precision. */
export const wholeOrTenth = (grams: number): string =>
  formatNumber(grams, { maximumFractionDigits: grams < 10 ? 1 : 0 });

/**
 * The one word before a figure that is not exact, the same on every screen: "mind." for a lower bound,
 * else "ca." for an estimate. A bound wins: "mind. ca." says less than it costs, and the estimate is
 * explained beside the figure. Words, not ≥ and ≈ glyphs, so they read the same to eye and screen reader.
 */
export function qualifierOf(value: Pick<NutritionValue, 'atLeast' | 'estimated'>): string | null {
  if (value.atLeast) {
    return m['nutrition.atLeast']();
  }

  return value.estimated ? m['nutrition.about']() : null;
}

/** A figure with its qualifier: "mind. 184 kcal", "ca. 520 kcal", "760 kcal". */
export function qualified(
  text: string,
  value: Pick<NutritionValue, 'atLeast' | 'estimated'>
): string {
  const word = qualifierOf(value);

  return word ? `${word}${nbsp}${text}` : text;
}
