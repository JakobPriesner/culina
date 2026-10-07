import type { AppError } from '$api';
import type { LoadStatus } from '$shell/stores';

import type { Facets, Interpretation, Recipe, RecipeSummary } from '../types';

import { fetchRecipePage, type RecipeFilters } from './recipeQuery';

export type { RecipeFilters };

/**
 * The paged, filtered list of recipes, with the answers that describe it.
 *
 * Only reads; the open recipe and every write live in `recipes.svelte.ts`,
 * which reaches the rows here through the few methods at the bottom.
 */
export class RecipeList {
  #items = $state.raw<RecipeSummary[]>([]);
  #status = $state<LoadStatus>('idle');
  #error = $state<AppError | null>(null);
  #total = $state(0);
  #cursor = $state<string | null>(null);
  #interpretation = $state<Interpretation | null>(null);
  #facets = $state<Facets | null>(null);
  #loadingMore = $state(false);
  #moreFailed = $state(false);

  /**
   * Whose items these are. Plain rather than $state: it is read before the
   * first await of a method an effect calls, and a tracked read there would
   * make the method's own writes call it again.
   */
  #householdId: string | null = null;

  /**
   * Which read is allowed to write to the store.
   *
   * Typing into the search box starts a request per pause, and they do not come
   * back in the order they were sent — a short query matches more rows and
   * takes longer, so the *earlier* search regularly answers last. Without this,
   * a list settles on whatever the slowest request said, which is the wrong
   * answer and looks like the filter is broken.
   */
  #readToken = 0;

  /** The request a newer one has made pointless. */
  #reading: AbortController | null = null;

  get items(): readonly RecipeSummary[] {
    return this.#items;
  }

  get status(): LoadStatus {
    return this.#status;
  }

  get error(): AppError | null {
    return this.#error;
  }

  get total(): number {
    return this.#total;
  }

  /**
   * What the server read the last query to mean, or null without one.
   *
   * Kept with the list it describes, and replaced with it, so the chips on
   * screen are always the reading of the results on screen.
   */
  get interpretation(): Interpretation | null {
    return this.#interpretation;
  }

  /** Refinements that split the current results, or null. */
  get facets(): Facets | null {
    return this.#facets;
  }

  /** True while more pages exist. */
  get hasMore(): boolean {
    return this.#cursor !== null;
  }

  /**
   * True when the next page could not be fetched.
   *
   * A list that fetches by itself must stop by itself. Without this, a dead
   * connection is a loop: the end of the list stays in view, asks again,
   * fails again, and the browser spends the rest of the afternoon on it.
   */
  get moreFailed(): boolean {
    return this.#moreFailed;
  }

  /** Replaces the list. Used when the filters change. */
  async list(householdId: string, filters: RecipeFilters = {}): Promise<void> {
    const token = this.#beginRead();

    // Another household's recipes are not a list to keep on screen while this
    // one's arrive: for that moment they would be under the wrong name.
    if (this.#householdId !== householdId) {
      this.#householdId = householdId;
      this.#items = [];
      this.#total = 0;
      this.#cursor = null;
    }

    this.#status = 'loading';
    this.#error = null;
    this.#moreFailed = false;
    this.#loadingMore = false;

    const result = await fetchRecipePage(householdId, filters, null, this.#reading?.signal);

    if (token !== this.#readToken) {
      return;
    }

    if (!result.ok) {
      this.#error = result.error;
      this.#status = 'failed';

      return;
    }

    const page = result.value;

    this.#items = page.items;
    this.#cursor = page.nextCursor;
    this.#total = page.total;
    this.#interpretation = page.interpretation;
    this.#facets = page.facets;
    this.#status = 'ready';
  }

  /**
   * Appends the next page. The list already on screen is never disturbed.
   *
   * Safe to call while it is already running: the end of the list scrolls into
   * view once per placeholder row and the browser says so more than once, so
   * the second ask has to be free rather than a second request for the same
   * cursor.
   */
  async loadMore(householdId: string, filters: RecipeFilters = {}): Promise<void> {
    if (!this.#cursor || this.#loadingMore) {
      return;
    }

    this.#loadingMore = true;
    this.#moreFailed = false;

    const token = this.#beginRead();
    const result = await fetchRecipePage(householdId, filters, this.#cursor, this.#reading?.signal);

    this.#loadingMore = false;

    // A filter changed while the next page was in flight: those rows belong to
    // a list that is no longer on screen.
    if (token !== this.#readToken) {
      return;
    }

    if (!result.ok) {
      // The page that is already there stays. A failed page is a reason to
      // stop fetching and ask, not to empty the screen.
      this.#error = result.error;
      this.#moreFailed = true;

      return;
    }

    this.#items = [...this.#items, ...result.value.items];
    this.#cursor = result.value.nextCursor;
    this.#total = result.value.total;
  }

  /** Takes a row off the list and returns what puts it back exactly as it was. */
  withdraw(recipeId: string): () => void {
    const before = this.#items;

    this.#items = before.filter((item) => item.id !== recipeId);

    return () => {
      this.#items = before;
    };
  }

  /** The row is really gone, so the count of what matched is one fewer. */
  noteRemoved(): void {
    this.#total = Math.max(0, this.#total - 1);
  }

  fail(error: AppError): void {
    this.#error = error;
  }

  clearError(): void {
    this.#error = null;
  }

  /** Keeps the row in the list in step with the recipe that was just saved. */
  refreshSummary(recipe: Recipe): void {
    this.#items = this.#items.map((item) =>
      item.id === recipe.id
        ? {
            ...item,
            title: recipe.title,
            imageId: recipe.imageId,
            totalMinutes: recipe.totalMinutes,
            yieldAmount: recipe.yieldAmount,
            yieldKind: recipe.yieldKind,
            tags: recipe.tags,
            updatedAt: recipe.updatedAt
          }
        : item
    );
  }

  reset(): void {
    this.#householdId = null;
    this.#items = [];
    this.#status = 'idle';
    this.#error = null;
    this.#total = 0;
    this.#cursor = null;
    this.#interpretation = null;
    this.#facets = null;
    this.#loadingMore = false;
    this.#moreFailed = false;
    this.#reading?.abort();
    this.#reading = null;
    this.#readToken += 1;
  }

  /**
   * Claims the right to write the list, and gives up the previous request.
   *
   * Aborting is politeness — the token is what makes it correct.
   */
  #beginRead(): number {
    this.#reading?.abort();
    this.#reading = new AbortController();

    return ++this.#readToken;
  }
}
