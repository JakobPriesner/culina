import type { AppError } from '$api';
import type { LoadStatus } from '$shell/stores';

import type { Facets, Interpretation, Recipe, RecipeSummary } from '../types';

import { fetchRecipePage, type RecipeFilters } from './recipeQuery';

export type { RecipeFilters };

/** The paged, filtered recipe list; reads only (writes live in `recipes.svelte.ts`). */
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

  /** Whose rows these are: household and cookbook. Plain, not $state: read before the first await of effect-called methods, where a tracked read would re-run on their own writes. */
  #context: string | null = null;

  /** Which read may write: responses arrive out of order (a short query answers last), so only the newest is applied. */
  #readToken = 0;

  #reading: AbortController | null = null;

  /** Bumped whenever the list is replaced rather than appended to, so an undo knows it is stale. */
  #listing = 0;

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

  /** The server's reading of the last query, replaced together with the list it describes. */
  get interpretation(): Interpretation | null {
    return this.#interpretation;
  }

  get facets(): Facets | null {
    return this.#facets;
  }

  get hasMore(): boolean {
    return this.#cursor !== null;
  }

  /** The next page failed; stops the end-of-list sentinel from retrying in a loop. */
  get moreFailed(): boolean {
    return this.#moreFailed;
  }

  /** Replaces the list when filters change. */
  async list(householdId: string, filters: RecipeFilters = {}): Promise<void> {
    const token = this.#beginRead();

    // Rows of the same household and cookbook stay on screen while they refresh; another context's never do.
    const context = `${householdId}|${filters.cookbookId ?? ''}`;

    if (this.#context !== context) {
      this.#context = context;
      this.#listing += 1;
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

    this.#listing += 1;
    this.#items = page.items;
    this.#cursor = page.nextCursor;
    this.#total = page.total;
    this.#interpretation = page.interpretation;
    this.#facets = page.facets;
    this.#status = 'ready';
  }

  /** Appends the next page; a repeat call while running is a no-op (the sentinel fires once per placeholder row). */
  async loadMore(householdId: string, filters: RecipeFilters = {}): Promise<void> {
    if (!this.#cursor || this.#loadingMore) {
      return;
    }

    this.#loadingMore = true;
    this.#moreFailed = false;

    const token = this.#beginRead();
    const result = await fetchRecipePage(householdId, filters, this.#cursor, this.#reading?.signal);

    this.#loadingMore = false;

    // Filters changed mid-flight: these rows belong to a stale list.
    if (token !== this.#readToken) {
      return;
    }

    if (!result.ok) {
      // Keep the page already shown; a failed page stops fetching but must not empty the list.
      this.#error = result.error;
      this.#moreFailed = true;

      return;
    }

    this.#items = [...this.#items, ...result.value.items];
    this.#cursor = result.value.nextCursor;
    this.#total = result.value.total;
  }

  /**
   * Removes a row and returns the undo, which puts back only that row: a page appended meanwhile
   * stays, and a list read meanwhile already says whether the row belongs.
   */
  withdraw(recipeId: string): () => void {
    const index = this.#items.findIndex((item) => item.id === recipeId);
    const row = this.#items[index];
    const listing = this.#listing;

    if (!row) {
      return () => {};
    }

    this.#items = this.#items.filter((item) => item.id !== recipeId);

    return () => {
      if (listing === this.#listing && !this.#items.some((item) => item.id === recipeId)) {
        this.#items = [...this.#items.slice(0, index), row, ...this.#items.slice(index)];
      }
    };
  }

  noteRemoved(): void {
    this.#total = Math.max(0, this.#total - 1);
  }

  fail(error: AppError): void {
    this.#error = error;
  }

  clearError(): void {
    this.#error = null;
  }

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
    this.#listing += 1;
    this.#context = null;
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

  /** Claims the right to write and aborts the previous request; the token, not the abort, guarantees correctness. */
  #beginRead(): number {
    this.#reading?.abort();
    this.#reading = new AbortController();

    return ++this.#readToken;
  }
}
