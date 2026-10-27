import { describe, expect, it } from 'vitest';

import { followed, releasesSheet } from './sheetDrag';

describe('letting go of a pulled sheet', () => {
  it('closes it after a flick, however short', () => {
    expect(releasesSheet(40, 700, 0.8)).toBe(true);
  });

  it('closes it once pulled a third of the way down', () => {
    expect(releasesSheet(240, 700, 0.1)).toBe(true);
  });

  it('settles it back after a slow, short pull', () => {
    expect(releasesSheet(120, 700, 0.1)).toBe(false);
  });

  it('never closes it on a push upward, however fast', () => {
    expect(releasesSheet(0, 700, 2)).toBe(false);
    expect(releasesSheet(-30, 700, 2)).toBe(false);
  });
});

describe('following a pull', () => {
  it('follows the finger down and resists it up', () => {
    expect(followed(60)).toBe(60);
    expect(followed(-80)).toBe(-10);
  });
});
