import type { Ingredient, Quantity } from './types';
import { canCombine, toCanonical } from './units';

/**
 * One line of the ingredient list.
 *
 * Not the same thing as an ingredient: a recipe that calls for butter twice —
 * once for the pan and once for the béchamel — is a recipe that needs one block
 * of butter, and a list that says so twice makes the reader do the arithmetic
 * that the app exists to do for them.
 */
export interface IngredientLine {
  /**
   * Every ingredient this line stands for.
   *
   * More than one once amounts have been added together, and the reason this is
   * a list rather than an id: pointing at "butter" in a step still has to light
   * the line it was folded into.
   */
  readonly ids: readonly string[];
  readonly quantity: Quantity;
  readonly name: string;
  readonly note: string | null;
}

/** The mutable half of the same thing, while the list is being built. */
interface Draft {
  ids: string[];
  value: number | null;
  name: string;
  notes: string[];
}

/**
 * The same thing, added up, in the order the recipe wrote it.
 *
 * Two lines are the same thing when they name the same thing in units that can
 * be added at all — `canCombine` is what decides that, so 200 g and 0.5 kg meet
 * and 100 g and a teaspoon do not. The first line's unit wins, because it is
 * the one the recipe chose: 1 kg and 500 g come out as 1.5 kg, not 1500 g.
 *
 * Amounts are added *before* anything is scaled. Adding scaled amounts adds
 * their rounding too, and two 12.5 g lines rounded to 15 g each are 30 g of
 * butter the recipe never asked for.
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

    // The preparation belongs to the amount it was written beside, so a folded
    // line keeps both: "300 g onion, finely diced, in rings" is clumsy and
    // true, and dropping either half would be neither.
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

/** Names match on what they say, not on how they were typed. */
const folded = (name: string): string => name.trim().toLocaleLowerCase();

/**
 * One amount added to another, expressed in the unit already on the line.
 *
 * An ingredient with no amount — "salt", "pepper to taste" — adds nothing and
 * takes nothing away: the line it joins keeps whatever it had, including
 * nothing.
 */
function added(value: number | null, unit: Quantity['unit'], quantity: Quantity): number | null {
  if (quantity.value === null) {
    return value;
  }

  const inLineUnit = (quantity.value * toCanonical(quantity.unit)) / toCanonical(unit);

  // Trimmed for the same reason scaling trims: 0.1 + 0.2 is not 0.3, and the
  // tail is far below anything an amount can show.
  return Number(((value ?? 0) + inLineUnit).toFixed(4));
}
