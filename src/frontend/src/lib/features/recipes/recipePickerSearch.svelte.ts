import { createRecipeStore } from './stores/recipes.svelte';
import { suggestions } from './stores/suggestions.svelte';
import { withoutChip } from './search/wording';
import type { SearchChip } from './types';

/** What the picker's search needs to know, read afresh each time so it stays reactive. */
interface Source {
  readonly open: () => boolean;
  readonly householdId: () => string;
  readonly cookbookId: () => string | undefined;
  readonly suggestFor: () => 'breakfast' | 'lunch' | 'dinner' | undefined;
  /** Recipes the caller has already taken, which are not suggested again. */
  readonly taken: () => readonly string[];
}

/**
 * The search behind the picker: what is typed, what has been asked, and what
 * is shown for it.
 *
 * Its own list, not the app's. The shared store is what the page behind the
 * sheet is drawing from, and a picker that searched into it would replace that
 * page's contents with whatever was typed here — the cookbook page would empty
 * out behind an open sheet, and the collection would still be filtered after
 * the sheet closed.
 */
export function createPickerSearch(source: Source) {
  const recipes = createRecipeStore();

  /** What is in the box, which is not yet what has been searched for. */
  let typed = $state('');
  let query = $state('');
  /** The query whose correction the reader turned down. */
  let asTypedFor = $state<string | null>(null);

  let debounce: ReturnType<typeof setTimeout> | undefined;

  /**
   * Whether the sheet is answering rather than searching.
   *
   * Only before anything is typed, and only inside the whole collection: a
   * suggestion ranks the library, and a cookbook is somebody's curation of it
   * whose own order is the one they built.
   */
  const suggesting = $derived(
    source.suggestFor() !== undefined &&
      query.trim().length === 0 &&
      source.cookbookId() === undefined
  );

  /** What is already on this week, so nothing is offered twice. */
  const occasion = $derived({ slot: source.suggestFor(), exclude: source.taken(), limit: 5 });

  const shown = $derived(
    suggesting ? suggestions.for(source.householdId(), occasion) : recipes.items
  );

  // Only while it is open: a closed sheet that keeps a search warm is a request
  // nobody asked for, on every page that happens to mount one.
  $effect(() => {
    if (source.open() && !suggesting) {
      void recipes.list(source.householdId(), {
        query,
        cookbookId: source.cookbookId(),
        asTyped: asTypedFor !== null && asTypedFor === query
      });
    }
  });

  $effect(() => {
    if (source.open() && suggesting) {
      void suggestions.ask(source.householdId(), occasion);
    }
  });

  // Forgotten on the way out, so it opens on everything next time rather than
  // on whatever somebody was looking for last week.
  $effect(() => {
    if (!source.open()) {
      clearTimeout(debounce);
      typed = '';
      query = '';
      asTypedFor = null;
    }
  });

  $effect(() => () => clearTimeout(debounce));

  /**
   * What the server read the words to mean, and what it had to change to
   * find anything — the same chips and notices as every other search, so
   * "vegetarisch Donnerstag" reads the same way in the planner as in the
   * library.
   */
  const interpretation = $derived(
    !suggesting && query.trim().length > 0 && recipes.status === 'ready'
      ? recipes.interpretation
      : null
  );

  /**
   * Whether to say that nothing matched.
   *
   * Only once an answer has arrived. While a search is in flight the previous
   * results are still on screen — the store keeps them deliberately — and
   * flashing "nothing matched" between two keystrokes says the opposite of
   * what is true.
   */
  const nothing = $derived(
    suggesting
      ? suggestions.statusOf(source.householdId(), occasion) === 'ready' && shown.length === 0
      : recipes.status === 'ready' && recipes.items.length === 0
  );

  return {
    get typed() {
      return typed;
    },
    get query() {
      return query;
    },
    get shown() {
      return shown;
    },
    get nothing() {
      return nothing;
    },
    get interpretation() {
      return interpretation;
    },
    get total() {
      return recipes.total;
    },

    type(value: string) {
      typed = value;
      clearTimeout(debounce);

      // Long enough that a word is finished, short enough that it feels live.
      debounce = setTimeout(() => (query = value), 250);
    },

    /** A reading removed is its characters removed from the query, at once. */
    remove(chip: SearchChip) {
      clearTimeout(debounce);
      typed = withoutChip(query, chip);
      query = typed;
    },

    /** Turns down the correction offered for the query on screen. */
    keepAsTyped() {
      asTypedFor = query;
    }
  };
}
