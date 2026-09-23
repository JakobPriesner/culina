import { afterEach, describe, expect, it, vi } from 'vitest';

import { recallRanking, rememberRanking } from './rankingHint';

afterEach(() => {
  localStorage.clear();
  vi.restoreAllMocks();
});

describe('the remembered ranking answer', () => {
  it('is unknown on a device that has never been told', () => {
    // Unknown, not "no": a page that read this as "no" would list in recent
    // order and never wait for a kitchen that does have history.
    expect(recallRanking('kitchen')).toBeNull();
  });

  it('comes back as it was left, either way', () => {
    rememberRanking('kitchen', true);
    expect(recallRanking('kitchen')).toBe(true);

    rememberRanking('kitchen', false);
    expect(recallRanking('kitchen')).toBe(false);
  });

  it('is kept apart for each household', () => {
    // History in one kitchen says nothing about the other.
    rememberRanking('home', true);
    rememberRanking('holiday-flat', false);

    expect(recallRanking('home')).toBe(true);
    expect(recallRanking('holiday-flat')).toBe(false);
  });

  it('is unknown rather than broken when storage refuses to be read', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new DOMException('blocked', 'SecurityError');
    });

    expect(recallRanking('kitchen')).toBeNull();
  });

  it('is simply not kept when storage refuses to be written', () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new DOMException('full', 'QuotaExceededError');
    });

    expect(() => rememberRanking('kitchen', true)).not.toThrow();
  });
});
