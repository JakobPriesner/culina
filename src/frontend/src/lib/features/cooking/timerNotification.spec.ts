import { afterEach, expect, it, vi } from 'vitest';
import { notifyTimerDone } from './timerNotification';
import { alarmCadence, type TimerNotice } from './timerState';
const notice: TimerNotice = {
  type: 'culina:timer',
  sessionId: 's1',
  stepIndex: 2,
  endsAt: 100,
  url: '/recipes/r1/cook'
};
afterEach(() => vi.unstubAllGlobals());
it('offers actions with stable session/step identity and alarm vibration', async () => {
  vi.stubGlobal('Notification', { permission: 'granted', maxActions: 4 });
  const showNotification = vi.fn().mockResolvedValue(undefined);
  vi.stubGlobal('navigator', {
    serviceWorker: { getRegistration: vi.fn().mockResolvedValue({ showNotification }) }
  });
  await notifyTimerDone('Simmer', notice);
  expect(showNotification).toHaveBeenCalledWith(
    'Simmer',
    expect.objectContaining({
      data: notice,
      tag: 'culina-timer-s1-2',
      renotify: true,
      vibrate: alarmCadence,
      silent: false,
      actions: expect.arrayContaining([
        expect.objectContaining({ action: 'minute-1' }),
        expect.objectContaining({ action: 'minute-2' }),
        expect.objectContaining({ action: 'dismiss' }),
        expect.objectContaining({ action: 'next' })
      ])
    })
  );
});
it('respects platforms that only allow two actions', async () => {
  vi.stubGlobal('Notification', { permission: 'granted', maxActions: 2 });
  const showNotification = vi.fn().mockResolvedValue(undefined);
  vi.stubGlobal('navigator', {
    serviceWorker: { getRegistration: vi.fn().mockResolvedValue({ showNotification }) }
  });
  await notifyTimerDone('Simmer', notice);
  expect(
    showNotification.mock.calls[0]![1].actions.map((action: { action: string }) => action.action)
  ).toEqual(['minute-1', 'minute-2']);
});
it('retries without actions if the platform rejects them', async () => {
  vi.stubGlobal('Notification', { permission: 'granted' });
  const showNotification = vi
    .fn()
    .mockRejectedValueOnce(new Error('unsupported actions'))
    .mockResolvedValue(undefined);
  vi.stubGlobal('navigator', {
    serviceWorker: { getRegistration: vi.fn().mockResolvedValue({ showNotification }) }
  });
  await notifyTimerDone('Simmer', notice);
  expect(showNotification.mock.calls[1]![1].actions).toEqual([]);
});
it('does nothing when notification permission is denied', async () => {
  vi.stubGlobal('Notification', { permission: 'denied' });
  await expect(notifyTimerDone('Simmer', notice)).resolves.toBeUndefined();
});
