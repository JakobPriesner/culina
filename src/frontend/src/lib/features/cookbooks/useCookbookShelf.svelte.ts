import { effectiveSort, RecipeQuery } from '$features/recipes/stores/libraryView.svelte';
import { createRecipeStore } from '$features/recipes/stores/recipes.svelte';

import { cookbooks } from './stores/cookbooks.svelte';

/** What the shelf needs to know about the page it is on. */
interface Page {
  readonly cookbookId: () => string;
  readonly householdId: () => string | null;
}

/**
 * The recipes on one shelf, and the question they are being asked with.
 *
 * Almost nothing of this is its own. The shelf's recipes are the recipe store
 * with a `cookbookId` filter, drawn by the same grid the collection uses, so
 * searching, the skeletons, the infinite scroll and the empty states arrived
 * here already written.
 */
export function useCookbookShelf(page: Page) {
  /**
   * This page's own list.
   *
   * Not the app's shared one: the collection behind this page is looking at
   * everything, and filtering that store to one shelf would leave it filtered
   * when somebody navigates back.
   */
  const shelf = createRecipeStore();

  /**
   * What this shelf is being asked for.
   *
   * Its own, not the library's: the collection behind this page is looking at
   * everything, and sharing one question would leave the library filtered to a
   * shelf when somebody navigates back. The same class either way, so a shelf
   * can be sorted and narrowed exactly as the library can.
   */
  const view = new RecipeQuery();

  /** Empty because of a filter is a mistake to undo; empty because it is new is an invitation. */
  const filtered = $derived(view.filtered);

  /**
   * A shelf is read in the order it was built, until somebody says otherwise.
   *
   * `ranks` is false here rather than plumbed through: "for tonight" ranks the
   * whole library, and offering it inside a shelf would promise an order over
   * the shelf that it does not mean.
   */
  const context = $derived({
    searching: view.query.trim().length > 0,
    ranks: false,
    inACookbook: true
  });

  const order = $derived(effectiveSort(view.sort, context));

  /** The query whose correction the reader turned down. */
  let asTypedFor = $state<string | null>(null);

  /**
   * Which list is on screen.
   *
   * Built once, so the first page, the next page and the retry cannot ask for
   * three different things.
   */
  const filters = $derived({
    query: view.query,
    tags: view.tags,
    maxMinutes: view.maxMinutes ?? undefined,
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

  /** Reads the shelf and its header again, after something changed what is on it. */
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
    /** The toolbar's correction was turned down for the words now typed. */
    turnDownCorrection() {
      asTypedFor = view.query;
    },
    more,
    reload
  };
}
