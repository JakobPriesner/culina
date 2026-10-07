import { http, request } from '$api';

import { toFacets, toInterpretation, toSummary } from '../mappers';

import { toWireSort, type RecipeSort } from './libraryView.svelte';
import { presumedDiets } from './presumedDiets.svelte';

export interface RecipeFilters {
  readonly query?: string;
  readonly tags?: readonly string[];
  /** Ask what can be cooked from these. Ranked by fit, not filtered. */
  readonly ingredients?: readonly string[];
  readonly maxMinutes?: number;
  /**
   * Read inside one cookbook.
   *
   * A cookbook is a view of the collection rather than a second one, so it is
   * a filter here like any other — which is what lets the cookbook page render
   * the same grid, with the same search and the same paging, and own none of
   * it.
   */
  readonly cookbookId?: string;
  /**
   * How to order the page.
   *
   * The app's own words; `toWireSort` is the one place they meet the query
   * string's. 'suggested' ranks the whole collection for whoever is asking — a
   * sort over the one collection rather than a second collection, which is what
   * lets it compose with every filter above it.
   */
  readonly sort?: RecipeSort;
  /**
   * Search the words exactly as typed: the reader turned the server's
   * correction of them down.
   */
  readonly asTyped?: boolean;
}

const pageSize = 24;

/**
 * One page of the recipe list, in the app's own shapes.
 *
 * `cursor` is null for the first page. The answer is either the page or the
 * request's failure, so the list decides what each means for what is on screen.
 */
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
