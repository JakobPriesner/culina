import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { render } from '@testing-library/svelte';
import { flushSync } from 'svelte';

import IntakeRuntime from './IntakeRuntime.svelte';
import { intakes, type IntakeJob } from './intakes.svelte';
import { importPush } from './push.svelte';

vi.mock('$features/auth/session.svelte', () => ({ session: { user: { userId: 'person' } } }));

/*
 * The runtime keeps the import status current. Polling a hidden tab, or asking
 * twice at start, is cost nobody sees, so the schedule is what is pinned here.
 */
const job = (stage: string): IntakeJob => ({ id: stage, stage }) as IntakeJob;

let hidden = false;

const setHidden = (value: boolean) => {
  hidden = value;
  document.dispatchEvent(new Event('visibilitychange'));
};

let refresh: ReturnType<typeof vi.spyOn>;

beforeEach(() => {
  vi.useFakeTimers();
  hidden = false;
  vi.spyOn(document, 'hidden', 'get').mockImplementation(() => hidden);
  vi.spyOn(importPush, 'restore').mockResolvedValue();
  refresh = vi.spyOn(intakes, 'refresh').mockResolvedValue();
  intakes.own(null);
});

afterEach(() => {
  vi.restoreAllMocks();
  vi.useRealTimers();
});

describe('polling', () => {
  it('asks once at start and not again for the idle interval', async () => {
    render(IntakeRuntime);
    flushSync();

    expect(refresh).toHaveBeenCalledTimes(1);

    await vi.advanceTimersByTimeAsync(14_000);

    expect(refresh).toHaveBeenCalledTimes(1);

    await vi.advanceTimersByTimeAsync(1_000);

    expect(refresh).toHaveBeenCalledTimes(2);
  });

  it('speeds up while an import is running', async () => {
    render(IntakeRuntime);
    flushSync();
    intakes.jobs = [job('running')];
    flushSync();
    refresh.mockClear();

    await vi.advanceTimersByTimeAsync(2_000);

    expect(refresh).toHaveBeenCalledTimes(1);
  });

  it('stops while the tab is hidden and catches up when it returns', async () => {
    render(IntakeRuntime);
    flushSync();
    intakes.jobs = [job('running')];
    flushSync();
    refresh.mockClear();

    setHidden(true);
    await vi.advanceTimersByTimeAsync(60_000);

    expect(refresh).not.toHaveBeenCalled();

    setHidden(false);
    await vi.advanceTimersByTimeAsync(0);

    expect(refresh).toHaveBeenCalledTimes(1);
  });
});
