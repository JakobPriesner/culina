import type { Ingredient, Quantity } from './types';
import { canCombine, toCanonical } from './units';

/** One ingredient-list line: repeated ingredients (butter for the pan and for the béchamel) fold into one so the reader does no arithmetic. */
export interface IngredientLine {
  /** Every ingredient this line stands for; a list because mentioning one in a step must light the folded line. */
  readonly ids: readonly string[];
  readonly quantity: Quantity;
  readonly name: string;
  readonly note: string | null;
}

interface Draft {
  ids: string[];
  value: number | null;
  name: string;
  notes: string[];
}

/**
 * Adds up ingredients in recipe order: same name and addable units (`canCombine`) fold, and the first line's unit wins (1 kg + 500 g = 1.5 kg).
 * Amounts are added before scaling, so rounding does not accumulate (two 12.5 g lines rounded to 15 g each would be 30 g).
 */
export function combineIngredients(ingredients: readonly Ingredient[]): IngredientLine[] {
  const drafts: { draft: Draft; unit: Quantity['unit'] }[] = [];

  for (const one of ingredients) {
    const into = drafts.find(
      (line) =>
        folded(line.draft.name) === folded(one.name) && canCombine(line.unit, one.quantity.unit)
    );

    if (!into) {
      drafts.push({
        draft: {
          ids: [one.id],
          value: one.quantity.value,
          name: one.name,
          notes: one.note ? [one.note] : []
        },
        unit: one.quantity.unit
      });

      continue;
    }

    into.draft.ids.push(one.id);
    into.draft.value = added(into.draft.value, into.unit, one.quantity);

    // The preparation belongs to the amount it was written beside, so a folded line keeps both notes.
    if (one.note && !into.draft.notes.includes(one.note)) {
      into.draft.notes.push(one.note);
    }
  }

  return drafts.map(({ draft, unit }) => ({
    ids: draft.ids,
    quantity: { value: draft.value, unit },
    name: draft.name,
    note: draft.notes.join(', ') || null
  }));
}

const folded = (name: string): string => name.trim().toLocaleLowerCase();

/** Adds one amount to another in the line's unit; an amount-less ingredient ("salt") adds nothing. */
function added(value: number | null, unit: Quantity['unit'], quantity: Quantity): number | null {
  if (quantity.value === null) {
    return value;
  }

  const inLineUnit = (quantity.value * toCanonical(quantity.unit)) / toCanonical(unit);

  // Trimmed like scaling: 0.1 + 0.2 is not 0.3.
  return Number(((value ?? 0) + inLineUnit).toFixed(4));
}
