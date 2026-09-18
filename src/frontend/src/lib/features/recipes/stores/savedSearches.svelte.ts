import type { components } from '$api/generated/schema';
import { http, request, type AppError } from '$api';
import { registerStore } from '$shell/stores';

import { fromWireSort, toWireSort, type RecipeSort } from './libraryView.svelte';

/**
 * A search somebody wanted back.
 *
 * The same four things the toolbar holds, so applying one is assigning them
 * rather than translating anything.
 */
export interface SavedSearch {
  readonly id: string;
  readonly name: string;
  readonly query: string;
  readonly tags: readonly string[];
  readonly maxMinutes: number | null;
  readonly sort: RecipeSort | null;
}

/** What a saved search is made of, before it has a name or an id. */
export interface SearchCriteria {
  readonly query: string;
  readonly tags: readonly string[];
  readonly maxMinutes: number | null;
  readonly sort: RecipeSort | null;
}

/** Whether a set of filters says anything the server will accept as a search. */
export const worthSaving = (criteria: SearchCriteria): boolean =>
  criteria.query.trim().length > 0 ||
  criteria.tags.length > 0 ||
  criteria.maxMinutes !== null ||
  criteria.sort !== null;

/**
 * The searches this household has saved.
 *
 * Household-owned rather than kept in this browser, unlike the recent searches
 * the overlay will remember: a search saved on a laptop that does not exist on
 * the phone in the kitchen is a search saved in the wrong place.
 */
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

  /**
   * Reads the household's saved searches, once.
   *
   * Not cached by age, unlike the tag vocabulary: this list only changes when
   * somebody on this device changes it, and every one of those paths updates
   * the store itself.
   */
  async load(householdId: string): Promise<void> {
    if (this.#loadedFor === householdId) {
      return;
    }

    this.#loadedFor = householdId;

    const result = await request(() =>
      http.GET('/api/v1/searches', { params: { query: { householdId } } })
    );

    if (!result.ok) {
      // Asked again next time: a list that failed to load once should not stay
      // missing for the rest of the session.
      this.#loadedFor = null;
      this.#error = result.error;

      return;
    }

    this.#items = result.value.items.map(toSaved);
    this.#error = null;
  }

  /** Saves what the toolbar is currently showing, under a name. */
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

    // Appended rather than refetched, and at the end, because the server orders
    // these oldest first and this is the newest.
    this.#items = [...this.#items, saved];

    return saved;
  }

  /** Renames one, or points it at different filters. Both are the same gesture. */
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

  /** Forgets one, putting it back if the server disagrees. */
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

/** The wire shape, at the store boundary and nowhere else. */
type WireSavedSearch = components['schemas']['SearchesSavedSearchDetail'];

function toSaved(wire: WireSavedSearch): SavedSearch {
  return {
    id: wire.searchId,
    name: wire.name,
    query: wire.criteria.query ?? '',
    tags: wire.criteria.tags ?? [],
    maxMinutes: wire.criteria.maxMinutes ?? null,
    sort: fromWireSort(wire.criteria.sort)
  };
}

function toWire(criteria: SearchCriteria) {
  const words = criteria.query.trim();

  return {
    // Omitted rather than sent empty: the server reads "nothing was asked" from
    // absence, and an empty string would be a filter matching everything.
    query: words.length > 0 ? words : undefined,
    tags: [...criteria.tags],
    maxMinutes: criteria.maxMinutes ?? undefined,
    // `shelf` is dropped rather than stored: it is a cookbook's own order, and
    // a saved search applied from the library has no cookbook to be an order of.
    sort:
      criteria.sort === null || criteria.sort === 'shelf' ? undefined : toWireSort(criteria.sort)
  };
}

export const savedSearches = new SavedSearchStore();

registerStore(() => savedSearches.reset());
