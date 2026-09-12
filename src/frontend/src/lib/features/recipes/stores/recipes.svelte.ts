import { ErrorCodes, http, request, type AppError } from '$api';
import { registerStore } from '$shell/stores';

import { toRecipe, toSummary, toWireGroups, toWireSteps } from '../mappers';
import type { Recipe, RecipeSummary } from '../types';

/**
 * The recipes a household has, and the one being looked at.
 *
 * Components read domain data from here and from nowhere else. A component
 * that fetches for itself is a component whose data nothing else can see, and
 * two of them on one screen are two requests and two answers.
 */
export interface RecipeFilters {
  readonly query?: string;
  readonly tags?: readonly string[];
  /** Ask what can be cooked from these. Ranked by fit, not filtered. */
  readonly ingredients?: readonly string[];
  readonly maxMinutes?: number;
  readonly sort?: 'recent' | 'title' | 'quickest' | 'match';
}

export type LoadStatus = 'idle' | 'loading' | 'ready' | 'failed';

const pageSize = 24;

class RecipeStore {
  #items = $state<RecipeSummary[]>([]);
  #detail = $state<Recipe | null>(null);
  #status = $state<LoadStatus>('idle');
  #error = $state<AppError | null>(null);
  #total = $state(0);
  #cursor = $state<string | null>(null);
  /** Ids of rows whose change has been applied here but not yet confirmed. */
  #pending = $state<string[]>([]);

  /**
   * One in-flight write per recipe.
   *
   * Two quick edits to the same recipe must not race: the second waits for the
   * first, so the version it sends is the one the first produced.
   */
  #writes = new Map<string, Promise<unknown>>();

  get items(): readonly RecipeSummary[] {
    return this.#items;
  }

  get detail(): Recipe | null {
    return this.#detail;
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

  /** True while more pages exist. */
  get hasMore(): boolean {
    return this.#cursor !== null;
  }

  isPending(id: string): boolean {
    return this.#pending.includes(id);
  }

  /** Replaces the list. Used when the filters change. */
  async list(householdId: string, filters: RecipeFilters = {}): Promise<void> {
    this.#status = 'loading';
    this.#error = null;

    const result = await this.#fetchPage(householdId, filters, null);

    result.match(
      (page) => {
        this.#items = page.items;
        this.#cursor = page.nextCursor;
        this.#total = page.total;
        this.#status = 'ready';
      },
      (error) => {
        this.#error = error;
        this.#status = 'failed';
      }
    );
  }

  /** Appends the next page. The list already on screen is never disturbed. */
  async loadMore(householdId: string, filters: RecipeFilters = {}): Promise<void> {
    if (!this.#cursor) {
      return;
    }

    const result = await this.#fetchPage(householdId, filters, this.#cursor);

    result.match(
      (page) => {
        this.#items = [...this.#items, ...page.items];
        this.#cursor = page.nextCursor;
        this.#total = page.total;
      },
      (error) => {
        // The page that is already there stays. A failed "load more" is a
        // reason to offer the button again, not to empty the screen.
        this.#error = error;
      }
    );
  }

  async load(recipeId: string): Promise<void> {
    this.#status = 'loading';
    this.#error = null;

    const result = await request(() =>
      http.GET('/api/v1/recipes/{recipeId}', { params: { path: { recipeId } } })
    );

    if (!result.ok) {
      this.#error = result.error;
      this.#status = 'failed';

      return;
    }

    this.#detail = toRecipe(result.value);
    this.#status = 'ready';
  }

  async create(householdId: string, title: string): Promise<Recipe | AppError> {
    const result = await request(() =>
      http.POST('/api/v1/recipes', { body: { householdId, title } })
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

    const outcome = await this.#serialise(next.id, async () => {
      const result = await request(() =>
        http.PUT('/api/v1/recipes/{recipeId}', {
          params: { path: { recipeId: next.id } },
          headers: { 'If-Match': `"v${next.version}"` },
          body: {
            title: next.title,
            description: next.description ?? undefined,
            language: next.language,
            yieldAmount: next.yieldAmount,
            yieldKind: next.yieldKind,
            prepMinutes: next.prepMinutes ?? undefined,
            cookMinutes: next.cookMinutes ?? undefined,
            groups: toWireGroups(next.groups),
            steps: toWireSteps(next.steps),
            tags: [...next.tags]
          }
        })
      );

      return result;
    });

    this.#markPending(next.id, false);

    if (outcome.ok) {
      this.#detail = toRecipe(outcome.value);
      this.#refreshSummary(this.#detail);

      return null;
    }

    // Put back exactly what was there. A stale version is not a reason to
    // retry: somebody else's change would be lost.
    this.#detail = before;
    this.#error = outcome.error;

    return outcome.error;
  }

  async remove(recipeId: string, version: number): Promise<AppError | null> {
    const before = this.#items;

    this.#items = this.#items.filter((item) => item.id !== recipeId);

    const outcome = await this.#serialise(recipeId, () =>
      request(() =>
        http.DELETE('/api/v1/recipes/{recipeId}', {
          params: { path: { recipeId } },
          headers: { 'If-Match': `"v${version}"` }
        })
      )
    );

    if (outcome.ok) {
      this.#total = Math.max(0, this.#total - 1);

      return null;
    }

    this.#items = before;
    this.#error = outcome.error;

    return outcome.error;
  }

  /** True when the failure means somebody else changed it first. */
  static changedElsewhere(error: AppError): boolean {
    return error.code === ErrorCodes.versionMismatch || error.status === 409;
  }

  clearError(): void {
    this.#error = null;
  }

  reset(): void {
    this.#items = [];
    this.#detail = null;
    this.#status = 'idle';
    this.#error = null;
    this.#total = 0;
    this.#cursor = null;
    this.#pending = [];
    this.#writes.clear();
  }

  async #fetchPage(householdId: string, filters: RecipeFilters, cursor: string | null) {
    const result = await request(() =>
      http.GET('/api/v1/recipes', {
        params: {
          query: {
            householdId,
            query: filters.query || undefined,
            tag: filters.tags?.length ? [...filters.tags] : undefined,
            ingredient: filters.ingredients?.length ? [...filters.ingredients] : undefined,
            maxMinutes: filters.maxMinutes,
            sort: filters.sort,
            cursor: cursor ?? undefined,
            limit: pageSize
          }
        }
      })
    );

    return {
      match: <TOut>(
        onPage: (page: {
          items: RecipeSummary[];
          nextCursor: string | null;
          total: number;
        }) => TOut,
        onError: (error: AppError) => TOut
      ): TOut =>
        result.ok
          ? onPage({
              items: result.value.items.map(toSummary),
              nextCursor: result.value.nextCursor ?? null,
              total: result.value.total
            })
          : onError(result.error)
    };
  }

  /** Queues a write behind whatever is already in flight for this recipe. */
  async #serialise<TResult>(id: string, write: () => Promise<TResult>): Promise<TResult> {
    const queued = (this.#writes.get(id) ?? Promise.resolve()).then(write, write);

    this.#writes.set(id, queued);

    try {
      return await queued;
    } finally {
      if (this.#writes.get(id) === queued) {
        this.#writes.delete(id);
      }
    }
  }

  #markPending(id: string, pending: boolean): void {
    this.#pending = pending
      ? [...this.#pending, id]
      : this.#pending.filter((candidate) => candidate !== id);
  }

  /** Keeps the row in the list in step with the recipe that was just saved. */
  #refreshSummary(recipe: Recipe): void {
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
}

export const recipes = new RecipeStore();

export const changedElsewhere = RecipeStore.changedElsewhere;

registerStore(() => recipes.reset());
