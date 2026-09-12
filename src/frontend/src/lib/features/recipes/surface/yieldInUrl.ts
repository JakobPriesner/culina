import { clampYield } from '../scaling';
import type { Recipe } from '../types';

/**
 * The chosen yield lives in the URL.
 *
 * Scaling is a view, never a change to the recipe — so it belongs where a view
 * belongs. Putting it in the query string means a scaled recipe survives a
 * reload, survives the step from reading to cooking, and can be sent to
 * somebody as the thing you actually meant: "here, for six".
 */
const key = 'yield';

/** Reads it back, refusing anything that is not a usable number. */
export function yieldFrom(url: URL, recipe: Recipe | null): number {
  const raw = url.searchParams.get(key);
  const parsed = raw === null ? Number.NaN : Number(raw);

  if (!Number.isFinite(parsed) || parsed <= 0) {
    return recipe?.yieldAmount ?? 1;
  }

  return clampYield(parsed);
}

/**
 * The same URL at a different yield.
 *
 * The recipe's own yield is left out rather than written as `?yield=4`: a link
 * to a recipe should be the plain link unless somebody deliberately scaled it.
 */
export function urlAtYield(url: URL, value: number, recipe: Recipe | null): string {
  const next = new URL(url);

  if (recipe && value === recipe.yieldAmount) {
    next.searchParams.delete(key);
  } else {
    next.searchParams.set(key, String(value));
  }

  return `${next.pathname}${next.search}`;
}
