import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { toaster } from './toaster.svelte';

/*
 * Undo is what lets Culina skip confirmation dialogs, so the undo path is the
 * part that has to be right: it must run, it must run once, and the message
 * must survive long enough to be reached.
 */
beforeEach(() => {
  vi.useFakeTimers();
  toaster.reset();
});

afterEach(() => vi.useRealTimers());

describe('a message', () => {
  it('appears and then goes away on its own', () => {
    toaster.show({ message: 'Recipe deleted' });

    expect(toaster.toasts).toHaveLength(1);

    vi.advanceTimersByTime(6000);

    expect(toaster.toasts).toHaveLength(0);
  });

  it('stays until dismissed when it is given no duration', () => {
    toaster.show({ message: 'Sync failed', durationMs: 0 });

    vi.advanceTimersByTime(60_000);

    expect(toaster.toasts).toHaveLength(1);
  });

  it('keeps the newest when too many stack up', () => {
    for (const message of ['first', 'second', 'third', 'fourth']) {
      toaster.show({ message });
    }

    expect(toaster.toasts.map((toast) => toast.message)).toEqual(['second', 'third', 'fourth']);
  });
});

describe('undo', () => {
  it('runs the action and takes the message away', () => {
    const restore = vi.fn();

    const id = toaster.show({
      message: 'Recipe deleted',
      action: { label: 'Undo', run: restore }
    });

    toaster.act(id);

    expect(restore).toHaveBeenCalledOnce();
    expect(toaster.toasts).toHaveLength(0);
  });

  it('cannot be pressed twice, so an undo cannot be undone', () => {
    const restore = vi.fn();

    const id = toaster.show({ message: 'Deleted', action: { label: 'Undo', run: restore } });

    toaster.act(id);
    toaster.act(id);

    expect(restore).toHaveBeenCalledOnce();
  });

  it('is still there while someone is reading the message', () => {
    const id = toaster.show({ message: 'Recipe deleted', action: { label: 'Undo', run: vi.fn() } });

    toaster.pause(id);
    vi.advanceTimersByTime(60_000);

    expect(toaster.toasts).toHaveLength(1);
  });

  it('gets the full time again once they look away', () => {
    const id = toaster.show({ message: 'Recipe deleted' });

    vi.advanceTimersByTime(5000);
    toaster.pause(id);
    toaster.resume(id);
    vi.advanceTimersByTime(5000);

    expect(toaster.toasts).toHaveLength(1);

    vi.advanceTimersByTime(1000);

    expect(toaster.toasts).toHaveLength(0);
  });
});

describe('dismissing by hand', () => {
  it('removes only that message', () => {
    const first = toaster.show({ message: 'first' });

    toaster.show({ message: 'second' });
    toaster.dismiss(first);

    expect(toaster.toasts.map((toast) => toast.message)).toEqual(['second']);
  });

  it('leaves no timer behind to fire into nothing', () => {
    const id = toaster.show({ message: 'first' });

    toaster.dismiss(id);
    vi.advanceTimersByTime(60_000);

    expect(toaster.toasts).toHaveLength(0);
  });
});
