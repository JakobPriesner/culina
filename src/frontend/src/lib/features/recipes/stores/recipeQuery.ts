import { http, request } from '$api';

import { toFacets, toInterpretation, toSummary } from '../mappers';

import { toWireSort, type RecipeSort } from './libraryView.svelte';
import { presumedDiets } from './presumedDiets.svelte';

export interface RecipeFilters {
  readonly query?: string;
  readonly tags?: readonly string[];
  /** Ranked by fit, not filtered. */
  readonly ingredients?: readonly string[];
  readonly maxMinutes?: number;
  /** A cookbook is a filter like any other, so its page reuses the grid, search and paging. */
  readonly cookbookId?: string;
  /** `toWireSort` maps these to the query string; 'suggested' ranks the whole collection, so it composes with every filter. */
  readonly sort?: RecipeSort;
  /** Search the words as typed: the reader declined the server's correction. */
  readonly asTyped?: boolean;
}

const pageSize = 24;

/** One page of the recipe list; `cursor` is null for the first. Returns the page or the request's failure. */
export async function fetchRecipePage(
  householdId: string,
  filters: RecipeFilters,
  cursor: string | null,
  signal?: AbortSignal
) {
  const result = await request(() =>
    http.GET('/api/v1/recipes', {
      signal,
      params: {
        query: {
          householdId,
          query: filters.query || undefined,
          tag: filters.tags?.length ? [...filters.tags] : undefined,
          ingredient: filters.ingredients?.length ? [...filters.ingredients] : undefined,
          maxMinutes: filters.maxMinutes,
          cookbookId: filters.cookbookId,
          sort: filters.sort ? toWireSort(filters.sort) : undefined,
          cursor: cursor ?? undefined,
          limit: pageSize,
          asTyped: filters.asTyped ? 'true' : undefined
        }
      }
    })
  );

  if (!result.ok) {
    return result;
  }

  const items = result.value.items.map(toSummary);

  presumedDiets.note(items);

  return {
    ok: true as const,
    value: {
      items,
      nextCursor: result.value.nextCursor ?? null,
      total: result.value.total,
      interpretation: result.value.interpretation
        ? toInterpretation(result.value.interpretation)
        : null,
      facets: result.value.facets ? toFacets(result.value.facets) : null
    }
  };
}
