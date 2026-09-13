import { beforeEach, describe, expect, it, vi } from 'vitest';

import { forget, forgetEveryDraft, recall, remember } from './journal';
import type { Recipe } from '../types';

/*
 * The gap between a keystroke and a save is where work goes missing: the tab
 * closed mid-sentence, the session that expired while somebody was thinking,
 * the kitchen with no signal. This is what makes that gap survivable.
 */
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

  it('empties completely when a session ends', () => {
    remember('u1', 'r1', recipe);
    remember('u2', 'r2', recipe);
    localStorage.setItem('culina.appearance', '{}');

    forgetEveryDraft();

    expect(recall('u1', 'r1')).toBeNull();
    expect(recall('u2', 'r2')).toBeNull();
    // And leaves alone what is not a draft.
    expect(localStorage.getItem('culina.appearance')).toBe('{}');
  });

  it('treats a hand-edited value as nothing, rather than throwing on the way in', () => {
    localStorage.setItem('culina.draft.u1.r1', 'not json at all');

    expect(recall('u1', 'r1')).toBeNull();
  });

  it('survives a store that refuses to be written to', () => {
    // Private browsing, or no room left. The editor still works; it simply
    // cannot promise to survive a reload — and it says "Kept on this device"
    // only when this succeeded.
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new DOMException('QuotaExceededError');
    });

    expect(() => remember('u1', 'r1', recipe)).not.toThrow();

    vi.restoreAllMocks();
  });
});
