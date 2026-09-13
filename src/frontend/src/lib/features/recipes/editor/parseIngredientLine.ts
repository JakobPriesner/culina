import type { Quantity } from '../types';
import type { Unit } from '../units';

/**
 * Reads "200 g Mehl" into an amount, a unit and a name.
 *
 * One input per ingredient, not three. Three fields is three times the tabbing
 * and turns writing down a recipe into data entry — nobody writes a recipe that
 * way on paper either.
 *
 * The shortcut is only trustworthy because the result is shown back as separate
 * parts: a wrong read is visible, and one tap fixes it. A parser that guessed
 * silently would be worse than no parser.
 */
export interface ParsedIngredient {
  readonly quantity: Quantity;
  readonly name: string;
  /** The preparation after a comma: "fein gehackt". */
  readonly note: string | null;
}

/**
 * Folds a typed word to the form the table below is keyed by.
 *
 * German is written with umlauts and typed both ways: the same person writes
 * `Stück` at a keyboard and `Stueck` on a phone in a hurry. Folding here means
 * the table holds one spelling of each word instead of two, and `Esslöffel`
 * cannot be the one that was forgotten.
 */
const fold = (word: string): string =>
  word
    .toLowerCase()
    .replace(/\.$/, '')
    .replaceAll('ä', 'ae')
    .replaceAll('ö', 'oe')
    .replaceAll('ü', 'ue')
    .replaceAll('ß', 'ss');

/**
 * What people actually type, mapped to the wire codes.
 *
 * Both languages, because a German recipe says `EL` and an English one says
 * `tbsp`, and the same person writes both depending on where the recipe came
 * from. Plurals are listed rather than stripped: German plurals are not a
 * suffix rule, and a parser that guessed would read `Zitronen` as a unit.
 */
const spellings: Record<string, Unit> = {
  g: 'g',
  gr: 'g',
  gramm: 'g',
  gram: 'g',
  grams: 'g',
  gramme: 'g',
  kg: 'kg',
  kilo: 'kg',
  kilos: 'kg',
  kilogramm: 'kg',
  kilogram: 'kg',
  kilograms: 'kg',
  ml: 'ml',
  milliliter: 'ml',
  millilitre: 'ml',
  l: 'l',
  liter: 'l',
  litre: 'l',
  tsp: 'tsp',
  tsps: 'tsp',
  teaspoon: 'tsp',
  teaspoons: 'tsp',
  tl: 'tsp',
  teeloeffel: 'tsp',
  tbsp: 'tbsp',
  tbsps: 'tbsp',
  tbs: 'tbsp',
  tablespoon: 'tbsp',
  tablespoons: 'tbsp',
  el: 'tbsp',
  essloeffel: 'tbsp',
  stk: 'piece',
  stueck: 'piece',
  piece: 'piece',
  pieces: 'piece',
  zehe: 'clove',
  zehen: 'clove',
  clove: 'clove',
  cloves: 'clove',
  bund: 'bunch',
  bunches: 'bunch',
  bunch: 'bunch',
  scheibe: 'slice',
  scheiben: 'slice',
  slice: 'slice',
  slices: 'slice',
  dose: 'can',
  dosen: 'can',
  can: 'can',
  cans: 'can',
  packung: 'pack',
  packungen: 'pack',
  paeckchen: 'pack',
  pack: 'pack',
  packs: 'pack',
  packet: 'pack',
  packets: 'pack',
  prise: 'pinch',
  prisen: 'pinch',
  pinch: 'pinch',
  pinches: 'pinch'
};

/** `1/2`, a fraction glyph, `1,5` and `1.5` are all the same number to a person. */
const fractions: Record<string, number> = {
  '½': 0.5,
  '⅓': 1 / 3,
  '⅔': 2 / 3,
  '¼': 0.25,
  '¾': 0.75
};

const glyphs = Object.keys(fractions).join('');
const amountPattern = new RegExp(
  `^\\s*(\\d+\\s*/\\s*\\d+|[${glyphs}]|\\d+[.,]?\\d*\\s*[${glyphs}]?)\\s*`
);
const mixedPattern = new RegExp(`^(\\d+)\\s*([${glyphs}])$`);

/**
 * Reads a line, knowing the units this kitchen already uses.
 *
 * @param line What was typed.
 * @param own The household's own units, which the built-in spellings do not
 *   cover. Once somebody has written "1 Schuss Milch" once, every later line
 *   reads the same way without being told again — which is the whole of what it
 *   means for a household to have added a unit.
 */
export function parseIngredientLine(line: string, own: readonly string[] = []): ParsedIngredient {
  const trimmed = line.trim();

  // Everything after the comma is how it is prepared, not what it is — but a
  // comma between two digits is a German decimal point, and "1,5 kg Mehl" is
  // one and a half kilos, not one kilo prepared "5 kg Mehl".
  const comma = trimmed.search(/(?<!\d),|,(?!\d)/);
  const head = comma === -1 ? trimmed : trimmed.slice(0, comma).trim();
  const note = comma === -1 ? null : trimmed.slice(comma + 1).trim() || null;

  const amountMatch = amountPattern.exec(head);
  const value = amountMatch ? toNumber(amountMatch[1]!) : null;
  const afterAmount = amountMatch ? head.slice(amountMatch[0].length) : head;

  const [firstWord = '', ...restWords] = afterAmount.split(/\s+/).filter(Boolean);

  // A unit only counts when there is an amount for it to measure: "Salz" is an
  // ingredient, and a word starting a name is not a litre.
  const unitWord = fold(firstWord);
  const unit =
    value === null
      ? null
      : (spellings[unitWord] ?? own.find((candidate) => fold(candidate) === unitWord) ?? null);
  const name = (unit ? restWords.join(' ') : afterAmount).trim();

  return {
    quantity: { value, unit },
    // Falling back to the whole line means an entry it cannot read still saves
    // as an ingredient rather than vanishing.
    name: name || head,
    note
  };
}

function toNumber(raw: string): number | null {
  const text = raw.trim();

  if (text in fractions) {
    return fractions[text]!;
  }

  const slash = /^(\d+)\s*\/\s*(\d+)$/.exec(text);

  if (slash) {
    const denominator = Number(slash[2]);

    return denominator === 0 ? null : Number(slash[1]) / denominator;
  }

  const mixed = mixedPattern.exec(text);

  if (mixed) {
    return Number(mixed[1]) + fractions[mixed[2]!]!;
  }

  const parsed = Number(text.replace(',', '.'));

  return Number.isFinite(parsed) ? parsed : null;
}
