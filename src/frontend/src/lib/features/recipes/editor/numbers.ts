/**
 * The numbers of the editor as they are being typed, before they are numbers.
 *
 * "", "1," and "0." are all things a half-typed amount looks like and none of
 * them survive a trip through `Number`. The old field read
 * `Number(value) || 1`, so clearing it to type "12" put a 1 back under the
 * cursor. Here the typed text is what the field shows until it parses, and
 * the recipe is only written to when it does.
 */
export type TypedField = 'yieldAmount' | 'prepMinutes' | 'cookMinutes';

export type TypedNumbers = Record<TypedField, string | undefined>;

export type MinutesField = 'prepMinutes' | 'cookMinutes';

/** Nothing typed yet: every field shows what the recipe says. */
export const nothingTyped = (): TypedNumbers => ({
  yieldAmount: undefined,
  prepMinutes: undefined,
  cookMinutes: undefined
});

/** A number somebody typed, in either of the two ways Europe writes one. */
export const numberIn = (text: string): number | null => {
  const value = Number(text.replace(',', '.'));

  return text.trim() && Number.isFinite(value) ? value : null;
};

/**
 * How much it makes, which is the one number a recipe cannot do without.
 *
 * Every amount on the reading surface is derived from it, so a zero or a word
 * is refused rather than quietly turned into a 1.
 */
export const yieldFrom = (text: string): number | null => {
  const value = numberIn(text);

  return value !== null && value > 0 ? value : null;
};

export const yieldWrong = (text: string): boolean => yieldFrom(text) === null;

/**
 * A time in minutes, which a recipe is allowed not to say: blank clears it
 * (null), a number is rounded, and anything else is undefined — not a time yet.
 */
export const minutesFrom = (text: string): number | null | undefined => {
  if (!text.trim()) {
    return null;
  }

  const value = numberIn(text);

  return value !== null && value >= 0 ? Math.round(value) : undefined;
};

export const minutesWrong = (text: string): boolean =>
  Boolean(text.trim()) && minutesFrom(text) === undefined;

/** What a field shows: what was typed into it, else what the recipe says. */
export const yieldShown = (typed: TypedNumbers, saved: number): string =>
  typed.yieldAmount ?? String(saved);

export const minutesShown = (
  typed: TypedNumbers,
  which: MinutesField,
  saved: number | null
): string => typed[which] ?? (saved === null ? '' : String(saved));

/**
 * What the two times add up to.
 *
 * It is the number the library and the recipe's own header show, so seeing it
 * form while the two halves are typed is the difference between filling in
 * two fields and setting how long the recipe takes.
 */
export const totalMinutes = (prep: number | null, cook: number | null): number | null => {
  const total = (prep ?? 0) + (cook ?? 0);

  return total > 0 ? total : null;
};
