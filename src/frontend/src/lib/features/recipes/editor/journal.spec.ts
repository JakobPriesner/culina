import { beforeEach, describe, expect, it, vi } from 'vitest';

import { forget, forgetEveryDraft, recall, remember } from './journal';
import type { Recipe } from '../types';

/* The gap between a keystroke and a save is where work goes missing (closed tab, expired session, no signal); this makes it survivable. */
const recipe = { id: 'r1', title: 'Half a thought' } as unknown as Recipe;

describe('the editor journal', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('gives back exactly what was written', () => {
    remember('u1', 'r1', recipe);

    expect(recall('u1', 'r1')?.recipe).toEqual(recipe);
  });

  it('says when it was written, so the app can say how old it is', () => {
    remember('u1', 'r1', recipe);

    expect(Date.parse(recall('u1', 'r1')!.at)).not.toBeNaN();
  });

  it('keeps one person out of another one’s draft on a shared device', () => {
    remember('u1', 'r1', recipe);

    expect(recall('u2', 'r1')).toBeNull();
  });

  it('has nothing to give back once the server has it', () => {
    remember('u1', 'r1', recipe);
    forget('u1', 'r1');

    expect(recall('u1', 'r1')).toBeNull();
  });

  it('empties completely when somebody signs out', () => {
    remember('u1', 'r1', recipe);
    remember('u2', 'r2', recipe);
    localStorage.setItem('culina.appearance', '{}');

    forgetEveryDraft();

    expect(recall('u1', 'r1')).toBeNull();
    expect(recall('u2', 'r2')).toBeNull();
    expect(localStorage.getItem('culina.appearance')).toBe('{}');
  });

  it('can leave one person’s drafts in place while removing everyone else’s', () => {
    remember('u1', 'r1', recipe);
    remember('u10', 'r2', recipe);
    remember('u2', 'r3', recipe);

    forgetEveryDraft('u1');

    expect(recall('u1', 'r1')?.recipe).toEqual(recipe);
    // A user id that merely starts with the kept one is somebody else.
    expect(recall('u10', 'r2')).toBeNull();
    expect(recall('u2', 'r3')).toBeNull();
  });

  it('treats a hand-edited value as nothing, rather than throwing on the way in', () => {
    localStorage.setItem('culina.draft.u1.r1', 'not json at all');

    expect(recall('u1', 'r1')).toBeNull();
  });

  it('survives a store that refuses to be written to', () => {
    // Private browsing or no room: the editor still works but cannot promise to survive a reload, and says "Kept on this device" only on success.
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new DOMException('QuotaExceededError');
    });

    expect(() => remember('u1', 'r1', recipe)).not.toThrow();

    vi.restoreAllMocks();
  });
});
