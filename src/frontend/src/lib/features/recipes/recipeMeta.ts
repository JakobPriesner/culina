import { m } from '$shell/i18n';

import type { RecipeSummary } from './types';

/**
 * The one line of facts under a recipe's title.
 *
 * Built here rather than in the card so the list, the surface and a search
 * result all describe a recipe the same way — and so the order of the facts is
 * decided once. Time first, because it is what decides whether tonight is the
 * night.
 */
export function metaLineFor(recipe: RecipeSummary): string {
  const parts: string[] = [];

  if (recipe.totalMinutes !== null) {
    parts.push(m['recipes.meta.minutes']({ count: recipe.totalMinutes }));
  }

  parts.push(
    recipe.yieldKind === 'pieces'
      ? m['recipes.meta.pieces']({ count: recipe.yieldAmount })
      : m['recipes.meta.servings']({ count: recipe.yieldAmount })
  );

  // Only once it has actually been cooked. "Made 0×" is noise on every recipe
  // nobody has got to yet.
  if (recipe.cookCount > 0) {
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
