import { m } from '$shell/i18n';

import type { RecipeSummary } from './types';
import { wordYield, type Yields } from './yieldWords';

export interface MetaFacts extends Yields {
  readonly totalMinutes: number | null;
  readonly yieldAmount: number;
  readonly cookCount?: number;
}

/** The facts line under a title, shared by list, surface and search so the order stays consistent (time first). The list card leaves the yield out: its calorie figure says what it is per. */
export function metaLineFor(
  recipe: MetaFacts,
  { yields = true }: { yields?: boolean } = {}
): string {
  const parts: string[] = [];

  if (recipe.totalMinutes !== null) {
    parts.push(m['recipes.meta.minutes']({ count: recipe.totalMinutes }));
  }

  if (yields) {
    parts.push(wordYield(recipe.yieldAmount, recipe));
  }

  // Hidden at zero: "Made 0×" is noise.
  if (recipe.cookCount) {
    parts.push(m['recipes.meta.cooked']({ count: recipe.cookCount }));
  }

  return parts.join(m['recipes.meta.separator']());
}

/** Null unless an ingredient match was requested (there is no pantry to maintain). */
export function matchLineFor(recipe: RecipeSummary): string | null {
  if (!recipe.match) {
    return null;
  }

  if (recipe.match.missing === 0) {
    return m['recipes.match.complete']();
  }

  return m['recipes.match.missing']({ count: recipe.match.missing });
}

/** Owning household's name for an inherited recipe; null for its own recipes. */
export const inheritedFrom = (
  recipe: Pick<RecipeSummary, 'householdId'>,
  inherited: Readonly<Record<string, string>>
): string | null => (recipe.householdId ? (inherited[recipe.householdId] ?? null) : null);
