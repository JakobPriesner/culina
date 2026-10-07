import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { createTimers } from './timers.svelte';

/* Timers are wall-clock deadlines, not ticked durations, so they keep counting while the app is closed. */
const sessionId = 'session-1';
const timers = () => createTimers(() => sessionId);

beforeEach(() => {
  localStorage.clear();
  vi.useFakeTimers();
  vi.setSystemTime(new Date('2026-09-12T12:00:00Z'));
});

afterEach(() => vi.useRealTimers());

describe('starting a timer', () => {
  it('counts down', () => {
    const kitchen = timers();
    const stop = kitchen.tick();

    kitchen.start(0, 600, 'Step 1');

    expect(kitchen.remaining(kitchen.timers[0]!)).toBe(600);

    vi.advanceTimersByTime(60_000);

    expect(kitchen.remaining(kitchen.timers[0]!)).toBe(540);

    stop();
  });

  it('replaces the one already on that step rather than stacking', () => {
    const kitchen = timers();

    kitchen.start(0, 600, 'Step 1');
    kitchen.start(0, 300, 'Step 1');

    expect(kitchen.timers).toHaveLength(1);
  });

  it('never counts past zero', () => {
    const kitchen = timers();
    const stop = kitchen.tick();

    kitchen.start(0, 60, 'Step 1');
    vi.advanceTimersByTime(120_000);

    expect(kitchen.remaining(kitchen.timers[0]!)).toBe(0);
    expect(kitchen.isDone(kitchen.timers[0]!)).toBe(true);

    stop();
  });
});

describe('coming back to a closed app', () => {
  it('still knows how long is left, because the deadline is absolute', () => {
    const first = timers();

    first.start(0, 600, 'Step 1');

    vi.advanceTimersByTime(240_000);

    const second = timers();

    second.load();
    second.tick()();

    expect(second.remaining(second.timers[0]!)).toBe(360);
  });

  it('survives a storage that refuses to answer', () => {
    const kitchen = timers();

    kitchen.start(0, 600, 'Step 1');
    localStorage.setItem(`culina.timers.${sessionId}`, 'not json');

    const second = timers();

    second.load();

    expect(second.timers).toEqual([]);
  });
});

describe('finishing', () => {
  it('leaves nothing behind for the next session', () => {
    const kitchen = timers();

    kitchen.start(0, 600, 'Step 1');
    kitchen.clear();

    expect(kitchen.timers).toEqual([]);
    expect(localStorage.getItem(`culina.timers.${sessionId}`)).toBeNull();
  });
});

describe('a phone that was in a pocket', () => {
  it('shows the right number the instant it is looked at again', () => {
    // Backgrounded tabs throttle or stop intervals; waiting for the next tick would show a stale number on unlocking.
    const kitchen = timers();
    const stop = kitchen.tick();

    kitchen.start(0, 600, 'Simmer');

    vi.setSystemTime(new Date('2026-09-12T12:05:00Z'));

    document.dispatchEvent(new Event('visibilitychange'));

    expect(kitchen.remaining(kitchen.timers[0]!)).toBe(300);

    stop();
  });

  it('stops listening once the cooking screen is gone', () => {
    const kitchen = timers();
    const stop = kitchen.tick();

    kitchen.start(0, 600, 'Simmer');
    stop();

    vi.setSystemTime(new Date('2026-09-12T12:05:00Z'));
    document.dispatchEvent(new Event('visibilitychange'));

    expect(kitchen.remaining(kitchen.timers[0]!)).toBe(600);
  });
});

describe('when a timer expires', () => {
  it('signals completion when the deadline passes', () => {
    const kitchen = timers();
    const stop = kitchen.tick();

    kitchen.start(0, 5, 'Simmer');
    expect(kitchen.isDone(kitchen.timers[0]!)).toBe(false);

    vi.advanceTimersByTime(6000);

    expect(kitchen.isDone(kitchen.timers[0]!)).toBe(true);

    stop();
  });
});
