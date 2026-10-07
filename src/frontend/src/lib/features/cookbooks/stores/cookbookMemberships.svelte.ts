import { http, request } from '$api';

import { toMembership } from '../mappers';
import type { CookbookMembership } from '../types';

/**
 * Which recipes are on which shelves, from both sides.
 *
 * The shelves themselves are `cookbookShelves.svelte.ts`; this is only the ticks.
 */
export class Memberships {
  /**
   * Which cookbooks the recipe being looked at is on, by recipe id.
   *
   * Kept here rather than on the recipe, because it is a fact about the shelves
   * and it has to change the moment one does — the tick in the sheet and the
   * line under the title are the same answer and must never disagree.
   */
  #byRecipe = $state<Record<string, CookbookMembership[]>>({});

  /**
   * Every recipe on a shelf, by cookbook id — the same fact from the other
   * side, for a picker that has to mark what is already on before anybody taps
   * it. The shelf's own recipe list is paged, so it cannot answer that past
   * the first screen.
   */
  #byShelf = $state<Record<string, readonly string[]>>({});

  /**
   * Whose shelves the memberships were read from. Plain rather than $state: it
   * is read before the first await of a method an effect calls, and a tracked
   * read there would make the method's own writes call it again.
   */
  #readFor: string | null = null;

  /** The cookbooks a recipe is on, or an empty list until it has been asked. */
  of(recipeId: string): readonly CookbookMembership[] {
    return this.#byRecipe[recipeId] ?? [];
  }

  /** The recipes on a shelf, or an empty list until it has been asked. */
  membersOf(cookbookId: string): readonly string[] {
    return this.#byShelf[cookbookId] ?? [];
  }

  contains(recipeId: string, cookbookId: string): boolean {
    return this.of(recipeId).some((shelf) => shelf.id === cookbookId);
  }

  /**
   * Which cookbooks a recipe is on: the shelves of the household named, which
   * an inherited recipe can be on too, or of the recipe's own household.
   */
  async load(recipeId: string, householdId: string | null): Promise<void> {
    // The same recipe is on different shelves in different households.
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

  /** Which recipes are on a shelf, all of them. */
  async loadMembers(cookbookId: string): Promise<void> {
    const result = await request(() =>
      http.GET('/api/v1/cookbooks/{cookbookId}/recipes', { params: { path: { cookbookId } } })
    );

    if (result.ok) {
      this.#byShelf = { ...this.#byShelf, [cookbookId]: result.value.recipeIds };
    }
  }

  /** Ticks a recipe on or off a shelf, and returns what puts both views back. */
  set(recipeId: string, cookbook: CookbookMembership, on: boolean): () => void {
    const before = this.of(recipeId);
    const members = this.membersOf(cookbook.id);

    this.#byRecipe = {
      ...this.#byRecipe,
      [recipeId]: on ? [...before, cookbook] : before.filter((shelf) => shelf.id !== cookbook.id)
    };
    this.#byShelf = {
      ...this.#byShelf,
      [cookbook.id]: on ? [...members, recipeId] : members.filter((id) => id !== recipeId)
    };

    return () => {
      this.#byRecipe = { ...this.#byRecipe, [recipeId]: [...before] };
      this.#byShelf = { ...this.#byShelf, [cookbook.id]: members };
    };
  }

  reset(): void {
    this.#byRecipe = {};
    this.#byShelf = {};
    this.#readFor = null;
  }
}
