import { m } from '$shell/i18n';

import type { Suggestion } from './types';

/**
 * The quiet line under a suggestion's title, in the reader's language. The server sends a code and a subject, never prose.
 * Null when no single signal decided the ranking: show nothing rather than an invented reason.
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
    // The three that name something; guarded against a missing subject even though the server never sends one.
    case 'tag':
      return reason.subject ? m['suggestions.reason.tag']({ subject: reason.subject }) : null;
    case 'ingredient':
      return reason.subject
        ? m['suggestions.reason.ingredient']({ subject: reason.subject })
        : null;
    case 'household':
      return reason.subject ? m['suggestions.reason.household']({ subject: reason.subject }) : null;
    default:
      // A code this client does not know (newer server): say nothing rather than the code.
      return null;
  }
}

/**
 * Reason lines for a shortlist walked one panel at a time, null where one would repeat the panel before it:
 * the first of a run says why, the rest fall back to the fixed line. Compared as rendered lines, not codes.
 */
export function reasonLinesFor(suggestions: readonly Suggestion[]): (string | null)[] {
  const lines = suggestions.map(reasonLineFor);

  return lines.map((line, index) => (index > 0 && line === lines[index - 1] ? null : line));
}
