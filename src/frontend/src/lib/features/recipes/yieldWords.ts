import { m } from '$shell/i18n';

import type { YieldKind } from './types';

/** Enough of a recipe to word its yield: structural, since a card, the surface and the scaling sheet hold different types and must word it identically. */
export interface Yields {
  readonly yieldKind: YieldKind;
  /** The recipe's own word for it, when it has one. */
  readonly yieldLabel: string | null;
}

/** What a recipe makes, in words ("4 servings", "1 Cake"). A recipe's own word is used as typed for every amount, never pluralised (the app cannot know "Blech" → "Bleche"); otherwise the kind is worded in the reader's language. */
export function wordYield(amount: number, of: Yields): string {
  if (of.yieldLabel) {
    return m['recipes.meta.yieldLabel']({ count: amount, label: of.yieldLabel });
  }

  return of.yieldKind === 'pieces'
    ? m['recipes.meta.pieces']({ count: amount })
    : m['recipes.meta.servings']({ count: amount });
}

/** The same word without a number, for the stepper that prints the amount itself; how far it counts stays with the kind. */
export const yieldNoun = (of: Yields): string =>
  of.yieldLabel ??
  (of.yieldKind === 'pieces' ? m['recipe.pieces.label']() : m['recipe.servings.label']());
