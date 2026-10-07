import { SvelteMap } from 'svelte/reactivity';

import { registerStore } from '$shell/stores';

import type { PresumableDiet, RecipeSummary } from '../types';

const presumed = new SvelteMap<string, PresumableDiet>();

registerStore(() => presumed.clear());

/**
 * Recipes a search found vegetarian only because nothing says otherwise, so each can ask once
 * opened; kept in memory per session, not in a link.
 */
export const presumedDiets = {
  note(items: readonly RecipeSummary[]): void {
    for (const item of items) {
      if (item.presumedDiet) {
        presumed.set(item.id, item.presumedDiet);
      }
    }
  },

  of(recipeId: string): PresumableDiet | null {
    return presumed.get(recipeId) ?? null;
  },

  settle(recipeId: string): void {
    presumed.delete(recipeId);
  }
};
