import { m } from '$shell/i18n';

import type { MatchReason, SearchChip } from '../types';

/**
 * Wording of the server's query readings in the reader's language; the server sends kinds and values,
 * not sentences. A value with no words here is shown as typed.
 */
export function chipLabel(chip: SearchChip): string {
  switch (chip.kind) {
    case 'time':
      return m['search.chip.time']({ minutes: chip.value });
    case 'quick':
      return m['search.chip.quick']();
    case 'ingredient':
      return m['search.chip.ingredient']({ word: chip.word ?? chip.text });
    case 'exclusion':
      return m['search.chip.exclusion']({ word: chip.word ?? chip.text });
    default:
      return keyed(`search.chip.${chip.kind}.${chip.value}`) ?? chip.text;
  }
}

export function cuisineLabel(value: string): string {
  return keyed(`search.chip.cuisine.${value}`) ?? value;
}

/** The quiet line under a result saying why it is there. */
export function reasonLine(reason: MatchReason): string {
  switch (reason.kind) {
    case 'ingredient':
      return m['search.reason.ingredient']({ term: reason.term ?? '' });
    case 'tag':
      return m['search.reason.tag']({ term: reason.term ?? '' });
    case 'concept':
      return m['search.reason.concept']({ term: reason.term ?? '' });
    default:
      return m['search.reason.text']();
  }
}

/** The query with one chip's characters deleted and surrounding spaces folded; the server returns where each reading came from, so only it parses. */
export function withoutChip(query: string, chip: SearchChip): string {
  const before = query.slice(0, chip.start).trimEnd();
  const after = query.slice(chip.end).trimStart();

  return [before, after].filter((part) => part.length > 0).join(' ');
}

/** Messages by a runtime key: a missing key is `undefined` on Paraglide's `m`, so look up and check, never call blind. */
function keyed(key: string): string | null {
  const message = (m as unknown as Record<string, (() => string) | undefined>)[key];

  return typeof message === 'function' ? message() : null;
}
