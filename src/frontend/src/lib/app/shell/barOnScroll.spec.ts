import { describe, expect, it } from 'vitest';

import { barAfterScroll, nearTop, shown, travel, type BarScroll } from './barOnScroll';

/** Scrolls through the positions in order, as a reader's thumb would, and says whether the bar ends compact. */
const after = (positions: number[], atEnd = false, from: BarScroll = shown): BarScroll =>
  positions.reduce(
    (bar, y, index) => barAfterScroll(bar, y, atEnd && index === positions.length - 1),
    from
  );

describe('the bottom bar while a page scrolls', () => {
  it('turns compact once the reader goes down the page', () => {
    expect(after([100, 300, 600]).compact).toBe(true);
  });

  it('stays for a small movement, so a resting thumb does not flicker it', () => {
    expect(after([200 + travel], false, { compact: false, anchor: 200 }).compact).toBe(false);
    expect(after([200, 600, 600 - travel]).compact).toBe(true);
  });

  it('comes back on a deliberate scroll up, measured from the lowest point reached', () => {
    expect(after([200, 900, 1500, 1500 - travel - 1]).compact).toBe(false);
  });

  it('measures the next scroll down from the highest point it was shown at', () => {
    const back = after([200, 900, 600]);

    expect(back.compact).toBe(false);
    expect(barAfterScroll(back, 600 + travel - 1, false).compact).toBe(false);
    expect(barAfterScroll(back, 600 + travel + 1, false).compact).toBe(true);
  });

  it('stays for a scroll the page makes, such as focus moving down a form', () => {
    expect(barAfterScroll(shown, 900, false, false)).toEqual({ compact: false, anchor: 900 });
  });

  it('still comes back when the page scrolls up on its own', () => {
    const compact = after([200, 900]);

    expect(barAfterScroll(compact, 300, false, false).compact).toBe(false);
  });

  it('is full near the top and at the end of the page', () => {
    expect(after([200, 900, nearTop]).compact).toBe(false);
    expect(after([200, 900, 1500], true).compact).toBe(false);
  });
});
