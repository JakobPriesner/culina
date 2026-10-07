import type { Quantity } from '../types';
import { foldUnit, spelledUnit } from '../unitSpellings';

/**
 * Reads "200 g Mehl" into amount, unit and name, for lines that arrive already written (pasted text, websites, the shopping list).
 * Guessing is safe only because results are shown back as separate, correctable parts.
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

/** Reads a line; `own` is the household's own units, so one written once reads the same way afterwards. */
export function parseIngredientLine(line: string, own: readonly string[] = []): ParsedIngredient {
  const trimmed = line.trim();

  // After a comma is preparation, but a comma between digits is a German decimal point ("1,5 kg Mehl").
  const comma = trimmed.search(/(?<!\d),|,(?!\d)/);
  const head = comma === -1 ? trimmed : trimmed.slice(0, comma).trim();
  const note = comma === -1 ? null : trimmed.slice(comma + 1).trim() || null;

  const amountMatch = amountPattern.exec(head);
  const value = amountMatch ? toNumber(amountMatch[1]!) : null;
  const afterAmount = amountMatch ? head.slice(amountMatch[0].length) : head;

  const [firstWord = '', ...restWords] = afterAmount.split(/\s+/).filter(Boolean);

  // A unit needs an amount to measure: "Salz" is an ingredient, not a litre.
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
    // Fall back to the whole line so an unreadable entry still saves.
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
