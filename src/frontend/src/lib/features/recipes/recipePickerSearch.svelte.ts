import { createRecipeStore } from './stores/recipes.svelte';
import { suggestions } from './stores/suggestions.svelte';
import { withoutChip } from './search/wording';
import type { SearchChip } from './types';

/** Read afresh each time so the search stays reactive. */
interface Source {
  readonly open: () => boolean;
  readonly householdId: () => string;
  readonly cookbookId: () => string | undefined;
  readonly suggestFor: () => 'breakfast' | 'lunch' | 'dinner' | undefined;
  readonly taken: () => readonly string[];
}

/** The picker's own search list, not the shared store: searching into that would change the page behind the sheet. */
export function createPickerSearch(source: Source) {
  const recipes = createRecipeStore();

  let typed = $state('');
  let query = $state('');
  /** The query whose server correction the reader declined. */
  let asTypedFor = $state<string | null>(null);

  let debounce: ReturnType<typeof setTimeout> | undefined;

  /** Suggesting, not searching: only before typing and across the whole collection, since a cookbook keeps its own order. */
  const suggesting = $derived(
    source.suggestFor() !== undefined &&
      query.trim().length === 0 &&
      source.cookbookId() === undefined
  );

  const occasion = $derived({ slot: source.suggestFor(), exclude: source.taken(), limit: 5 });

  const shown = $derived(
    suggesting ? suggestions.for(source.householdId(), occasion) : recipes.items
  );

  // Only while open: a closed sheet must not keep a search warm.
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

  // Reset on close so it reopens on everything.
  $effect(() => {
    if (!source.open()) {
      clearTimeout(debounce);
      typed = '';
      query = '';
      asTypedFor = null;
    }
  });

  $effect(() => () => clearTimeout(debounce));

  /** The server's reading of the words and its corrections, shown as the same chips and notices as every other search. */
  const interpretation = $derived(
    !suggesting && query.trim().length > 0 && recipes.status === 'ready'
      ? recipes.interpretation
      : null
  );

  /** "Nothing matched" only once an answer has arrived; the store keeps old results during a search, so earlier it would flash falsely. */
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

      // Long enough that a word is finished, short enough to feel live.
      debounce = setTimeout(() => (query = value), 250);
    },

    /** Removing a reading removes its characters from the query at once. */
    remove(chip: SearchChip) {
      clearTimeout(debounce);
      typed = withoutChip(query, chip);
      query = typed;
    },

    keepAsTyped() {
      asTypedFor = query;
    }
  };
}
