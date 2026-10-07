import { ErrorCodes, http, request, type AppError } from '$api';
import { registerStore, type LoadStatus } from '$shell/stores';

import { toRecipe, toWireRecipe } from '../mappers';
import type { Facets, Interpretation, Recipe, RecipeSummary } from '../types';

import { RecipeList, type RecipeFilters } from './recipeList.svelte';
import { WriteQueue } from './writeQueue';

export type { RecipeFilters };

/**
 * The open recipe and every write; the list and paging are `recipeList.svelte.ts`, touched only to
 * keep its rows in step.
 */
class RecipeStore {
  #list = new RecipeList();
  #detail = $state<Recipe | null>(null);

  /**
   * Apart from the list's status: a late list failure must not put an error over a loaded recipe.
   */
  #detailStatus = $state<LoadStatus>('idle');
  #detailError = $state<AppError | null>(null);

  #pending = $state<string[]>([]);

  #writes = new WriteQueue();

  /** The same stale-read guard as the list's, for going from one recipe to the next. */
  #detailToken = 0;

  get items(): readonly RecipeSummary[] {
    return this.#list.items;
  }

  get detail(): Recipe | null {
    return this.#detail;
  }

  get status(): LoadStatus {
    return this.#list.status;
  }

  get error(): AppError | null {
    return this.#list.error;
  }

  get detailStatus(): LoadStatus {
    return this.#detailStatus;
  }

  get detailError(): AppError | null {
    return this.#detailError;
  }

  get total(): number {
    return this.#list.total;
  }

  get interpretation(): Interpretation | null {
    return this.#list.interpretation;
  }

  get facets(): Facets | null {
    return this.#list.facets;
  }

  get hasMore(): boolean {
    return this.#list.hasMore;
  }

  get moreFailed(): boolean {
    return this.#list.moreFailed;
  }

  isPending(id: string): boolean {
    return this.#pending.includes(id);
  }

  list(householdId: string, filters: RecipeFilters = {}): Promise<void> {
    return this.#list.list(householdId, filters);
  }

  loadMore(householdId: string, filters: RecipeFilters = {}): Promise<void> {
    return this.#list.loadMore(householdId, filters);
  }

  async load(recipeId: string): Promise<void> {
    const token = ++this.#detailToken;

    this.#detailStatus = 'loading';
    this.#detailError = null;

    const result = await request(() =>
      http.GET('/api/v1/recipes/{recipeId}', { params: { path: { recipeId } } })
    );

    if (token !== this.#detailToken) {
      return;
    }

    if (!result.ok) {
      this.#detailError = result.error;
      this.#detailStatus = 'failed';

      return;
    }

    this.#detail = toRecipe(result.value);
    this.#detailStatus = 'ready';
  }

  async create(
    householdId: string,
    title: string,
    /** The assistant draft it came from, so the recipe records that it was one. */
    draftId?: string,
    sourceUrl?: string
  ): Promise<Recipe | AppError> {
    const result = await request(() =>
      http.POST('/api/v1/recipes', { body: { householdId, title, draftId, sourceUrl } })
    );

    if (!result.ok) {
      return result.error;
    }

    // Trusted from the answer rather than refetched.
    const created = toRecipe(result.value);

    this.#detail = created;

    return created;
  }

  /**
   * Makes the household its own copy of a recipe it only inherits; the copy becomes the open
   * recipe.
   */
  async copy(recipeId: string, householdId: string): Promise<Recipe | AppError> {
    const result = await request(() =>
      http.POST('/api/v1/recipes/{recipeId}/copies', {
        params: { path: { recipeId } },
        body: { householdId }
      })
    );

    if (!result.ok) {
      return result.error;
    }

    const copied = toRecipe(result.value);

    this.#detail = copied;

    return copied;
  }

  /**
   * Applies the change first, then sends it; failure restores the snapshot, since an inverse drifts
   * if anything else moved.
   */
  async update(next: Recipe): Promise<AppError | null> {
    const before = this.#detail;

    this.#detail = next;
    this.#markPending(next.id, true);

    const outcome = await this.#writes.run(next.id, () =>
      request(() =>
        http.PUT('/api/v1/recipes/{recipeId}', {
          params: { path: { recipeId: next.id } },
          headers: { 'If-Match': `"v${next.version}"` },
          body: toWireRecipe(next)
        })
      )
    );

    this.#markPending(next.id, false);

    if (outcome.ok) {
      const saved = toRecipe(outcome.value);

      this.#detail = saved;
      this.#list.refreshSummary(saved);

      return null;
    }

    // Restore exactly what was there; retrying a stale version would lose someone else's change.
    this.#detail = before;

    return outcome.error;
  }

  async remove(recipeId: string, version: number): Promise<AppError | null> {
    const restore = this.#list.withdraw(recipeId);

    const outcome = await this.#writes.run(recipeId, () =>
      request(() =>
        http.DELETE('/api/v1/recipes/{recipeId}', {
          params: { path: { recipeId } },
          headers: { 'If-Match': `"v${version}"` }
        })
      )
    );

    if (outcome.ok) {
      this.#list.noteRemoved();

      // Only once really gone: the recipe stays on screen behind the question while it is asked.
      if (this.#detail?.id === recipeId) {
        this.#detail = null;
      }

      return null;
    }

    restore();
    this.#list.fail(outcome.error);

    return outcome.error;
  }

  static changedElsewhere(error: AppError): boolean {
    return error.code === ErrorCodes.versionMismatch || error.status === 409;
  }

  clearError(): void {
    this.#list.clearError();
  }

  reset(): void {
    this.#list.reset();
    this.#detail = null;
    this.#detailStatus = 'idle';
    this.#detailError = null;
    this.#detailToken += 1;
    this.#pending = [];
    this.#writes.clear();
  }

  #markPending(id: string, pending: boolean): void {
    this.#pending = pending
      ? [...this.#pending, id]
      : this.#pending.filter((candidate) => candidate !== id);
  }
}

/**
 * A list nobody else shares: the cookbook shelf and the picker above it must not replace each
 * other's contents.
 */
export const createRecipeStore = (): RecipeStore => new RecipeStore();

export const recipes = createRecipeStore();

export const changedElsewhere = RecipeStore.changedElsewhere;

registerStore(() => recipes.reset());
