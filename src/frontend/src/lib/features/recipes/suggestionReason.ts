import { m } from '$shell/i18n';

import type { Suggestion } from './types';

/**
 * The one quiet line under a suggestion's title, in the reader's language.
 *
 * The server sends a code and at most a subject, never prose: the wording
 * belongs here because this is what knows which of two languages somebody
 * reads, and because a sentence on the wire cannot be translated after it
 * arrives.
 *
 * Null whenever no single signal decided the ranking. That is an ordinary
 * answer and it means show nothing — a good suggestion with no explanation is
 * fine, and an invented one is a lie that discredits the reasons that were
 * true.
 */
export function reasonLineFor(suggestion: Suggestion): string | null {
  const reason = suggestion.reason;

  if (!reason) {
    return null;
  }

  switch (reason.code) {
    case 'affinity':
      return m['suggestions.reason.affinity']();
    case 'rediscovery':
      return m['suggestions.reason.rediscovery']();
    case 'season':
      return m['suggestions.reason.season']();
    case 'slot':
      return m['suggestions.reason.slot']();
    case 'fresh':
      return m['suggestions.reason.fresh']();
    case 'similar':
      return m['suggestions.reason.similar']();
    // The three that name something. The server already refuses to send one of
    // these without a subject, and the guard is here anyway: "You often cook"
    // with a hole in it is worse than no line at all, and a contract is a thing
    // that can change.
    case 'tag':
      return reason.subject ? m['suggestions.reason.tag']({ subject: reason.subject }) : null;
    case 'ingredient':
      return reason.subject
        ? m['suggestions.reason.ingredient']({ subject: reason.subject })
        : null;
    case 'household':
      return reason.subject ? m['suggestions.reason.household']({ subject: reason.subject }) : null;
    default:
      // A code this client does not know yet. Newer server, older app: say
      // nothing rather than the code.
      return null;
  }
}

/**
 * The reason lines for a shortlist walked one panel at a time, each null where
 * it would only repeat the panel before it.
 *
 * Every repeat is still true — a household that cooks its favourites really
 * does get "one you keep coming back to" five times. But read in a row, five
 * copies of the same fact stop reading as a fact and start reading as
 * boilerplate, which undoes what a reason is for. So the set is explained
 * rather than each member of it: the first of a run says why, and the rest fall
 * back to the panel's fixed line.
 *
 * Compared as rendered lines rather than codes, so "you often cook soup" and
 * "you often cook curry" are two different things to say, which they are.
 */
export function reasonLinesFor(suggestions: readonly Suggestion[]): (string | null)[] {
  const lines = suggestions.map(reasonLineFor);

  return lines.map((line, index) => (index > 0 && line === lines[index - 1] ? null : line));
}
