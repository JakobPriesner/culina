import { describe, expect, it } from 'vitest';

import { reasonLineFor, reasonLinesFor } from './suggestionReason';
import type { Suggestion, SuggestionReason } from './types';

/* A reason is rendered from a code and at most a subject, never prose, so the sentence is decided here; "say nothing" must stay a first-class answer. */
const suggestion = (reason: SuggestionReason | null): Suggestion => ({
  id: 'r1',
  title: 'Linsensuppe',
  imageId: null,
  totalMinutes: 40,
  yieldAmount: 4,
  yieldKind: 'servings',
  yieldLabel: null,
  tags: [],
  cookCount: 3,
  lastCookedAt: null,
  updatedAt: '2026-09-18T00:00:00Z',
  match: null,
  reason
});

describe('reasonLineFor', () => {
  it('says nothing when no single signal decided the ranking', () => {
    // Not a gap: a good suggestion with no explanation is fine, an invented one discredits every true reason.
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
    // The server already refuses to send these; the guard stays since "You often cook ___" with a hole is worse than no line and contracts change.
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

describe('reasonLinesFor', () => {
  const affinity = suggestion({ code: 'affinity', subject: null });

  it('says a run of the same reason once, at the start of the run', () => {
    // Five copies of one fact read as boilerplate: the first says why, the rest fall back to the panel's fixed line.
    const lines = reasonLinesFor([affinity, affinity, affinity]);

    expect(lines[0]).toBe(reasonLineFor(affinity));
    expect(lines.slice(1)).toEqual([null, null]);
  });

  it('says a reason again once something else came between', () => {
    const fresh = suggestion({ code: 'fresh', subject: null });

    const lines = reasonLinesFor([affinity, fresh, affinity]);

    expect(lines).toEqual([reasonLineFor(affinity), reasonLineFor(fresh), reasonLineFor(affinity)]);
  });

  it('treats the same reason about two different things as two reasons', () => {
    const soup = suggestion({ code: 'tag', subject: 'Suppe' });
    const curry = suggestion({ code: 'tag', subject: 'Curry' });

    expect(reasonLinesFor([soup, curry])).toEqual([reasonLineFor(soup), reasonLineFor(curry)]);
  });
});
