import { m } from '$shell/i18n';

import type { YieldKind } from './types';

/**
 * Enough of a recipe to say what it makes.
 *
 * Structural rather than one of the recipe types, because a card has a summary,
 * the surface has a whole recipe and the scaling sheet has both — and all three
 * have to word a yield identically or the same dish reads as two dishes.
 */
export interface Yields {
  readonly yieldKind: YieldKind;
  /** The recipe's own word for it, when it has one. */
  readonly yieldLabel: string | null;
}

/**
 * What a recipe makes, in words: "4 servings", "12 pieces", "1 Cake".
 *
 * A recipe that gave itself a word is taken at that word, exactly as typed and
 * for every amount. Nothing pluralises it: it is one person's noun in one
 * person's language, and this app cannot know whether the plural of "Blech" is
 * "Bleche" — but it can know that inventing one is worse than not.
 *
 * Without a word, the kind is worded in whichever language the reader has
 * chosen, which is what nearly every recipe wants.
 */
export function wordYield(amount: number, of: Yields): string {
  if (of.yieldLabel) {
    return m['recipes.meta.yieldLabel']({ count: amount, label: of.yieldLabel });
  }

  return of.yieldKind === 'pieces'
    ? m['recipes.meta.pieces']({ count: amount })
    : m['recipes.meta.servings']({ count: amount });
}

/**
 * The same word without a number, for the control that sets the number.
 *
 * The stepper prints the amount itself, so it needs the noun alone — and the
 * noun is the only thing a label replaces. How far the stepper counts stays
 * with the kind, because "a cake" says nothing about what one more of it is.
 */
export const yieldNoun = (of: Yields): string =>
  of.yieldLabel ??
  (of.yieldKind === 'pieces' ? m['recipe.pieces.label']() : m['recipe.servings.label']());
