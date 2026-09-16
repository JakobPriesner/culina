import { beforeEach, describe, expect, it, vi } from 'vitest';

import { forgetLastDraft, recallLastDraft, rememberLastDraft } from './lastDraft';

describe('the last-started-draft pointer', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('gives back which recipe was last started', () => {
    rememberLastDraft('u1', 'h1', 'r1', 'Half a thought');

    expect(recallLastDraft('u1', 'h1')).toEqual({ recipeId: 'r1', title: 'Half a thought' });
  });

  it('keeps one household out of another one’s draft on a shared account', () => {
    rememberLastDraft('u1', 'h1', 'r1', 'Half a thought');

    expect(recallLastDraft('u1', 'h2')).toBeNull();
  });

  it('keeps one person out of another one’s draft on a shared device', () => {
    rememberLastDraft('u1', 'h1', 'r1', 'Half a thought');

    expect(recallLastDraft('u2', 'h1')).toBeNull();
  });

  it('has nothing to give back once forgotten', () => {
    rememberLastDraft('u1', 'h1', 'r1', 'Half a thought');
    forgetLastDraft('u1', 'h1');

    expect(recallLastDraft('u1', 'h1')).toBeNull();
  });

  it('treats a hand-edited value as nothing, rather than throwing on the way in', () => {
    localStorage.setItem('culina.lastDraft.u1.h1', 'not json at all');

    expect(recallLastDraft('u1', 'h1')).toBeNull();
  });

  it('survives a store that refuses to be written to', () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new DOMException('QuotaExceededError');
    });

    expect(() => rememberLastDraft('u1', 'h1', 'r1', 'Half a thought')).not.toThrow();

    vi.restoreAllMocks();
  });
});
