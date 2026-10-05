import 'fake-indexeddb/auto';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { playKitchenChime } from './kitchenAudio';
import { notifyTimerDone } from './timerNotification';
import { haptics } from '$shell/haptics';
import { createTimers } from './timers.svelte';
import { applyTimerAction, editTimerState, forgetKitchen, type TimerNotice } from './timerState';
vi.mock('./timerNotification', () => ({
  notifyTimerDone: vi.fn(),
  closeTimerNotification: vi.fn(),
  requestTimerNotificationPermission: vi.fn()
}));
vi.mock('$shell/haptics', () => ({ haptics: { alarm: vi.fn() } }));
vi.mock('./kitchenAudio', () => ({ playKitchenChime: vi.fn(), unlockAudio: vi.fn() }));
beforeEach(async () => {
  vi.clearAllMocks();
  localStorage.clear();
  await forgetKitchen();
});

it('migrates legacy deadlines and reconciles worker dismissal without resurrecting them', async () => {
  const timer = { stepIndex: 0, endsAt: 100, label: 'Simmer' };
  localStorage.setItem('culina.timers.s1', JSON.stringify([timer]));
  const kitchen = createTimers(() => 's1');
  kitchen.load();
  await kitchen.refresh();
  expect((await editTimerState('s1'))!.timers).toEqual([timer]);
  const notice: TimerNotice = {
    type: 'culina:timer',
    sessionId: 's1',
    stepIndex: 0,
    endsAt: 100,
    url: '/'
  };
  await applyTimerAction(notice, 'dismiss');
  const restored = createTimers(() => 's1');
  restored.load();
  await restored.refresh();
  expect(restored.timers).toEqual([]);
});
it('reconciles extensions and a pending next step from the worker', async () => {
  const kitchen = createTimers(() => 's1');
  kitchen.start(0, 1, 'Simmer');
  await kitchen.refresh();
  const notice: TimerNotice = {
    type: 'culina:timer',
    sessionId: 's1',
    stepIndex: 0,
    endsAt: kitchen.timers[0]!.endsAt,
    url: '/'
  };
  await applyTimerAction(notice, 'minute-2');
  await kitchen.refresh();
  expect(kitchen.remaining(kitchen.timers[0]!)).toBeGreaterThan(119);
  await applyTimerAction({ ...notice, endsAt: kitchen.timers[0]!.endsAt }, 'next');
  await kitchen.refresh();
  expect(kitchen.consumeNextStep()).toBe(1);
  expect(kitchen.consumeNextStep()).toBeUndefined();
  await kitchen.refresh();
  expect((await editTimerState('s1'))!.nextStep).toBeUndefined();
});
it('does not clear running timers when the same session moves to a different step', async () => {
  const kitchen = createTimers(() => 's1');
  kitchen.start(0, 60, 'Simmer');
  kitchen.load();
  await kitchen.refresh();
  expect(kitchen.timers).toHaveLength(1);
});

afterEach(() => vi.useRealTimers());
it('rings once across two windows, using the chime and foreground haptic cadence', async () => {
  vi.useFakeTimers({ toFake: ['Date', 'setInterval', 'clearInterval'] });
  const first = createTimers(() => 's1');
  first.start(0, 1, 'Simmer');
  await first.refresh();
  const second = createTimers(() => 's1');
  second.load();
  await second.refresh();
  vi.setSystemTime(Date.now() + 2000);
  const stopFirst = first.tick();
  const stopSecond = second.tick();
  try {
    await Promise.all([first.refresh(), second.refresh()]);
    expect(playKitchenChime).toHaveBeenCalledOnce();
    expect(haptics.alarm).toHaveBeenCalledOnce();
    expect(notifyTimerDone).toHaveBeenCalledOnce();
    expect((await editTimerState('s1'))!.timers[0]!.notified).toBe(true);
  } finally {
    stopFirst();
    stopSecond();
  }
});
