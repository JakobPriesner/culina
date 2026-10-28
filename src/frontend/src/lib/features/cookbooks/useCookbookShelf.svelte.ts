import { effectiveSort, RecipeQuery } from '$features/recipes/stores/libraryView.svelte';
import { createRecipeStore } from '$features/recipes/stores/recipes.svelte';
import { registerStore } from '$shell/stores';

import { cookbooks } from './stores/cookbooks.svelte';

/**
 * Kept across visits, so coming back to a shelf shows its recipes while they refresh; the list drops
 * them when the household or cookbook differs, and sign-out resets it.
 */
const shelf = createRecipeStore();

registerStore(() => shelf.reset());

interface Page {
  readonly cookbookId: () => string;
  readonly householdId: () => string | null;
}

/** The recipes on one shelf: a recipe store of its own (not the library's) with a `cookbookId` filter and its own query. */
export function useCookbookShelf(page: Page) {
  /** Its own query for the same reason, on the library's class so a shelf sorts and narrows alike. */
  const view = new RecipeQuery();

  const filtered = $derived(view.filtered);

  /** `ranks: false`: "for tonight" ranks the whole library and would promise an order a shelf lacks. */
  const context = $derived({
    searching: view.query.trim().length > 0,
    ranks: false,
    inACookbook: true
  });

  const order = $derived(effectiveSort(view.sort, context));

  let asTypedFor = $state<string | null>(null);

  // One object so the first page, next page and retry ask for the same thing.
  const filters = $derived({
    query: view.query,
    tags: view.tags,
    maxMinutes: view.maxMinutes ?? undefined,
    maxKcal: view.maxKcal ?? undefined,
    cookbookId: page.cookbookId(),
    sort: order,
    asTyped: asTypedFor !== null && asTypedFor === view.query
  });

  const autoLoads = $derived(shelf.hasMore && !shelf.moreFailed);

  $effect(() => {
    if (page.cookbookId()) {
      void cookbooks.load(page.cookbookId());
    }
  });

  $effect(() => {
    const householdId = page.householdId();

    if (householdId && page.cookbookId()) {
      void shelf.list(householdId, filters);
    }
  });

  function more() {
    const householdId = page.householdId();

    if (householdId && page.cookbookId()) {
      void shelf.loadMore(householdId, filters);
    }
  }

  function reload() {
    const householdId = page.householdId();
    const cookbookId = page.cookbookId();

    if (householdId && cookbookId) {
      void shelf.list(householdId, filters);
      void cookbooks.load(cookbookId);
    }
  }

  return {
    shelf,
    view,
    get filtered() {
      return filtered;
    },
    get context() {
      return context;
    },
    get autoLoads() {
      return autoLoads;
    },
    turnDownCorrection() {
      asTypedFor = view.query;
    },
    more,
    reload
  };
}
