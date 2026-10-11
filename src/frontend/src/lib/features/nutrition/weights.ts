import { formatNumber } from '$shell/i18n';

/** What the server accepts as the weight of one unit: more than nothing, and at most ten kilograms. */
export const maxGrams = 10_000;

/** The grams a person typed, with a comma or a point; null when it is not a weight the server would take. */
export function parseGrams(typed: string): number | null {
  const grams = Number(typed.trim().replace(',', '.'));

  return Number.isFinite(grams) && grams > 0 && grams <= maxGrams ? grams : null;
}

/** Grams as typed back into the field: tenths when there are some, no thousands separator to trip the parser. */
export const gramsField = (grams: number): string =>
  formatNumber(grams, { maximumFractionDigits: 1, useGrouping: false });

/** Grams to read: "110", "4,5", "1.200". */
export const gramsWords = (grams: number): string =>
  formatNumber(grams, { maximumFractionDigits: grams < 10 ? 1 : 0 });
