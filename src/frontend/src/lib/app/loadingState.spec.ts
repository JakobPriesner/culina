import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { createLoadingState, defaultTimings } from './loadingState.svelte';

/*
 * Three numbers decide whether waiting feels like progress or like a glitch.
 * Fake timers are the only way to assert them, and asserting them is the only
 * way they stay the same on every screen.
 */
beforeEach(() => vi.useFakeTimers());
afterEach(() => vi.useRealTimers());

const { delayMs, minimumMs, slowMs } = defaultTimings;

describe('a fast response', () => {
  it('never shows anything, so the app does not flicker', () => {
    const loading = createLoadingState();

    loading.start();
    vi.advanceTimersByTime(delayMs - 10);
    loading.stop();
    vi.advanceTimersByTime(1000);

    expect(loading.showing).toBe(false);
  });
});

describe('a slower response', () => {
  it('shows once the delay has passed', () => {
    const loading = createLoadingState();

    loading.start();

    expect(loading.showing).toBe(false);

    vi.advanceTimersByTime(delayMs);

    expect(loading.showing).toBe(true);
  });

  it('stays visible for the minimum, so it cannot vanish in the same glance', () => {
    const loading = createLoadingState();

    loading.start();
    vi.advanceTimersByTime(delayMs + 10);
    loading.stop();

    expect(loading.showing).toBe(true);

    vi.advanceTimersByTime(minimumMs - 20);

    expect(loading.showing).toBe(true);

    vi.advanceTimersByTime(20);

    expect(loading.showing).toBe(false);
  });

  it('hides immediately once the minimum has already passed', () => {
    const loading = createLoadingState();

    loading.start();
    vi.advanceTimersByTime(delayMs + minimumMs + 50);
    loading.stop();

    expect(loading.showing).toBe(false);
  });
});

describe('a response that never comes', () => {
  it('admits it is taking longer than usual', () => {
    const loading = createLoadingState();

    loading.start();
    vi.advanceTimersByTime(delayMs + slowMs - 10);

    expect(loading.slow).toBe(false);

    vi.advanceTimersByTime(10);

    expect(loading.slow).toBe(true);
  });

  it('stops admitting it once the work is done', () => {
    const loading = createLoadingState();

    loading.start();
    vi.advanceTimersByTime(delayMs + slowMs);
    loading.stop();
    vi.advanceTimersByTime(minimumMs);

    expect(loading.slow).toBe(false);
    expect(loading.showing).toBe(false);
  });
});

describe('a second start while already running', () => {
  it('does not restart the clock, so a refetch does not delay the skeleton twice', () => {
    const loading = createLoadingState();

    loading.start();
    vi.advanceTimersByTime(delayMs - 10);
    loading.start();
    vi.advanceTimersByTime(10);

    expect(loading.showing).toBe(true);
  });
});

describe('a component that goes away', () => {
  it('fires nothing afterwards', () => {
    const loading = createLoadingState();

    loading.start();
    loading.dispose();
    vi.advanceTimersByTime(delayMs + slowMs + minimumMs);

    expect(loading.showing).toBe(false);
    expect(loading.slow).toBe(false);
  });
});
