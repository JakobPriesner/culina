import { clampYield } from '../scaling';
import type { RecipeReading } from '../types';

/** The chosen yield lives in the URL (scaling is a view): it survives reload and the step to cooking, and can be shared as "for six". */
const key = 'yield';

/** Reads it back, refusing anything that is not a usable number. */
export function yieldFrom(url: URL, recipe: RecipeReading | null): number {
  const raw = url.searchParams.get(key);
  const parsed = raw === null ? Number.NaN : Number(raw);

  if (!Number.isFinite(parsed) || parsed <= 0) {
    return recipe?.yieldAmount ?? 1;
  }

  return clampYield(parsed);
}

/** The same URL at a different yield; the recipe's own yield is omitted so a plain link stays plain. */
export function urlAtYield(url: URL, value: number, recipe: RecipeReading | null): string {
  const next = new URL(url);

  if (recipe && value === recipe.yieldAmount) {
    next.searchParams.delete(key);
  } else {
    next.searchParams.set(key, String(value));
  }

  return `${next.pathname}${next.search}`;
}
