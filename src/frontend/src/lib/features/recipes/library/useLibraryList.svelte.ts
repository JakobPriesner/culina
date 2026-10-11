import { untrack } from 'svelte';

import { effectiveSort, libraryView } from '$features/recipes/stores/libraryView.svelte';
import { recipes } from '$features/recipes/stores/recipes.svelte';

interface Page {
  readonly householdId: () => string | null;
  /** Whether the ranking has enough history to take over the default order. */
  readonly ranks: () => boolean;
}

/** The recipe list for the toolbar's query and order, plus paging; a refetch keeps the list on screen. */
export function useLibraryList(page: Page) {
  const searching = $derived(libraryView.query.trim().length > 0);

  const context = $derived({ searching, ranks: page.ranks(), inACookbook: false });

  const order = $derived(effectiveSort(libraryView.sort, context));

  // Compared rather than cleared, so the refusal lapses as soon as the query changes.
  let asTypedFor = $state<string | null>(null);

  const filters = $derived({
    query: libraryView.query,
    tags: libraryView.tags,
    maxMinutes: libraryView.maxMinutes ?? undefined,
    maxKcal: libraryView.maxKcal ?? undefined,
    asTyped: asTypedFor !== null && asTypedFor === libraryView.query,
    // Always explicit so the label above the grid can't drift from the request.
    sort: order
  });

  // Stops after a failed page, or the end-of-list trigger would retry forever while offline.
  const autoLoads = $derived(recipes.status === 'ready' && recipes.hasMore && !recipes.moreFailed);

  $effect(() => {
    const householdId = page.householdId();

    if (householdId) {
      untrack(() => libraryView.forHousehold(householdId));
    }
  });

  $effect(() => {
    const householdId = page.householdId();

    if (householdId) {
      void recipes.list(householdId, filters);
    }
  });

  function retry() {
    recipes.clearError();

    const householdId = page.householdId();

    if (householdId) {
      void recipes.list(householdId, filters);
    }
  }

  /** Loads the next page; also the retry after a failed page. */
  function more() {
    const householdId = page.householdId();

    if (householdId) {
      void recipes.loadMore(householdId, filters);
    }
  }

  return {
    get context() {
      return context;
    },
    get order() {
      return order;
    },
    get autoLoads() {
      return autoLoads;
    },
    turnDownCorrection() {
      asTypedFor = libraryView.query;
    },
    retry,
    more
  };
}
