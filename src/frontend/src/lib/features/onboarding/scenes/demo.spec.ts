import { afterEach, describe, expect, it, vi } from 'vitest';

import { play, typing } from './demo';

describe('play', () => {
  afterEach(() => vi.useRealTimers());

  it('runs every beat at once where nothing should move, so the end state shows', () => {
    const seen: number[] = [];

    play(
      [
        [500, () => seen.push(1)],
        [900, () => seen.push(2)]
      ],
      false
    );

    expect(seen).toEqual([1, 2]);
  });

  it('runs beats at their times, and none after it is stopped', () => {
    vi.useFakeTimers();
    const seen: number[] = [];
    const stop = play(
      [
        [100, () => seen.push(1)],
        [300, () => seen.push(2)]
      ],
      true
    );

    vi.advanceTimersByTime(150);
    stop();
    vi.advanceTimersByTime(500);

    expect(seen).toEqual([1]);
  });
});

describe('typing', () => {
  it('shows the text growing a letter at a time and says when it is done', () => {
    const shown: string[] = [];
    const { beats, end } = typing('Die', 100, (typed) => shown.push(typed), 10);

    beats.forEach(([, run]) => run());

    expect(shown).toEqual(['D', 'Di', 'Die']);
    expect(beats.map(([at]) => at)).toEqual([100, 110, 120]);
    expect(end).toBe(130);
  });
});
