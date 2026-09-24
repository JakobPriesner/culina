import { m } from '$shell/i18n';

import type { MatchReason, SearchChip } from '../types';

/**
 * How the server's readings of a query are worded, in the reader's language.
 *
 * The server sends a kind, a stable value and the characters it read them
 * from, never a sentence: which of two languages somebody reads is known here
 * and nowhere else. A value this client has no words for is shown as it was
 * typed, which is always a true thing to say.
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

/** A cuisine facet or chip value, worded. */
export function cuisineLabel(value: string): string {
  return keyed(`search.chip.cuisine.${value}`) ?? value;
}

/** The quiet line under a result that says why it is there. */
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

/**
 * The query with one chip taken out: its characters deleted, and the space
 * either side of them folded into one.
 *
 * This is the whole of removing a chip. The server returns where each reading
 * came from, so there is one parser and it is on the server; the client only
 * ever edits the string it would have typed.
 */
export function withoutChip(query: string, chip: SearchChip): string {
  const before = query.slice(0, chip.start).trimEnd();
  const after = query.slice(chip.end).trimStart();

  return [before, after].filter((part) => part.length > 0).join(' ');
}

/**
 * Messages reached by a key built at run time.
 *
 * A key that does not exist is `undefined` on Paraglide's `m` rather than an
 * error, and calling it would throw — so it is looked up and checked, never
 * called blind.
 */
function keyed(key: string): string | null {
  const message = (m as unknown as Record<string, (() => string) | undefined>)[key];

  return typeof message === 'function' ? message() : null;
}
