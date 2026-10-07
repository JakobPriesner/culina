import { untrack } from 'svelte';

import { withoutChip } from '../search/wording';
import type { SearchChip } from '../types';
import type { RecipeQuery } from '../stores/libraryView.svelte';

/**
 * The search box's text, and how it reaches the query.
 *
 * What is typed runs ahead of what has been applied: the debounce lives here
 * rather than in each page, so two boxes never wait different lengths and feel
 * like two different apps.
 */
export function createQueryBox(view: () => RecipeQuery) {
  /** What is in the box, which runs ahead of what has been applied. */
  let typed = $state(untrack(() => view().query));

  /**
   * The last thing this box put into the query.
   *
   * What tells a change made here from one made anywhere else, and the whole of
   * why the effect below can leave typing alone. Without it that effect read
   * `typed`, which made every keystroke one of its own dependencies: the box
   * was set to "o", the effect woke, found the applied query still empty, and
   * put the box back — clearing the debounce on its way. Searching the library
   * did nothing at all.
   */
  let pushed = $state(untrack(() => view().query));

  let debounce: ReturnType<typeof setTimeout> | undefined;

  // The box follows the query when something else sets it — applying a saved
  // search, or clearing everything — without fighting what is being typed.
  // `pushed`, not `typed`: see above.
  $effect(() => {
    if (view().query !== pushed) {
      clearTimeout(debounce);
      pushed = view().query;
      typed = view().query;
    }
  });

  $effect(() => () => clearTimeout(debounce));

  /** Applies a query at once, in the box and in the view. */
  function apply(next: string) {
    clearTimeout(debounce);
    typed = next;
    pushed = next;
    view().query = next;
  }

  return {
    get typed() {
      return typed;
    },

    type(value: string) {
      typed = value;
      clearTimeout(debounce);

      // Long enough that a word is finished, short enough that it feels live.
      debounce = setTimeout(() => {
        pushed = value;
        view().query = value;
      }, 250);
    },

    clear: () => apply(''),

    /** A reading removed is its characters removed from the query, applied at once. */
    removeChip: (chip: SearchChip) => apply(withoutChip(view().query, chip))
  };
}
