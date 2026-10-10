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
