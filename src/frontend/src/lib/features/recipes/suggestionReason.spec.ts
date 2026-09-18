import { describe, expect, it } from 'vitest';

import { reasonLineFor } from './suggestionReason';
import type { Suggestion, SuggestionReason } from './types';

/*
 * A reason is a fact about the ranking, rendered in the reader's language. The
 * server sends a code and at most a subject and never prose, so this is where
 * the sentence is decided — and where "say nothing" has to stay a first-class
 * answer rather than a gap somebody fills later.
 */
const suggestion = (reason: SuggestionReason | null): Suggestion => ({
  id: 'r1',
  title: 'Linsensuppe',
  imageId: null,
  totalMinutes: 40,
  yieldAmount: 4,
  yieldKind: 'servings',
  tags: [],
  cookCount: 3,
  lastCookedAt: null,
  updatedAt: '2026-09-18T00:00:00Z',
  match: null,
  reason
});

describe('reasonLineFor', () => {
  it('says nothing when no single signal decided the ranking', () => {
    // Not a gap. A good suggestion with no explanation is fine; an invented one
    // is a lie, and catching it once discredits every reason that was true.
    expect(reasonLineFor(suggestion(null))).toBeNull();
  });

  it('writes the subject into the line for the reasons that name something', () => {
    const line = reasonLineFor(suggestion({ code: 'ingredient', subject: 'Aubergine' }));

    expect(line).toContain('Aubergine');
  });

  it('names the person for a household reason, never an id', () => {
    const line = reasonLineFor(suggestion({ code: 'household', subject: 'Anna' }));

    expect(line).toContain('Anna');
    expect(line).not.toContain('r1');
  });

  it('says nothing when a reason that needs a subject has none', () => {
    // The server already refuses to send these, and the guard is here anyway:
    // "You often cook ___" with a hole in it is worse than no line at all, and
    // a contract is a thing that can change.
    for (const code of ['tag', 'ingredient', 'household'] as const) {
      expect(reasonLineFor(suggestion({ code, subject: null }))).toBeNull();
    }
  });

  it('answers the reasons that explain themselves without a subject', () => {
    for (const code of ['affinity', 'rediscovery', 'season', 'slot', 'fresh', 'similar'] as const) {
      expect(reasonLineFor(suggestion({ code, subject: null }))).not.toBeNull();
    }
  });

  it('says nothing for a code it does not know', () => {
    // A newer server and an older app. Saying nothing beats printing the code.
    const unknown = { code: 'telepathy', subject: null } as unknown as SuggestionReason;

    expect(reasonLineFor(suggestion(unknown))).toBeNull();
  });
});
