import { m } from '$shell/i18n';

import type { RecipeSort } from '../stores/libraryView.svelte';

/**
 * The words an order is called by.
 *
 * A lookup rather than a message key built from the value, because a key
 * assembled at runtime is one the message extractor cannot see and the compiler
 * cannot check.
 */
export function sortLabel(sort: RecipeSort): string {
  switch (sort) {
    case 'relevance':
      return m['filters.sort.relevance']();
    case 'suggested':
      return m['filters.sort.suggested']();
    case 'title':
      return m['filters.sort.title']();
    case 'quickest':
      return m['filters.sort.quickest']();
    case 'mostCooked':
      return m['filters.sort.mostCooked']();
    case 'shelf':
      return m['filters.sort.shelf']();
    default:
      return m['filters.sort.recent']();
  }
}

/** "Up to 30 min", or the words for no ceiling at all. */
export const timeLabel = (minutes: number | null): string =>
  minutes === null ? m['filters.time.any']() : m['filters.time.upTo']({ count: minutes });

/**
 * What a saved search remembers, in one line.
 *
 * Shown before saving and beside each saved search, so that "Quick dinners"
 * never has to be opened to find out what it actually asks for.
 */
export function summarise(criteria: {
  query: string;
  tags: readonly string[];
  maxMinutes: number | null;
  sort: RecipeSort | null;
}): string {
  const parts = [
    // The words as typed, unquoted: a quotation mark is punctuation, and
    // punctuation differs by language the same way words do.
    ...(criteria.query.trim().length > 0 ? [criteria.query.trim()] : []),
    ...criteria.tags,
    ...(criteria.maxMinutes === null ? [] : [timeLabel(criteria.maxMinutes)]),
    ...(criteria.sort === null ? [] : [sortLabel(criteria.sort)])
  ];

  return parts.join(' · ');
}
