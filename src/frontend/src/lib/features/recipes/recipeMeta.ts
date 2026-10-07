import { m } from '$shell/i18n';

import type { RecipeSummary } from './types';
import { wordYield, type Yields } from './yieldWords';

/**
 * What the line of facts is made of: a summary has all of it, the surface a
 * whole recipe has everything but how often it was cooked, which is not a fact
 * about the recipe being read.
 */
export interface MetaFacts extends Yields {
  readonly totalMinutes: number | null;
  readonly yieldAmount: number;
  readonly cookCount?: number;
}

/**
 * The one line of facts under a recipe's title.
 *
 * Built here rather than in the card so the list, the surface and a search
 * result all describe a recipe the same way — and so the order of the facts is
 * decided once. Time first, because it is what decides whether tonight is the
 * night.
 */
export function metaLineFor(recipe: MetaFacts): string {
  const parts: string[] = [];

  if (recipe.totalMinutes !== null) {
    parts.push(m['recipes.meta.minutes']({ count: recipe.totalMinutes }));
  }

  parts.push(wordYield(recipe.yieldAmount, recipe));

  // Only once it has actually been cooked. "Made 0×" is noise on every recipe
  // nobody has got to yet.
  if (recipe.cookCount) {
    parts.push(m['recipes.meta.cooked']({ count: recipe.cookCount }));
  }

  return parts.join(m['recipes.meta.separator']());
}

/**
 * How well a recipe fits what someone said they have, in words.
 *
 * Null when they did not ask — the whole point of the ingredient search is that
 * there is no pantry to maintain, so a recipe says nothing about matching
 * unless a match was requested.
 */
export function matchLineFor(recipe: RecipeSummary): string | null {
  if (!recipe.match) {
    return null;
  }

  if (recipe.match.missing === 0) {
    return m['recipes.match.complete']();
  }

  return m['recipes.match.missing']({ count: recipe.match.missing });
}

/**
 * Where a recipe comes from, when it is inherited: the name of the household
 * it belongs to, if that is one the household on screen inherits from, and
 * null for its own recipes and for a list that does not say.
 */
export const inheritedFrom = (
  recipe: Pick<RecipeSummary, 'householdId'>,
  inherited: Readonly<Record<string, string>>
): string | null => (recipe.householdId ? (inherited[recipe.householdId] ?? null) : null);
