import { http, request } from '$api';

import { toMembership } from '../mappers';
import type { CookbookMembership } from '../types';

/** Which recipes are on which shelves, from both sides; the shelves are in `cookbookShelves.svelte.ts`. */
export class Memberships {
  // By recipe id; kept here so the sheet's tick and the line under the title can't disagree.
  #byRecipe = $state<Record<string, CookbookMembership[]>>({});

  // By cookbook id, for pickers: the shelf's own recipe list is paged and can't answer past page one.
  #byShelf = $state<Record<string, readonly string[]>>({});

  // Not $state: read before the first await of effect-called methods, where a tracked read would loop.
  #readFor: string | null = null;

  of(recipeId: string): readonly CookbookMembership[] {
    return this.#byRecipe[recipeId] ?? [];
  }

  membersOf(cookbookId: string): readonly string[] {
    return this.#byShelf[cookbookId] ?? [];
  }

  contains(recipeId: string, cookbookId: string): boolean {
    return this.of(recipeId).some((shelf) => shelf.id === cookbookId);
  }

  /** Loads a recipe's cookbooks in the named household (inherited recipes can be shelved there) or its own. */
  async load(recipeId: string, householdId: string | null): Promise<void> {
    // A recipe is on different shelves in different households.
    if (this.#readFor !== householdId) {
      this.#readFor = householdId;
      this.#byRecipe = {};
    }

    const result = await request(() =>
      http.GET('/api/v1/recipes/{recipeId}/cookbooks', {
        params: { path: { recipeId }, query: householdId ? { householdId } : {} }
      })
    );

    if (result.ok) {
      this.#byRecipe = { ...this.#byRecipe, [recipeId]: result.value.items.map(toMembership) };
    }
  }

  async loadMembers(cookbookId: string): Promise<void> {
    const result = await request(() =>
      http.GET('/api/v1/cookbooks/{cookbookId}/recipes', { params: { path: { cookbookId } } })
    );

    if (result.ok) {
      this.#byShelf = { ...this.#byShelf, [cookbookId]: result.value.recipeIds };
    }
  }

  /** Ticks a recipe on or off a shelf, and returns what puts just that tick back (other ticks meanwhile stay). */
  set(recipeId: string, cookbook: CookbookMembership, on: boolean): () => void {
    this.#apply(recipeId, cookbook, on);

    return () => this.#apply(recipeId, cookbook, !on);
  }

  #apply(recipeId: string, cookbook: CookbookMembership, on: boolean): void {
    const shelves = this.of(recipeId).filter((shelf) => shelf.id !== cookbook.id);
    const members = this.membersOf(cookbook.id).filter((id) => id !== recipeId);

    this.#byRecipe = { ...this.#byRecipe, [recipeId]: on ? [...shelves, cookbook] : shelves };
    this.#byShelf = { ...this.#byShelf, [cookbook.id]: on ? [...members, recipeId] : members };
  }

  reset(): void {
    this.#byRecipe = {};
    this.#byShelf = {};
    this.#readFor = null;
  }
}
