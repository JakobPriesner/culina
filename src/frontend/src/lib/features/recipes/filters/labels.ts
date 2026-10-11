import { m } from '$shell/i18n';

import type { RecipeSort } from '../stores/libraryView.svelte';

/** Order names as a lookup: a runtime-built message key is invisible to the extractor and the compiler. */
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

export const timeLabel = (minutes: number | null): string =>
  minutes === null ? m['filters.time.any']() : m['filters.time.upTo']({ count: minutes });

export const calorieLabel = (kcal: number | null): string =>
  kcal === null ? m['filters.calories.any']() : m['filters.calories.upTo']({ count: kcal });

/** A saved search in one line, shown before saving and beside each saved search. */
export function summarise(criteria: {
  query: string;
  tags: readonly string[];
  maxMinutes: number | null;
  maxKcal?: number | null;
  sort: RecipeSort | null;
}): string {
  const parts = [
    // The words as typed, unquoted: quotation marks are language-specific punctuation.
    ...(criteria.query.trim().length > 0 ? [criteria.query.trim()] : []),
    ...criteria.tags,
    ...(criteria.maxMinutes === null ? [] : [timeLabel(criteria.maxMinutes)]),
    ...(criteria.maxKcal == null ? [] : [calorieLabel(criteria.maxKcal)]),
    ...(criteria.sort === null ? [] : [sortLabel(criteria.sort)])
  ];

  return parts.join(' · ');
}
