import { http, request, type AppError } from '$api';
import type { LoadStatus } from '$shell/stores';

import { toCookbook, toDetail } from '../mappers';
import type { Cookbook, CookbookDetail, CookbookRules } from '../types';

const pageSize = 24;

// Undefined (not empty rules) for a manual cookbook; the server rejects rules on one.
const toWireRules = (rules?: CookbookRules | null) =>
  rules
    ? {
        tags: [...rules.tags],
        ingredients: [...rules.ingredients],
        maxMinutes: rules.maxMinutes ?? undefined
      }
    : undefined;

/** A household's cookbook shelves and the open one; membership lives in `cookbookMemberships.svelte.ts`. */
export class Shelves {
  #items = $state<Cookbook[]>([]);
  #open = $state<CookbookDetail | null>(null);
  #status = $state<LoadStatus>('idle');
  #cursor = $state<string | null>(null);
  #loadingMore = $state(false);
  #moreFailed = $state(false);
  #error = $state<AppError | null>(null);

  // Not $state: `list` runs in an $effect, and a tracked read here would make it re-run on its own writes.
  #loaded = false;

  // Not $state, for the same reason as `#loaded`.
  #householdId: string | null = null;

  get items(): readonly Cookbook[] {
    return this.#items;
  }

  get open(): CookbookDetail | null {
    return this.#open;
  }

  get status(): LoadStatus {
    return this.#status;
  }

  get error(): AppError | null {
    return this.#error;
  }

  get hasMore(): boolean {
    return this.#cursor !== null;
  }

  get moreFailed(): boolean {
    return this.#moreFailed;
  }

  async list(householdId: string): Promise<void> {
    // A household change drops the old shelves and shows the first-read skeleton.
    if (this.#householdId !== householdId) {
      this.#householdId = householdId;
      this.#items = [];
      this.#cursor = null;
      this.#loaded = false;
    }

    // Only the first read shows a skeleton; refreshes keep the shelves on screen.
    if (!this.#loaded) {
      this.#status = 'loading';
    }

    this.#error = null;
    this.#moreFailed = false;

    const result = await request(() =>
      http.GET('/api/v1/cookbooks', {
        params: { query: { householdId, limit: pageSize } }
      })
    );

    this.#loaded = true;

    if (result.ok) {
      this.#items = result.value.items.map(toCookbook);
      this.#cursor = result.value.nextCursor ?? null;
      this.#status = 'ready';

      return;
    }

    this.#error = result.error;
    this.#status = 'failed';
  }

  /** Appends the next page without disturbing what is on screen. */
  async loadMore(householdId: string): Promise<void> {
    if (!this.#cursor || this.#loadingMore) {
      return;
    }

    this.#loadingMore = true;
    this.#moreFailed = false;

    const result = await request(() =>
      http.GET('/api/v1/cookbooks', {
        params: { query: { householdId, limit: pageSize, cursor: this.#cursor ?? undefined } }
      })
    );

    this.#loadingMore = false;

    if (result.ok) {
      this.#items = [...this.#items, ...result.value.items.map(toCookbook)];
      this.#cursor = result.value.nextCursor ?? null;

      return;
    }

    // A failed page keeps what is already shown.
    this.#error = result.error;
    this.#moreFailed = true;
  }

  async load(cookbookId: string): Promise<void> {
    this.#status = 'loading';
    this.#error = null;

    const result = await request(() =>
      http.GET('/api/v1/cookbooks/{cookbookId}', { params: { path: { cookbookId } } })
    );

    if (result.ok) {
      this.#open = toDetail(result.value);
      this.#status = 'ready';

      return;
    }

    this.#error = result.error;
    this.#status = 'failed';
  }

  /** Creates a cookbook; rules make it fill itself, with no separate flag. */
  async create(
    householdId: string,
    name: string,
    description?: string,
    rules?: CookbookRules | null
  ): Promise<CookbookDetail | null> {
    const result = await request(() =>
      http.POST('/api/v1/cookbooks', {
        body: { householdId, name, description, rules: toWireRules(rules) }
      })
    );

    if (!result.ok) {
      this.#error = result.error;

      return null;
    }

    const created = toDetail(result.value);

    // Front of the list, where the server orders it anyway.
    this.#items = [created, ...this.#items];
    this.#error = null;

    return created;
  }

  async rename(
    cookbookId: string,
    name: string,
    description: string | null,
    rules?: CookbookRules | null
  ): Promise<boolean> {
    const current = this.#open;

    if (!current || current.id !== cookbookId) {
      return false;
    }

    const result = await request(() =>
      http.PATCH('/api/v1/cookbooks/{cookbookId}', {
        params: { path: { cookbookId } },
        body: { name, description: description ?? undefined, rules: toWireRules(rules) },
        headers: { 'If-Match': `"v${current.version}"` }
      })
    );

    if (!result.ok) {
      this.#error = result.error;

      return false;
    }

    const renamed = toDetail(result.value);

    this.#open = renamed;
    this.#items = this.#items.map((shelf) =>
      shelf.id === cookbookId
        ? { ...shelf, name: renamed.name, description: renamed.description }
        : shelf
    );
    this.#error = null;

    return true;
  }

  /** Deletes the open cookbook at the version it was opened at, so a concurrent edit gets a 412. */
  async remove(cookbookId: string): Promise<boolean> {
    const current = this.#open;

    if (!current || current.id !== cookbookId) {
      return false;
    }

    const removed = this.#items;

    // Optimistic; restored below on failure.
    this.#items = this.#items.filter((shelf) => shelf.id !== cookbookId);

    const result = await request(() =>
      http.DELETE('/api/v1/cookbooks/{cookbookId}', {
        params: { path: { cookbookId } },
        headers: { 'If-Match': `"v${current.version}"` }
      })
    );

    if (result.ok) {
      this.#error = null;

      return true;
    }

    this.#items = removed;
    this.#error = result.error;

    return false;
  }

  /** Changes a shelf's recipe count at once and returns what takes just this change back; counts add up, so other writes meanwhile stay. */
  countRecipe(cookbookId: string, delta: 1 | -1): () => void {
    const add = (by: number) => {
      this.#items = this.#items.map((shelf) =>
        shelf.id === cookbookId ? { ...shelf, recipeCount: shelf.recipeCount + by } : shelf
      );
    };

    add(delta);

    return () => add(-delta);
  }

  fail(error: AppError): void {
    this.#error = error;
  }

  clearError(): void {
    this.#error = null;
  }

  reset(): void {
    this.#items = [];
    this.#open = null;
    this.#status = 'idle';
    this.#error = null;
    this.#cursor = null;
    this.#loadingMore = false;
    this.#moreFailed = false;
    this.#loaded = false;
    this.#householdId = null;
  }
}
