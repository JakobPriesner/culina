import type { ScaledQuantity } from './scaling';
import { familyOf, type Unit } from './units';

/**
 * Turns a scaled amount into the text a recipe would print.
 *
 * Separate from the scaling itself because it is the only part that knows about
 * language: `1,5 kg` in German and `1.5 kg` in English are the same quantity.
 */
export interface QuantityText {
  /** The number, already localised. Empty when the recipe gives no amount. */
  readonly amount: string;
  /** The unit's short form, or empty for a bare count. */
  readonly unit: string;
  /** Amount and unit joined, non-breaking, ready to render. */
  readonly text: string;
}

/**
 * Fractions as glyphs, where the amount makes them natural.
 *
 * Spoons are measured in halves and thirds because the spoons exist: `0.5 tsp`
 * is a number, `½ tsp` is the thing in the drawer. Part of one countable thing
 * is the same: nobody writes `0.5 onion`, they write half an onion.
 */
const glyphs = new Map<number, string>([
  [0.25, '¼'],
  [1 / 3, '⅓'],
  [0.5, '½'],
  [2 / 3, '⅔'],
  [0.75, '¾']
]);

/** The short form shown beside a number, or empty where the ingredient names it. */
const short: Record<Unit, string> = {
  g: 'g',
  kg: 'kg',
  ml: 'ml',
  l: 'l',
  // Spoons are absent, not empty-by-accident: their abbreviation is a word in
  // the reader's language (EL, not tbsp), so it comes from the labels.
  tsp: '',
  tbsp: '',
  // Count units are named by the ingredient itself — "3 cloves garlic" reads
  // worse than "3 garlic cloves", so the recipe's own words carry it.
  piece: '',
  clove: '',
  bunch: '',
  slice: '',
  can: '',
  pack: '',
  pinch: ''
};

export interface QuantityLabels {
  /** The word for a unit that has no short form, supplied by the caller. */
  readonly unitName: (unit: Unit, count: number) => string;
  /** Marks an amount that rounding moved: "~". */
  readonly approximately: (amount: string) => string;
}

/**
 * A non-breaking space, so `250` never wraps away from `g`.
 *
 * Escaped rather than typed literally: an invisible character in source is a
 * character nobody can see in a diff.
 */
const nbsp = '\u00a0';

export function formatQuantity(
  quantity: ScaledQuantity,
  locale: string,
  labels: QuantityLabels
): QuantityText {
  if (quantity.value === null) {
    return { amount: '', unit: unitTextFor(quantity, 0, labels), text: '' };
  }

  const amount = quantity.isRange
    ? // An en dash with no spaces, the way a range is written: 4–5.
      `${number(quantity.value, locale, quantity.unit)}–${number(quantity.upper ?? quantity.value, locale, quantity.unit)}`
    : number(quantity.value, locale, quantity.unit);

  const marked = quantity.isApproximate ? labels.approximately(amount) : amount;
  const unit = unitTextFor(quantity, quantity.upper ?? quantity.value, labels);

  return { amount: marked, unit, text: unit ? `${marked}${nbsp}${unit}` : marked };
}

function unitTextFor(quantity: ScaledQuantity, count: number, labels: QuantityLabels): string {
  if (!quantity.unit) {
    return '';
  }

  return short[quantity.unit] || labels.unitName(quantity.unit, count);
}

/**
 * Whether this is a piece of a single countable thing.
 *
 * `½ onion`, yes. `2½ onions` is not something a recipe says — that is where a
 * range belongs, and scaling produces one.
 */
const isPartOfOne = (whole: number, unit: Unit | null): boolean =>
  whole === 0 && familyOf(unit) === 'count';

function number(value: number, locale: string, unit: Unit | null): string {
  const whole = Math.floor(value);
  const fraction = Number((value - whole).toFixed(4));

  // Only where the amount is spoken as a fraction in the first place. `½ g` is
  // not a thing anyone writes — a scale shows 0.5 g — but half an onion is.
  if (fraction > 0 && (familyOf(unit) === 'spoon' || isPartOfOne(whole, unit))) {
    const glyph = [...glyphs].find(([size]) => Math.abs(size - fraction) < 0.005)?.[1];

    if (glyph) {
      return whole > 0 ? `${decimal(whole, locale)}${glyph}` : glyph;
    }
  }

  return decimal(value, locale);
}

/** Trimmed and localised: `1.0` is `1`, and `1.5` is `1,5` in German. */
const decimal = (value: number, locale: string): string =>
  new Intl.NumberFormat(locale, { maximumFractionDigits: 2 }).format(value);
