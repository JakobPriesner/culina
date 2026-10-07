import { untrack } from 'svelte';

import { effectiveSort, libraryView } from '$features/recipes/stores/libraryView.svelte';
import { recipes } from '$features/recipes/stores/recipes.svelte';

/** What the list needs to know about the page it is on. */
interface Page {
  readonly householdId: () => string | null;
  /** Whether the ranking has anything true to say about this kitchen yet. */
  readonly ranks: () => boolean;
}

/**
 * The recipe list for what the toolbar says: the query it is asked with, the
 * order it is in, and the next page when the end of it is read.
 *
 * A refetch keeps the list that is already on screen — the old answer is almost
 * always still the right one, and replacing it with a skeleton loses your
 * place.
 */
export function useLibraryList(page: Page) {
  const searching = $derived(libraryView.query.trim().length > 0);

  /**
   * What the toolbar needs in order to decide an order nobody has chosen.
   *
   * `ranks` is the same signal the suggested chip used: the ranking may only
   * take over once it has something true to say about this kitchen.
   */
  const context = $derived({ searching, ranks: page.ranks(), inACookbook: false });

  const order = $derived(effectiveSort(libraryView.sort, context));

  /**
   * The query whose correction was turned down.
   *
   * Compared rather than cleared, so it lapses by itself the moment the query
   * changes: a refusal is about the words it was given, not the next ones.
   */
  let asTypedFor = $state<string | null>(null);

  const filters = $derived({
    query: libraryView.query,
    tags: libraryView.tags,
    maxMinutes: libraryView.maxMinutes ?? undefined,
    asTyped: asTypedFor !== null && asTypedFor === libraryView.query,
    // Always explicit, so that the order the page names above the grid is the
    // order it actually asked for. The server would pick the same one from an
    // absent `sort`, but a label worked out separately from the request is a
    // label that can be wrong.
    sort: order
  });

  /**
   * Whether the end of the list fetches the next page by itself.
   *
   * It stops once a page fails. A list that asks for itself would otherwise
   * ask forever while the connection is down, because the thing that triggers
   * the request — the end of the list, in view — never goes away. From then on
   * it is a button, and one deliberate press is worth more than a thousand.
   */
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

  /**
   * The next page, asked for by reading far enough down.
   *
   * Also the retry: a page that failed is asked for in exactly the same way,
   * by the same call, so there is no second path that can drift.
   */
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
    /** The toolbar's correction was turned down for the words now typed. */
    turnDownCorrection() {
      asTypedFor = libraryView.query;
    },
    retry,
    more
  };
}
