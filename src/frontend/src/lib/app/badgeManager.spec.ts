import { expect, it, vi } from 'vitest';
import { createBadgeManager } from './badgeManager.svelte';

it('uses timers before shopping and clears when both finish', async () => {
  const api = {
    setAppBadge: vi.fn().mockResolvedValue(undefined),
    clearAppBadge: vi.fn().mockResolvedValue(undefined)
  };
  const manager = createBadgeManager(() => api);
  manager.update(2, 12);
  await vi.waitFor(() => expect(api.setAppBadge).toHaveBeenLastCalledWith(2));
  manager.update(0, 12);
  await vi.waitFor(() => expect(api.setAppBadge).toHaveBeenLastCalledWith(12));
  manager.update(0, 0);
  await vi.waitFor(() => expect(api.clearAppBadge).toHaveBeenCalledOnce());
});
it('lets a later clear win over a slow badge update', async () => {
  let finish!: () => void;
  const api = {
    setAppBadge: vi.fn(
      () =>
        new Promise<void>((resolve) => {
          finish = resolve;
        })
    ),
    clearAppBadge: vi.fn().mockResolvedValue(undefined)
  };
  const manager = createBadgeManager(() => api);
  manager.update(1, 0);
  manager.update(0, 7);
  manager.clear();
  finish();
  await vi.waitFor(() => expect(api.clearAppBadge).toHaveBeenCalledOnce());
  expect(api.setAppBadge).toHaveBeenCalledOnce();
});
it('silently tolerates missing APIs and permission failures', async () => {
  const manager = createBadgeManager(() => ({}));
  manager.update(1, 0);
  manager.clear();
  const denied = createBadgeManager(() => ({
    setAppBadge: vi.fn().mockRejectedValue(new Error('denied'))
  }));
  denied.update(1, 0);
  await Promise.resolve();
  denied.clear();
});
