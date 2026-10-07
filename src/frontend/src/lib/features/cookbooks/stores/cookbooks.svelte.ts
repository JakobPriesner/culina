import { ErrorCodes, http, request, type AppError } from '$api';
import { registerStore, type LoadStatus } from '$shell/stores';

import type { Cookbook, CookbookDetail, CookbookMembership, CookbookRules } from '../types';

import { Memberships } from './cookbookMemberships.svelte';
import { Shelves } from './cookbookShelves.svelte';

/**
 * A household's shelves.
 *
 * This store knows what shelves exist and what is on them. It does not know
 * what a recipe is: the recipes on a cookbook are the recipe store's, read with
 * a `cookbookId` filter, so a cookbook page gets search, filters and paging
 * without a second implementation of any of them.
 *
 * The shelves are `cookbookShelves.svelte.ts`, what is on them
 * `cookbookMemberships.svelte.ts`; ticking a recipe on or off is the one thing
 * that touches both, so it lives here.
 */
class CookbookStore {
  #shelves = new Shelves();
  #memberships = new Memberships();

  get items(): readonly Cookbook[] {
    return this.#shelves.items;
  }

  get open(): CookbookDetail | null {
    return this.#shelves.open;
  }

  get status(): LoadStatus {
    return this.#shelves.status;
  }

  get error(): AppError | null {
    return this.#shelves.error;
  }

  get hasMore(): boolean {
    return this.#shelves.hasMore;
  }

  get moreFailed(): boolean {
    return this.#shelves.moreFailed;
  }

  membershipsOf(recipeId: string): readonly CookbookMembership[] {
    return this.#memberships.of(recipeId);
  }

  membersOf(cookbookId: string): readonly string[] {
    return this.#memberships.membersOf(cookbookId);
  }

  contains(recipeId: string, cookbookId: string): boolean {
    return this.#memberships.contains(recipeId, cookbookId);
  }

  list(householdId: string): Promise<void> {
    return this.#shelves.list(householdId);
  }

  loadMore(householdId: string): Promise<void> {
    return this.#shelves.loadMore(householdId);
  }

  load(cookbookId: string): Promise<void> {
    return this.#shelves.load(cookbookId);
  }

  create(
    householdId: string,
    name: string,
    description?: string,
    rules?: CookbookRules | null
  ): Promise<CookbookDetail | null> {
    return this.#shelves.create(householdId, name, description, rules);
  }

  rename(
    cookbookId: string,
    name: string,
    description: string | null,
    rules?: CookbookRules | null
  ): Promise<boolean> {
    return this.#shelves.rename(cookbookId, name, description, rules);
  }

  remove(cookbookId: string): Promise<boolean> {
    return this.#shelves.remove(cookbookId);
  }

  loadMemberships(recipeId: string, householdId: string | null = null): Promise<void> {
    return this.#memberships.load(recipeId, householdId);
  }

  loadMembers(cookbookId: string): Promise<void> {
    return this.#memberships.loadMembers(cookbookId);
  }

  /**
   * Puts a recipe on a shelf, or takes it off.
   *
   * One method because it is one control: a tick that changes what it means is
   * still a tick, and two methods would be two places for the optimistic
   * bookkeeping to drift.
   */
  async setOn(recipeId: string, cookbook: CookbookMembership, on: boolean): Promise<boolean> {
    const undoTick = this.#memberships.set(recipeId, cookbook, on);
    const undoCount = this.#shelves.countRecipe(cookbook.id, on ? 1 : -1);

    const path = { path: { cookbookId: cookbook.id, recipeId } };

    const result = on
      ? await request(() =>
          http.PUT('/api/v1/cookbooks/{cookbookId}/recipes/{recipeId}', { params: path })
        )
      : await request(() =>
          http.DELETE('/api/v1/cookbooks/{cookbookId}/recipes/{recipeId}', { params: path })
        );

    if (result.ok) {
      this.#shelves.clearError();

      return true;
    }

    undoTick();
    undoCount();
    this.#shelves.fail(result.error);

    return false;
  }

  /** Whether a failure means somebody else wrote first. */
  static changedElsewhere(error: AppError): boolean {
    return error.code === ErrorCodes.versionMismatch || error.status === 409;
  }

  clearError(): void {
    this.#shelves.clearError();
  }

  reset(): void {
    this.#shelves.reset();
    this.#memberships.reset();
  }
}

export const cookbooks = new CookbookStore();

registerStore(() => cookbooks.reset());
