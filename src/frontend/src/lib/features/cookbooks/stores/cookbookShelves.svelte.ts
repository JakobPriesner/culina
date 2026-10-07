import { http, request, type AppError } from '$api';
import type { LoadStatus } from '$shell/stores';

import { toCookbook, toDetail } from '../mappers';
import type { Cookbook, CookbookDetail, CookbookRules } from '../types';

const pageSize = 24;

/**
 * Rules as the API takes them, or nothing at all.
 *
 * Undefined rather than an empty object for a manual cookbook: sending blank
 * rules would be claiming it has some, and the server rightly refuses to give a
 * shelf somebody fills by hand a set of conditions as well.
 */
const toWireRules = (rules?: CookbookRules | null) =>
  rules
    ? {
        tags: [...rules.tags],
        ingredients: [...rules.ingredients],
        maxMinutes: rules.maxMinutes ?? undefined
      }
    : undefined;

/**
 * The shelves a household has and the one that is open: reading, paging,
 * making, renaming and deleting them. Which recipes are on them is
 * `cookbookMemberships.svelte.ts`.
 */
export class Shelves {
  #items = $state<Cookbook[]>([]);
  #open = $state<CookbookDetail | null>(null);
  #status = $state<LoadStatus>('idle');
  #cursor = $state<string | null>(null);
  #loadingMore = $state(false);
  #moreFailed = $state(false);
  #error = $state<AppError | null>(null);

  /**
   * Whether a first answer has ever arrived.
   *
   * Deliberately **not** `$state`. `list` is called from an `$effect`, and an
   * effect tracks every reactive value read while it runs — so a `list` that
   * read `#items` to decide whether to show a skeleton would depend on the
   * thing it is about to write, and re-run itself forever. The same trap
   * `units.svelte.ts` documents.
   */
  #loaded = false;

  /**
   * Whose items these are. Plain rather than $state: it is read before the
   * first await of a method an effect calls, and a tracked read there would
   * make the method's own writes call it again.
   */
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
    // Another household's shelves are not the ones to keep while this one's
    // arrive, so a change of household gets the skeleton a first read gets.
    if (this.#householdId !== householdId) {
      this.#householdId = householdId;
      this.#items = [];
      this.#cursor = null;
      this.#loaded = false;
    }

    // The shelves already on screen stay while the next answer arrives:
    // replacing them with a skeleton to show the same shelves again loses your
    // place for nothing. Only the very first read shows one.
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

  /** Appends the next page. What is already on screen is never disturbed. */
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

    // The page that is there stays. A failed page is a reason to stop fetching
    // and ask, not to empty the screen.
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

  /**
   * Starts a cookbook.
   *
   * Rules are what makes one that fills itself; there is no separate flag that
   * could disagree with them.
   */
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

    // Straight to the front, where the list orders it anyway: a shelf somebody
    // just made should be the one they can see.
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

  /**
   * Deletes the open cookbook, quoting the version it was opened at: somebody
   * who renamed it or changed its rules in the meantime gets a 412 here, not a
   * shelf that vanished under them.
   */
  async remove(cookbookId: string): Promise<boolean> {
    const current = this.#open;

    if (!current || current.id !== cookbookId) {
      return false;
    }

    const removed = this.#items;

    // Gone from the screen before the server has agreed, and put back exactly
    // as it was if it does not.
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

  /** Changes a shelf's recipe count at once and returns what restores it. */
  countRecipe(cookbookId: string, delta: 1 | -1): () => void {
    const before = this.#items;

    this.#items = before.map((shelf) =>
      shelf.id === cookbookId ? { ...shelf, recipeCount: shelf.recipeCount + delta } : shelf
    );

    return () => {
      this.#items = before;
    };
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
