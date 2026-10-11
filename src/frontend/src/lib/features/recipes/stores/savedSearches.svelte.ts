import type { components } from '$api/generated/schema';
import { http, request, type AppError } from '$api';
import { registerStore } from '$shell/stores';

import { fromWireSort, toWireSort, type RecipeSort } from './libraryView.svelte';

/** A saved search holds the toolbar's four things, so applying one is assigning them. */
export interface SavedSearch {
  readonly id: string;
  readonly name: string;
  readonly query: string;
  readonly tags: readonly string[];
  readonly maxMinutes: number | null;
  readonly maxKcal?: number | null;
  readonly sort: RecipeSort | null;
}

export interface SearchCriteria {
  readonly query: string;
  readonly tags: readonly string[];
  readonly maxMinutes: number | null;
  readonly maxKcal?: number | null;
  readonly sort: RecipeSort | null;
}

export const worthSaving = (criteria: SearchCriteria): boolean =>
  criteria.query.trim().length > 0 ||
  criteria.tags.length > 0 ||
  criteria.maxMinutes !== null ||
  criteria.maxKcal != null ||
  criteria.sort !== null;

/** Household-owned (unlike recent searches, which stay in the browser) so a search saved on a laptop exists on the kitchen phone. */
class SavedSearchStore {
  #items = $state<SavedSearch[]>([]);
  #error = $state<AppError | null>(null);
  #loadedFor: string | null = null;

  get items(): readonly SavedSearch[] {
    return this.#items;
  }

  get error(): AppError | null {
    return this.#error;
  }

  /** Reads the saved searches once; not age-cached, since every change to the list goes through this store. */
  async load(householdId: string): Promise<void> {
    if (this.#loadedFor === householdId) {
      return;
    }

    this.#loadedFor = householdId;

    const result = await request(() =>
      http.GET('/api/v1/searches', { params: { query: { householdId } } })
    );

    if (!result.ok) {
      // Retry next time instead of staying missing for the session.
      this.#loadedFor = null;
      this.#error = result.error;

      return;
    }

    this.#items = result.value.items.map(toSaved);
    this.#error = null;
  }

  async save(
    householdId: string,
    name: string,
    criteria: SearchCriteria
  ): Promise<SavedSearch | AppError> {
    const result = await request(() =>
      http.POST('/api/v1/searches', {
        body: { householdId, name, criteria: toWire(criteria) }
      })
    );

    if (!result.ok) {
      return result.error;
    }

    const saved = toSaved(result.value);

    // Appended: the server orders oldest first and this is the newest.
    this.#items = [...this.#items, saved];

    return saved;
  }

  async revise(
    searchId: string,
    name: string,
    criteria: SearchCriteria
  ): Promise<SavedSearch | AppError> {
    const before = this.#items;

    const result = await request(() =>
      http.PATCH('/api/v1/searches/{searchId}', {
        params: { path: { searchId } },
        body: { name, criteria: toWire(criteria) }
      })
    );

    if (!result.ok) {
      this.#items = before;
      this.#error = result.error;

      return result.error;
    }

    const saved = toSaved(result.value);

    this.#items = this.#items.map((one) => (one.id === searchId ? saved : one));

    return saved;
  }

  async forget(searchId: string): Promise<AppError | null> {
    const before = this.#items;

    this.#items = this.#items.filter((one) => one.id !== searchId);

    const result = await request(() =>
      http.DELETE('/api/v1/searches/{searchId}', { params: { path: { searchId } } })
    );

    if (result.ok) {
      return null;
    }

    this.#items = before;
    this.#error = result.error;

    return result.error;
  }

  clearError(): void {
    this.#error = null;
  }

  reset(): void {
    this.#items = [];
    this.#error = null;
    this.#loadedFor = null;
  }
}

/** Wire shape, used at the store boundary only. */
type WireSavedSearch = components['schemas']['SearchesSavedSearchDetail'];

function toSaved(wire: WireSavedSearch): SavedSearch {
  return {
    id: wire.searchId,
    name: wire.name,
    query: wire.criteria.query ?? '',
    tags: wire.criteria.tags ?? [],
    maxMinutes: wire.criteria.maxMinutes ?? null,
    maxKcal: wire.criteria.maxKcal ?? null,
    sort: fromWireSort(wire.criteria.sort)
  };
}

function toWire(criteria: SearchCriteria) {
  const words = criteria.query.trim();

  return {
    // Omitted rather than empty: the server reads absence as "nothing asked"; an empty string would match everything.
    query: words.length > 0 ? words : undefined,
    tags: [...criteria.tags],
    maxMinutes: criteria.maxMinutes ?? undefined,
    maxKcal: criteria.maxKcal ?? undefined,
    // `shelf` is dropped: it is a cookbook's own order, and the library has no cookbook.
    sort:
      criteria.sort === null || criteria.sort === 'shelf' ? undefined : toWireSort(criteria.sort)
  };
}

export const savedSearches = new SavedSearchStore();

registerStore(() => savedSearches.reset());
