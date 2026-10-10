import { http, request, type AppError } from '$api';

import type { FoodHit } from '../types';

export type FoodSearch = { ok: true; foods: FoodHit[] } | { ok: false; error: AppError };

/** The table's foods that fit what was typed, best first; reference data, not household data. */
export async function searchFoods(query: string, limit = 20): Promise<FoodSearch> {
  const result = await request(() =>
    http.GET('/api/v1/foods', { params: { query: { q: query, limit } } })
  );

  return result.ok
    ? { ok: true, foods: result.value.items.map((one) => ({ ...one })) }
    : { ok: false, error: result.error };
}
