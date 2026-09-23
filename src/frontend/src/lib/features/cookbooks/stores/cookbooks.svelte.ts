import { ErrorCodes, http, request, type AppError } from '$api';
import { registerStore, type LoadStatus } from '$shell/stores';

import { toCookbook, toDetail, toMembership } from '../mappers';
import type { Cookbook, CookbookDetail, CookbookMembership, CookbookRules } from '../types';

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
 * A household's shelves.
 *
 * This store knows what shelves exist and what is on them. It does not know
 * what a recipe is: the recipes on a cookbook are the recipe store's, read with
 * a `cookbookId` filter, so a cookbook page gets search, filters and paging
 * without a second implementation of any of them.
 */
class CookbookStore {
  #items = $state<Cookbook[]>([]);
  #open = $state<CookbookDetail | null>(null);
  #status = $state<LoadStatus>('idle');
  #error = $state<AppError | null>(null);
  #cursor = $state<string | null>(null);
  #loadingMore = $state(false);
  #moreFailed = $state(false);

  /**
   * Which cookbooks the recipe being looked at is on, by recipe id.
   *
   * Kept here rather than on the recipe, because it is a fact about the shelves
   * and it has to change the moment one does — the tick in the sheet and the
   * line under the title are the same answer and must never disagree.
   */
  #memberships = $state<Record<string, CookbookMembership[]>>({});

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

  /** The cookbooks a recipe is on, or an empty list until it has been asked. */
  membershipsOf(recipeId: string): readonly CookbookMembership[] {
    return this.#memberships[recipeId] ?? [];
  }

  contains(recipeId: string, cookbookId: string): boolean {
    return this.membershipsOf(recipeId).some((shelf) => shelf.id === cookbookId);
  }

  async list(householdId: string): Promise<void> {
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

  async remove(cookbookId: string): Promise<boolean> {
    const removed = this.#items;

    // Gone from the screen before the server has agreed, and put back exactly
    // as it was if it does not.
    this.#items = this.#items.filter((shelf) => shelf.id !== cookbookId);

    const result = await request(() =>
      http.DELETE('/api/v1/cookbooks/{cookbookId}', { params: { path: { cookbookId } } })
    );

    if (result.ok) {
      this.#error = null;

      return true;
    }

    this.#items = removed;
    this.#error = result.error;

    return false;
  }

  /** Which cookbooks a recipe is on. */
  async loadMemberships(recipeId: string): Promise<void> {
    const result = await request(() =>
      http.GET('/api/v1/recipes/{recipeId}/cookbooks', { params: { path: { recipeId } } })
    );

    if (result.ok) {
      this.#memberships = {
        ...this.#memberships,
        [recipeId]: result.value.items.map(toMembership)
      };
    }
  }

  /**
   * Puts a recipe on a shelf, or takes it off.
   *
   * One method because it is one control: a tick that changes what it means is
   * still a tick, and two methods would be two places for the optimistic
   * bookkeeping to drift.
   */
  async setOn(recipeId: string, cookbook: CookbookMembership, on: boolean): Promise<boolean> {
    const before = this.membershipsOf(recipeId);
    const counted = this.#items;

    this.#remember(
      recipeId,
      on ? [...before, cookbook] : before.filter((shelf) => shelf.id !== cookbook.id)
    );
    this.#items = this.#items.map((shelf) =>
      shelf.id === cookbook.id
        ? { ...shelf, recipeCount: shelf.recipeCount + (on ? 1 : -1) }
        : shelf
    );

    const path = { path: { cookbookId: cookbook.id, recipeId } };

    const result = on
      ? await request(() =>
          http.PUT('/api/v1/cookbooks/{cookbookId}/recipes/{recipeId}', { params: path })
        )
      : await request(() =>
          http.DELETE('/api/v1/cookbooks/{cookbookId}/recipes/{recipeId}', { params: path })
        );

    if (result.ok) {
      this.#error = null;

      return true;
    }

    this.#remember(recipeId, [...before]);
    this.#items = counted;
    this.#error = result.error;

    return false;
  }

  /** Whether a failure means somebody else wrote first. */
  static changedElsewhere(error: AppError): boolean {
    return error.code === ErrorCodes.versionMismatch || error.status === 409;
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
    this.#memberships = {};
    this.#loaded = false;
  }

  #remember(recipeId: string, shelves: CookbookMembership[]): void {
    this.#memberships = { ...this.#memberships, [recipeId]: shelves };
  }
}

export const cookbooks = new CookbookStore();

registerStore(() => cookbooks.reset());
