/**
 * Editor numbers while typed: "1," or "0." do not survive `Number`, so a field shows its text
 * until it parses and the recipe is written only then.
 */
export type TypedField = 'yieldAmount' | 'prepMinutes' | 'cookMinutes';

export type TypedNumbers = Record<TypedField, string | undefined>;

export type MinutesField = 'prepMinutes' | 'cookMinutes';

export const nothingTyped = (): TypedNumbers => ({
  yieldAmount: undefined,
  prepMinutes: undefined,
  cookMinutes: undefined
});

/** Accepts a comma or a dot as the decimal separator. */
export const numberIn = (text: string): number | null => {
  const value = Number(text.replace(',', '.'));

  return text.trim() && Number.isFinite(value) ? value : null;
};

/**
 * Yield must be positive: every amount derives from it, so zero or text is refused, not turned into
 * 1.
 */
export const yieldFrom = (text: string): number | null => {
  const value = numberIn(text);

  return value !== null && value > 0 ? value : null;
};

export const yieldWrong = (text: string): boolean => yieldFrom(text) === null;

/** Blank clears (null), a number is rounded, anything else is undefined: not a time yet. */
export const minutesFrom = (text: string): number | null | undefined => {
  if (!text.trim()) {
    return null;
  }

  const value = numberIn(text);

  return value !== null && value >= 0 ? Math.round(value) : undefined;
};

export const minutesWrong = (text: string): boolean =>
  Boolean(text.trim()) && minutesFrom(text) === undefined;

export const yieldShown = (typed: TypedNumbers, saved: number): string =>
  typed.yieldAmount ?? String(saved);

export const minutesShown = (
  typed: TypedNumbers,
  which: MinutesField,
  saved: number | null
): string => typed[which] ?? (saved === null ? '' : String(saved));

/** Shown live while typing, so filling two fields reads as setting how long the recipe takes. */
export const totalMinutes = (prep: number | null, cook: number | null): number | null => {
  const total = (prep ?? 0) + (cook ?? 0);

  return total > 0 ? total : null;
};
