import 'fake-indexeddb/auto';
import { beforeEach, expect, it } from 'vitest';
import { applyTimerAction, editTimerState, forgetKitchen, type TimerNotice } from './timerState';

const notice: TimerNotice = {
  type: 'culina:timer',
  sessionId: 's1',
  stepIndex: 0,
  endsAt: 100,
  url: '/recipes/r1/cook'
};
beforeEach(async () => {
  await forgetKitchen();
  await editTimerState('s1', () => ({
    url: notice.url,
    timers: [{ stepIndex: 0, endsAt: 100, label: 'Simmer' }]
  }));
});

it.each(['minute-1', 'minute-2'])('extends from now without a window: %s', async (action) => {
  const before = Date.now();
  expect(await applyTimerAction(notice, action)).toBe(true);
  const timer = (await editTimerState('s1'))!.timers[0]!;
  expect(timer.endsAt).toBeGreaterThanOrEqual(before + (action === 'minute-1' ? 60_000 : 120_000));
  expect(timer.notified).toBeUndefined();
});
it('does not let an old notification change a replacement timer', async () => {
  await editTimerState('s1', (state) => ({
    ...state,
    timers: [{ stepIndex: 0, endsAt: 200, label: 'New' }]
  }));
  expect(await applyTimerAction(notice, 'dismiss')).toBe(false);
  expect((await editTimerState('s1'))!.timers[0]!.endsAt).toBe(200);
});
it('dismisses only the matching session and step', async () => {
  await editTimerState('s2', (state) => ({
    ...state,
    timers: [{ stepIndex: 0, endsAt: 100, label: 'Other' }]
  }));
  expect(await applyTimerAction(notice, 'dismiss')).toBe(true);
  expect((await editTimerState('s1'))!.timers).toEqual([]);
  expect((await editTimerState('s2'))!.timers).toHaveLength(1);
});
it('persists the next step and consumes a notification only once', async () => {
  expect(await applyTimerAction(notice, 'next')).toBe(true);
  expect((await editTimerState('s1'))!.nextStep).toBe(1);
  expect(await applyTimerAction(notice, 'next')).toBe(false);
});
it('serializes competing actions on the same alert', async () => {
  const results = await Promise.all([
    applyTimerAction(notice, 'minute-1'),
    applyTimerAction(notice, 'minute-2')
  ]);
  expect(results.filter(Boolean)).toHaveLength(1);
  expect((await editTimerState('s1'))!.timers).toHaveLength(1);
});
it('forgets all sessions on sign-out', async () => {
  await forgetKitchen();
  expect((await editTimerState('s1'))!.timers).toEqual([]);
});
