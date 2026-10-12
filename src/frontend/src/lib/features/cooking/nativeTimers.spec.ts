import { beforeEach, describe, expect, it, vi } from 'vitest';

/* The app's timers: the system gets the whole set every time, and a press recorded on the lock screen is applied by the page exactly once, at the moment it was made. */

const plugin = vi.hoisted(() => ({
  sync: vi.fn(() => Promise.resolve()),
  drain: vi.fn(),
  close: vi.fn(() => Promise.resolve()),
  requestPermissions: vi.fn(() => Promise.resolve({ display: 'granted' })),
  addListener: vi.fn()
}));

let onAction: (() => void) | undefined;

vi.mock('@capacitor/core', () => ({ registerPlugin: () => plugin }));
vi.mock('$app/navigation', () => ({ goto: vi.fn(() => Promise.resolve()) }));
vi.mock('./timerState', () => ({ applyTimerAction: vi.fn(() => Promise.resolve(true)) }));

plugin.addListener.mockImplementation((_event: string, listener: () => void) => {
  onAction = listener;

  return Promise.resolve({ remove: () => Promise.resolve() });
});

const { drainNativeTimers, startNativeTimers, syncNativeTimers, timersChangedEvent } =
  await import('./nativeTimers');
const { applyTimerAction } = await import('./timerState');
const { goto } = await import('$app/navigation');

const press = (action: string, at = 1_000) => ({
  sessionId: 's1',
  stepIndex: 2,
  endsAt: 500,
  url: '/recipes/r1/cook?yield=2',
  action,
  at
});

beforeEach(() => {
  vi.clearAllMocks();
  plugin.drain.mockResolvedValue({ actions: [] });
});

describe('handing timers to the system', () => {
  it('sends the whole set, with the words the system shows in the app language', async () => {
    const timers = [{ stepIndex: 0, endsAt: 9_000, label: 'Simmer' }];

    await syncNativeTimers({ sessionId: 's1', url: '/recipes/r1/cook', timers });

    expect(plugin.sync).toHaveBeenCalledWith(
      expect.objectContaining({
        sessionId: 's1',
        url: '/recipes/r1/cook',
        timers,
        text: expect.objectContaining({ minute1: '+1 min', more: '+{count} more' })
      })
    );
  });

  it('ends everything when cooking ends', async () => {
    await syncNativeTimers(null);

    expect(plugin.sync).toHaveBeenCalledWith(
      expect.objectContaining({ sessionId: null, timers: [] })
    );
  });
});

describe('presses made on the lock screen', () => {
  it('applies each through the website action, at the moment it was pressed, then tells the page', async () => {
    plugin.drain.mockResolvedValue({ actions: [press('minute-1', 42_000)] });
    const changed = vi.fn();
    window.addEventListener(timersChangedEvent, changed);

    await drainNativeTimers();

    expect(applyTimerAction).toHaveBeenCalledWith(
      {
        type: 'culina:timer',
        sessionId: 's1',
        stepIndex: 2,
        endsAt: 500,
        url: '/recipes/r1/cook?yield=2'
      },
      'minute-1',
      42_000
    );
    expect(changed).toHaveBeenCalledTimes(1);
    expect(goto).not.toHaveBeenCalled();
    window.removeEventListener(timersChangedEvent, changed);
  });

  it('opens the recipe for a tap, and for "next step"', async () => {
    plugin.drain.mockResolvedValue({ actions: [press('open')] });
    await drainNativeTimers();
    expect(applyTimerAction).not.toHaveBeenCalled();
    expect(goto).toHaveBeenCalledWith('/recipes/r1/cook?yield=2');

    plugin.drain.mockResolvedValue({ actions: [press('next')] });
    await drainNativeTimers();
    expect(applyTimerAction).toHaveBeenCalledWith(expect.anything(), 'next', 1_000);
  });

  it('drains when the system says a press was recorded', async () => {
    await startNativeTimers();
    plugin.drain.mockResolvedValue({ actions: [press('dismiss')] });

    onAction?.();

    await vi.waitFor(() =>
      expect(applyTimerAction).toHaveBeenCalledWith(expect.anything(), 'dismiss', 1_000)
    );
  });
});
