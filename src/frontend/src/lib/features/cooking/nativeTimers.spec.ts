import { beforeEach, describe, expect, it, vi } from 'vitest';

/* The app's alarms: the operating system must hold exactly the running timers, and a button on one must act only on the timer it was shown for. */

interface Scheduled {
  id: number;
  extra: unknown;
  schedule?: { at?: Date };
  title?: string;
}

const pending: Scheduled[] = [];
let onAction: ((event: unknown) => void) | undefined;

const plugin = vi.hoisted(() => ({
  registerActionTypes: vi.fn(() => Promise.resolve()),
  createChannel: vi.fn(() => Promise.resolve()),
  addListener: vi.fn(),
  getPending: vi.fn(),
  schedule: vi.fn(),
  cancel: vi.fn(),
  getDeliveredNotifications: vi.fn(() => Promise.resolve({ notifications: [] })),
  removeDeliveredNotificationsById: vi.fn(() => Promise.resolve()),
  checkPermissions: vi.fn(() => Promise.resolve({ display: 'granted' })),
  requestPermissions: vi.fn(() => Promise.resolve({ display: 'granted' }))
}));

vi.mock('@capacitor/local-notifications', () => ({ LocalNotifications: plugin }));
vi.mock('$app/navigation', () => ({ goto: vi.fn(() => Promise.resolve()) }));
vi.mock('./timerState', () => ({ applyTimerAction: vi.fn(() => Promise.resolve(true)) }));

plugin.addListener.mockImplementation((_event: string, listener: (event: unknown) => void) => {
  onAction = listener;

  return Promise.resolve({ remove: () => Promise.resolve() });
});
plugin.getPending.mockImplementation(() => Promise.resolve({ notifications: [...pending] }));
plugin.schedule.mockImplementation(({ notifications }: { notifications: Scheduled[] }) => {
  for (const notification of notifications) {
    const at = pending.findIndex((kept) => kept.id === notification.id);

    if (at >= 0) pending.splice(at, 1);
    pending.push(notification);
  }

  return Promise.resolve({ notifications });
});
plugin.cancel.mockImplementation(({ notifications }: { notifications: { id: number }[] }) => {
  for (const { id } of notifications) {
    const at = pending.findIndex((kept) => kept.id === id);

    if (at >= 0) pending.splice(at, 1);
  }

  return Promise.resolve();
});

const { syncNativeTimers, startNativeTimers, timersChangedEvent } = await import('./nativeTimers');
const { applyTimerAction } = await import('./timerState');
const { goto } = await import('$app/navigation');

const inMinutes = (minutes: number) => Date.now() + minutes * 60_000;

const snapshot = (timers: { stepIndex: number; endsAt: number; pausedRemaining?: number }[]) => ({
  sessionId: 's1',
  url: '/recipes/r1/cook?yield=2',
  timers: timers.map((timer) => ({ label: `Step ${timer.stepIndex + 1}`, ...timer }))
});

const deadlines = () =>
  pending.map((kept) => (kept.extra as { endsAt: number }).endsAt).sort((a, b) => a - b);

beforeEach(() => {
  pending.length = 0;
  vi.clearAllMocks();
});

describe('handing timers to the operating system', () => {
  it('schedules each running timer at its deadline', async () => {
    const soon = inMinutes(3);
    const later = inMinutes(10);

    await syncNativeTimers(
      snapshot([
        { stepIndex: 0, endsAt: soon },
        { stepIndex: 2, endsAt: later }
      ])
    );

    expect(deadlines()).toEqual([soon, later]);
    expect(pending.map((kept) => kept.schedule?.at?.getTime()).sort()).toEqual([soon, later]);
    expect(
      pending.find((kept) => (kept.extra as { stepIndex: number }).stepIndex === 2)?.title
    ).toBe('Step 3');
  });

  it('leaves an unchanged timer alone and moves an extended one', async () => {
    const soon = inMinutes(3);
    const extended = inMinutes(4);
    const later = inMinutes(10);
    await syncNativeTimers(
      snapshot([
        { stepIndex: 0, endsAt: soon },
        { stepIndex: 2, endsAt: later }
      ])
    );
    plugin.schedule.mockClear();

    await syncNativeTimers(
      snapshot([
        { stepIndex: 0, endsAt: extended },
        { stepIndex: 2, endsAt: later }
      ])
    );

    expect(deadlines()).toEqual([extended, later]);
    expect(plugin.schedule).toHaveBeenCalledTimes(1);
  });

  it('cancels paused, finished and dismissed timers, and everything when cooking ends', async () => {
    await syncNativeTimers(snapshot([{ stepIndex: 0, endsAt: inMinutes(3) }]));

    await syncNativeTimers(
      snapshot([
        { stepIndex: 0, endsAt: inMinutes(3), pausedRemaining: 170 },
        { stepIndex: 1, endsAt: Date.now() - 1_000 }
      ])
    );
    expect(pending).toEqual([]);

    await syncNativeTimers(snapshot([{ stepIndex: 0, endsAt: inMinutes(3) }]));
    await syncNativeTimers(null);
    expect(pending).toEqual([]);
  });
});

describe('a button on a notification', () => {
  it('acts through the same conditional action as the website, then tells the page', async () => {
    await startNativeTimers();
    const changed = vi.fn();
    window.addEventListener(timersChangedEvent, changed);
    const notice = {
      type: 'culina:timer',
      sessionId: 's1',
      stepIndex: 0,
      endsAt: 123,
      url: '/recipes/r1/cook'
    };

    onAction?.({ actionId: 'minute-1', notification: { extra: notice } });
    await vi.waitFor(() => expect(changed).toHaveBeenCalled());

    expect(applyTimerAction).toHaveBeenCalledWith(notice, 'minute-1');
    expect(goto).not.toHaveBeenCalled();
    window.removeEventListener(timersChangedEvent, changed);
  });

  it('opens the recipe being cooked when the notification itself is tapped', async () => {
    await startNativeTimers();
    const notice = {
      type: 'culina:timer',
      sessionId: 's1',
      stepIndex: 0,
      endsAt: 123,
      url: '/recipes/r1/cook'
    };

    onAction?.({ actionId: 'tap', notification: { extra: notice } });

    await vi.waitFor(() => expect(goto).toHaveBeenCalledWith('/recipes/r1/cook'));
    expect(applyTimerAction).not.toHaveBeenCalled();
  });

  it('ignores a notification that is not a timer', async () => {
    await startNativeTimers();

    onAction?.({ actionId: 'dismiss', notification: { extra: { type: 'something-else' } } });
    await Promise.resolve();

    expect(applyTimerAction).not.toHaveBeenCalled();
  });
});
