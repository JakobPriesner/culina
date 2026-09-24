import type { Quantity } from '../types';
import { foldUnit, spelledUnit } from '../unitSpellings';

/**
 * Reads "200 g Mehl" into an amount, a unit and a name.
 *
 * For lines that arrive already written, rather than for the editor: a recipe
 * pasted in as text, a website's ingredient list, a line typed into the
 * shopping list. Somebody writing a recipe here fills the three fields in
 * themselves, and nothing has to be guessed.
 *
 * Guessing is only ever safe because the result is shown back as separate parts
 * before it is kept — the paste import shows what it understood, and every part
 * of it can be corrected. A parser that guessed silently would be worse than no
 * parser.
 */
export interface ParsedIngredient {
  readonly quantity: Quantity;
  readonly name: string;
  /** The preparation after a comma: "fein gehackt". */
  readonly note: string | null;
}

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
  const unitWord = foldUnit(firstWord);
  const unit =
    value === null
      ? null
      : (spelledUnit(firstWord) ??
        own.find((candidate) => foldUnit(candidate) === unitWord) ??
        null);
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
