import { SvelteMap } from 'svelte/reactivity';

import { registerStore } from '$shell/stores';

import type { PresumableDiet, RecipeSummary } from '../types';

const presumed = new SvelteMap<string, PresumableDiet>();

registerStore(() => presumed.clear());

/**
 * The recipes a search found vegetarian only because nothing in them says
 * otherwise, so that each can ask, once opened, whether it is.
 *
 * Kept by the recipe store as its pages arrive, wherever the search was made —
 * the overlay, the library, a cookbook — rather than carried in a link: the
 * presumption is a fact about the recipe, true until somebody answers, and
 * not a property of how anybody got to it. Memory only, so a reload forgets
 * it and nothing about it outlives the session.
 */
export const presumedDiets = {
  /** Takes note of what a page of results presumed. */
  note(items: readonly RecipeSummary[]): void {
    for (const item of items) {
      if (item.presumedDiet) {
        presumed.set(item.id, item.presumedDiet);
      }
    }
  },

  /** The diet this recipe was presumed to keep, or null. */
  of(recipeId: string): PresumableDiet | null {
    return presumed.get(recipeId) ?? null;
  },

  /** Answered: not to be asked again. */
  settle(recipeId: string): void {
    presumed.delete(recipeId);
  }
};
