import { afterEach, expect, it, vi } from 'vitest';
import { createWakeLock } from './wakeLock.svelte';
afterEach(() => vi.restoreAllMocks());

it('releases a request that arrives after cooking ended', async () => {
  let arrive!: (lock: WakeLockSentinel) => void;
  const request = vi.fn(
    () =>
      new Promise<WakeLockSentinel>((resolve) => {
        arrive = resolve;
      })
  );
  vi.stubGlobal('navigator', { wakeLock: { request } });
  const lock = {
    released: false,
    release: vi.fn().mockResolvedValue(undefined),
    addEventListener: vi.fn()
  };
  const wake = createWakeLock();
  const stop = wake.engage();
  stop();
  arrive(lock as unknown as WakeLockSentinel);
  await vi.waitFor(() => expect(lock.release).toHaveBeenCalledOnce());
  expect(wake.held).toBe(false);
  vi.unstubAllGlobals();
});
it('reflects browser release and retakes on return', async () => {
  const lock = new EventTarget() as EventTarget & {
    released: boolean;
    release: () => Promise<void>;
  };
  lock.released = false;
  lock.release = vi.fn().mockResolvedValue(undefined);
  const request = vi.fn().mockResolvedValue(lock);
  vi.stubGlobal('navigator', { wakeLock: { request } });
  const wake = createWakeLock();
  const stop = wake.engage();
  await vi.waitFor(() => expect(wake.held).toBe(true));
  lock.dispatchEvent(new Event('release'));
  expect(wake.held).toBe(false);
  document.dispatchEvent(new Event('visibilitychange'));
  await vi.waitFor(() => expect(request).toHaveBeenCalledTimes(2));
  stop();
  vi.unstubAllGlobals();
});
