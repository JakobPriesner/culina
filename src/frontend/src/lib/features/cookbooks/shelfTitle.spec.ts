import { describe, expect, it } from 'vitest';

import { shelfTitle } from './shelfTitle';

describe('a cookbook name on screen', () => {
  it('writes an import date the way the reader writes dates', () => {
    expect(shelfTitle('culina.example.org · 2026-10-27')).toMatch(
      /^culina\.example\.org · .*2026$/
    );
    expect(shelfTitle('culina.example.org · 2026-10-27')).not.toContain('2026-10-27');
  });

  it('leaves every other name as it was written', () => {
    expect(shelfTitle('Sonntagsessen')).toBe('Sonntagsessen');
    expect(shelfTitle('Backen · Brot')).toBe('Backen · Brot');
  });
});
