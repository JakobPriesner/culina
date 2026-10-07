import { ErrorCodes, http, request, type AppError } from '$api';
import { registerStore, type LoadStatus } from '$shell/stores';

import { toRecipe, toWireRecipe } from '../mappers';
import type { Facets, Interpretation, Recipe, RecipeSummary } from '../types';

import { RecipeList, type RecipeFilters } from './recipeList.svelte';
import { WriteQueue } from './writeQueue';

export type { RecipeFilters };

/**
 * The recipes a household has, and the one being looked at.
 *
 * Components read domain data from here and from nowhere else. A component
 * that fetches for itself is a component whose data nothing else can see, and
 * two of them on one screen are two requests and two answers.
 *
 * The list and its paging are `recipeList.svelte.ts`; this is the open recipe
 * and every write, which reach the list only to keep its rows in step.
 */
class RecipeStore {
  #list = new RecipeList();
  #detail = $state<Recipe | null>(null);

  /**
   * How the open recipe's read went, apart from the list's.
   *
   * The library can still be reading when a recipe opens, and its answer is
   * about the list: a late list failure must not put an error over a recipe
   * that loaded, nor a late list success hide a recipe that is not there.
   */
  #detailStatus = $state<LoadStatus>('idle');
  #detailError = $state<AppError | null>(null);

  /** Ids of rows whose change has been applied here but not yet confirmed. */
  #pending = $state<string[]>([]);

  #writes = new WriteQueue();

  /**
   * Which recipe read is allowed to write the open recipe — the same guard as
   * the list's, for going from one recipe to the next before the first answers.
   */
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

  /** Replaces the list. Used when the filters change. */
  list(householdId: string, filters: RecipeFilters = {}): Promise<void> {
    return this.#list.list(householdId, filters);
  }

  /** Appends the next page. The list already on screen is never disturbed. */
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

    // Trusted rather than refetched: the server just told us what it made, and
    // asking again would be a second round trip to learn the same thing.
    const created = toRecipe(result.value);

    this.#detail = created;

    return created;
  }

  /**
   * Makes a household its own copy of a recipe it can read — how a household
   * changes a recipe it only inherits.
   *
   * The copy becomes the open recipe, trusted from the answer as a new one is.
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
   * Applies a change here first, then sends it.
   *
   * The whole recipe is snapshotted before the change and that exact snapshot
   * is restored on failure — never an inverse operation. Inverses drift: undoing
   * "set the title" by setting it back is only correct if nothing else moved.
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

    // Put back exactly what was there. A stale version is not a reason to
    // retry: somebody else's change would be lost.
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

      // Only once it is really gone: the recipe is still on screen behind the
      // question while it is being asked. Afterwards, going back to its
      // address asks the server rather than drawing a recipe that is not there.
      if (this.#detail?.id === recipeId) {
        this.#detail = null;
      }

      return null;
    }

    restore();
    this.#list.fail(outcome.error);

    return outcome.error;
  }

  /** True when the failure means somebody else changed it first. */
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
 * A list of recipes nobody else is sharing.
 *
 * More than one can be on screen at once: the cookbook page draws a shelf while
 * the picker above it searches everything, and a single shared store would mean
 * each kept replacing the other's contents.
 */
export const createRecipeStore = (): RecipeStore => new RecipeStore();

/** The one the collection and the recipe pages read from. */
export const recipes = createRecipeStore();

export const changedElsewhere = RecipeStore.changedElsewhere;

registerStore(() => recipes.reset());
