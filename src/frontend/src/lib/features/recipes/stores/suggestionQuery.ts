import type { LoadStatus } from '$shell/stores';

import type { Suggestion } from '../types';

export interface SuggestionQuery {
  /** Which meal, when the caller knows. The plan always does. */
  readonly slot?: 'breakfast' | 'lunch' | 'dinner';
  /** A ceiling on total time. Honoured exactly, never treated as a preference. */
  readonly maxMinutes?: number;
  /** What the caller already has on screen or already planned. */
  readonly exclude?: readonly string[];
  readonly limit?: number;
}

/** One question, as a key, so the same one is never asked twice. */
export function keyOf(householdId: string, query: SuggestionQuery): string {
  return [
    householdId,
    query.slot ?? '',
    query.maxMinutes ?? '',
    (query.exclude ?? []).join(','),
    query.limit ?? ''
  ].join('|');
}

/** How many the server answers with when nobody says. */
export const DefaultLimit = 5;

/**
 * The most one question is walked to.
 *
 * The next page is asked for by naming everything already shown, so the list
 * cannot grow without the address growing with it. Sixty is twelve pages of
 * five and well inside what a request line may carry — and far past the point
 * where the answer to "what should I cook?" is still a shortlist.
 */
export const Most = 60;

/**
 * How many questions are remembered at once.
 *
 * Every change to the week plan is a new question, since what is already
 * planned is named in it, and answers are only ever read for the one being
 * looked at. The least recently used one is forgotten, and asked again if it
 * comes back.
 */
export const AnswersKept = 20;

/** One question's answer, and how far along it is. */
export interface Answer {
  readonly items: Suggestion[];
  readonly status: LoadStatus;
  /** Whether asking again, past what is shown, may find more. */
  readonly more: boolean;
}
