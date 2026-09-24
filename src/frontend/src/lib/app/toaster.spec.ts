import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { render, screen, waitFor, within } from '@testing-library/svelte';

import LocaleKeyedToaster from '$lib/test/LocaleKeyedToaster.svelte';

import { m } from './i18n';
import { preferences } from './preferences.svelte';
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

/** Words that do not depend on the language, for the tests that are about timing. */
const words = (text: string) => () => text;

describe('a message', () => {
  it('appears and then goes away on its own', () => {
    toaster.show({ message: words('Recipe deleted') });

    expect(toaster.toasts).toHaveLength(1);

    vi.advanceTimersByTime(6000);

    expect(toaster.toasts).toHaveLength(0);
  });

  it('stays until dismissed when it is given no duration', () => {
    toaster.show({ message: words('Sync failed'), durationMs: 0 });

    vi.advanceTimersByTime(60_000);

    expect(toaster.toasts).toHaveLength(1);
  });

  it('keeps the newest when too many stack up', () => {
    for (const message of ['first', 'second', 'third', 'fourth']) {
      toaster.show({ message: words(message) });
    }

    expect(toaster.toasts.map((toast) => toast.message())).toEqual(['second', 'third', 'fourth']);
  });
});

describe('undo', () => {
  it('runs the action and takes the message away', () => {
    const restore = vi.fn();

    const id = toaster.show({
      message: words('Recipe deleted'),
      action: { label: words('Undo'), run: restore }
    });

    toaster.act(id);

    expect(restore).toHaveBeenCalledOnce();
    expect(toaster.toasts).toHaveLength(0);
  });

  it('cannot be pressed twice, so an undo cannot be undone', () => {
    const restore = vi.fn();

    const id = toaster.show({
      message: words('Deleted'),
      action: { label: words('Undo'), run: restore }
    });

    toaster.act(id);
    toaster.act(id);

    expect(restore).toHaveBeenCalledOnce();
  });

  it('is still there while someone is reading the message', () => {
    const id = toaster.show({
      message: words('Recipe deleted'),
      action: { label: words('Undo'), run: vi.fn() }
    });

    toaster.pause(id);
    vi.advanceTimersByTime(60_000);

    expect(toaster.toasts).toHaveLength(1);
  });

  it('gets the full time again once they look away', () => {
    const id = toaster.show({ message: words('Recipe deleted') });

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
    const first = toaster.show({ message: words('first') });

    toaster.show({ message: words('second') });
    toaster.dismiss(first);

    expect(toaster.toasts.map((toast) => toast.message())).toEqual(['second']);
  });

  it('leaves no timer behind to fire into nothing', () => {
    const id = toaster.show({ message: words('first') });

    toaster.dismiss(id);
    vi.advanceTimersByTime(60_000);

    expect(toaster.toasts).toHaveLength(0);
  });
});

/*
 * A toast can be on screen when somebody changes language — the update offer
 * stays until it is answered — and it has to change with everything around
 * it, message and buttons as one.
 */
describe('a change of language', () => {
  beforeEach(() => vi.useRealTimers());

  afterEach(() => {
    preferences.setLocale('en');
    preferences.reset();
  });

  it('says a message already on screen in the new language', async () => {
    preferences.setLocale('de');
    render(LocaleKeyedToaster);

    toaster.show({
      message: m['app.update.available'],
      durationMs: 0,
      action: { label: m['app.update.reload'], run: vi.fn() }
    });

    expect(await screen.findByRole('status')).toHaveTextContent('Eine neue Version');

    preferences.setLocale('en');

    await waitFor(() =>
      expect(screen.getByRole('status')).toHaveTextContent(m['app.update.available']())
    );

    const toast = screen.getByRole('status');

    expect(within(toast).getByRole('button', { name: m['app.update.reload']() })).toBeVisible();
    expect(within(toast).getByRole('button', { name: m['app.dismiss']() })).toBeVisible();
  });
});
