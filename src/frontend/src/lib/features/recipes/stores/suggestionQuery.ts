import type { LoadStatus } from '$shell/stores';

import type { Suggestion } from '../types';

export interface SuggestionQuery {
  readonly slot?: 'breakfast' | 'lunch' | 'dinner';
  /** A ceiling on total time, honoured exactly. */
  readonly maxMinutes?: number;
  readonly exclude?: readonly string[];
  readonly limit?: number;
}

export function keyOf(householdId: string, query: SuggestionQuery): string {
  return [
    householdId,
    query.slot ?? '',
    query.maxMinutes ?? '',
    (query.exclude ?? []).join(','),
    query.limit ?? ''
  ].join('|');
}

export const DefaultLimit = 5;

/** Paging cap: the next page names everything already shown, so the request line grows with the list. */
export const Most = 60;

/** LRU size: every plan change is a new question, but only the one on screen is read. */
export const AnswersKept = 20;

export interface Answer {
  readonly items: Suggestion[];
  readonly status: LoadStatus;
  readonly more: boolean;
}
