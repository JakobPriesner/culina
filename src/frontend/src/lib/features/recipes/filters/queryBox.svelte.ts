import { untrack } from 'svelte';

import { withoutChip } from '../search/wording';
import type { SearchChip } from '../types';
import type { RecipeQuery } from '../stores/libraryView.svelte';

/** The search box's text and its debounced path into the query. */
export function createQueryBox(view: () => RecipeQuery) {
  let typed = $state(untrack(() => view().query));

  /**
   * The last value this box put into the query, so the effect below can tell outside changes from
   * typing. Reading `typed` there reset the box on every keystroke.
   */
  let pushed = $state(untrack(() => view().query));

  let debounce: ReturnType<typeof setTimeout> | undefined;

  // Follow outside changes (saved search, clear all) without fighting typing.
  $effect(() => {
    if (view().query !== pushed) {
      clearTimeout(debounce);
      pushed = view().query;
      typed = view().query;
    }
  });

  $effect(() => () => clearTimeout(debounce));

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

      debounce = setTimeout(() => {
        pushed = value;
        view().query = value;
      }, 250);
    },

    clear: () => apply(''),

    removeChip: (chip: SearchChip) => apply(withoutChip(view().query, chip))
  };
}
