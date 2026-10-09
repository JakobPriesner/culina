import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { render } from '@testing-library/svelte';
import { flushSync } from 'svelte';

import IntakeRuntime from './IntakeRuntime.svelte';
import { intakes } from './intakes.svelte';
import { importPush } from './push.svelte';

vi.mock('$features/auth/session.svelte', () => ({ session: { user: { userId: 'person' } } }));

/* The runtime follows the server's stream: no timers, no polling requests. */
let follow: ReturnType<typeof vi.spyOn>;
let stop: ReturnType<typeof vi.spyOn>;
let fetched: ReturnType<typeof vi.fn>;

beforeEach(() => {
  vi.useFakeTimers();
  vi.spyOn(importPush, 'restore').mockResolvedValue();
  follow = vi.spyOn(intakes, 'follow').mockImplementation(() => {});
  stop = vi.spyOn(intakes, 'stop').mockImplementation(() => {});
  fetched = vi.fn();
  vi.stubGlobal('fetch', fetched);
  intakes.own(null);
});

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe('following', () => {
  it('opens the stream at start and never polls', async () => {
    render(IntakeRuntime);
    flushSync();

    expect(follow).toHaveBeenCalledTimes(1);

    await vi.advanceTimersByTimeAsync(60_000);

    expect(follow).toHaveBeenCalledTimes(1);
    expect(fetched).not.toHaveBeenCalled();
  });

  it('tries again when the connection comes back', () => {
    render(IntakeRuntime);
    flushSync();
    follow.mockClear();

    window.dispatchEvent(new Event('online'));

    expect(follow).toHaveBeenCalledTimes(1);
  });

  it('closes the stream when it goes away', () => {
    const view = render(IntakeRuntime);
    flushSync();

    view.unmount();

    expect(stop).toHaveBeenCalled();
  });
});
