import type { ScaledQuantity } from './scaling';
import { isCustomary, type CustomaryUnit } from './measurement';
import { familyOf, isBuiltIn, type BuiltInUnit, type Unit } from './units';

/** A scaled amount as printed text; separate from scaling because only this knows language (`1,5 kg` vs `1.5 kg`). */
export interface QuantityText {
  /** The number, already localised. Empty when the recipe gives no amount. */
  readonly amount: string;
  /** The unit's short form, or empty for a bare count. */
  readonly unit: string;
  /** Amount and unit joined, non-breaking, ready to render. */
  readonly text: string;
}

/** Fractions as glyphs where natural: `½ tsp` is the thing in the drawer, and nobody writes `0.5 onion`. */
const glyphs = new Map<number, string>([
  [0.125, '⅛'],
  [0.25, '¼'],
  [1 / 3, '⅓'],
  [0.5, '½'],
  [2 / 3, '⅔'],
  [0.75, '¾']
]);

/** The short form shown beside a number, or empty where the ingredient names it. */
const short: Record<BuiltInUnit, string> = {
  g: 'g',
  kg: 'kg',
  ml: 'ml',
  l: 'l',
  // Spoons are empty: their abbreviation is a word in the reader's language (EL, not tbsp), from the labels.
  tsp: '',
  tbsp: '',
  // Count units are named by the ingredient ("3 garlic cloves" beats "3 cloves garlic").
  piece: '',
  clove: '',
  bunch: '',
  slice: '',
  can: '',
  pack: '',
  pinch: ''
};

/** Same for imperial units: oz and lb are abbreviations everywhere; a cup is a word, pluralised by the labels. */
const customaryShort: Record<CustomaryUnit, string> = {
  oz: 'oz',
  lb: 'lb',
  'fl oz': 'fl oz',
  cup: ''
};

/** A household's own unit is its own label ("1 Schuss Milch"): nothing to translate or abbreviate. */
const shortOf = (unit: Unit): string => {
  if (isBuiltIn(unit)) {
    return short[unit];
  }

  return isCustomary(unit) ? customaryShort[unit] : unit;
};

export interface QuantityLabels {
  /** The word for a unit that has no short form, supplied by the caller. */
  readonly unitName: (unit: Unit, count: number) => string;
  /** Marks an amount that rounding moved: "~". */
  readonly approximately: (amount: string) => string;
}

/** Non-breaking, so `250` never wraps from `g`; escaped because an invisible character is unreadable in a diff. */
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

  return shortOf(quantity.unit) || labels.unitName(quantity.unit, count);
}

/** A piece of one countable thing (`½ onion`); `2½ onions` is a range, which scaling produces. */
const isPartOfOne = (whole: number, unit: Unit | null): boolean =>
  whole === 0 && familyOf(unit) === 'count';

function number(value: number, locale: string, unit: Unit | null): string {
  const whole = Math.floor(value);
  const fraction = Number((value - whole).toFixed(4));

  // Only where the amount is spoken as a fraction: `½ g` never, half an onion yes;
  // customary units take a whole part too ("1½ cups").
  if (
    fraction > 0 &&
    (familyOf(unit) === 'spoon' || isCustomary(unit) || isPartOfOne(whole, unit))
  ) {
    const glyph = [...glyphs].find(([size]) => Math.abs(size - fraction) < 0.005)?.[1];

    if (glyph) {
      return whole > 0 ? `${decimal(whole, locale)}${glyph}` : glyph;
    }
  }

  return decimal(value, locale);
}

/** Trimmed and localised: `1.0` is `1`, and `1.5` is `1,5` in German. */
const decimal = (value: number, locale: string): string => decimalFormat(locale).format(value);

/** Made once per locale: a scaled recipe formats an amount per ingredient. */
const decimalFormats = new Map<string, Intl.NumberFormat>();

function decimalFormat(locale: string): Intl.NumberFormat {
  let format = decimalFormats.get(locale);

  if (!format) {
    format = new Intl.NumberFormat(locale, { maximumFractionDigits: 2 });
    decimalFormats.set(locale, format);
  }

  return format;
}
