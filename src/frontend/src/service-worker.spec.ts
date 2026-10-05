import { applyTimerAction, editTimerState } from './lib/features/cooking/timerState';
import { beforeAll, beforeEach, expect, it, vi } from 'vitest';

/*
 * A stalled connection never rejects a fetch, it just never answers. These
 * tests pin that the worker stops waiting for it when it has a copy, before
 * the app stops waiting for the worker.
 */
vi.mock('$service-worker', () => ({
  base: '',
  build: [],
  files: [],
  prerendered: [],
  version: 'test'
}));

vi.mock('./lib/features/cooking/timerState', () => ({
  applyTimerAction: vi.fn(),
  editTimerState: vi.fn(),
  forgetKitchen: vi.fn().mockResolvedValue(undefined)
}));

type Listener = (event: unknown) => void;

let onFetch: Listener;
let onActivate: Listener;
let onNotificationClick: Listener;
let onPush: Listener;

beforeAll(async () => {
  const addEventListener = vi.spyOn(self, 'addEventListener');

  // Not a literal: tsconfig.worker.json checks the worker as a worker, and a
  // literal import would pull its webworker lib into the app's program.
  const worker = './service-worker';

  await import(/* @vite-ignore */ worker);

  const listenerFor = (type: string) =>
    addEventListener.mock.calls.find(([candidate]) => candidate === type)?.[1] as Listener;

  onFetch = listenerFor('fetch');
  onActivate = listenerFor('activate');
  onNotificationClick = listenerFor('notificationclick');
  onPush = listenerFor('push');
  addEventListener.mockRestore();
});

const stored = new Response('{"title":"as last seen"}', { status: 200 });

beforeEach(() => {
  vi.useFakeTimers();
  vi.stubGlobal('caches', {
    open: () =>
      Promise.resolve({
        match: () => Promise.resolve(stored),
        put: () => Promise.resolve(),
        keys: () => Promise.resolve([]),
        delete: () => Promise.resolve(true)
      })
  });

  return () => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
  };
});

/** Sends a recipe read through the worker and records what it answers. */
function readRecipe() {
  const answer: { response?: Response } = {};

  onFetch({
    request: new Request(new URL('/api/v1/recipes/r1', location.origin)),
    respondWith: (pending: Promise<Response>) => void pending.then((r) => (answer.response = r)),
    waitUntil: () => {}
  });

  return answer;
}

it('answers a recipe read from the cache when the network stalls', async () => {
  vi.stubGlobal('fetch', () => new Promise(() => {}));

  const answer = readRecipe();

  await vi.advanceTimersByTimeAsync(2_500);

  expect(answer.response).toBe(stored);
});

it('still answers from the network when it answers in time', async () => {
  const fresh = new Response('{"title":"just edited"}', { status: 200 });

  vi.stubGlobal('fetch', () => Promise.resolve(fresh));

  const answer = readRecipe();

  await vi.advanceTimersByTimeAsync(0);

  expect(answer.response).toBe(fresh);
});

it('keeps the private cache when a new build takes over', async () => {
  const deleted: string[] = [];

  vi.stubGlobal('caches', {
    keys: () => Promise.resolve(['culina-old', 'culina-private', 'culina-test']),
    delete: (key: string) => Promise.resolve(deleted.push(key) > 0)
  });
  vi.stubGlobal('clients', { claim: () => Promise.resolve() });

  let activated: Promise<unknown> = Promise.resolve();

  onActivate({ waitUntil: (pending: Promise<unknown>) => (activated = pending) });
  await activated;

  expect(deleted).toEqual(['culina-old']);
});

it('forgets the private cache when the server says the session is gone', async () => {
  const deleted: string[] = [];

  vi.stubGlobal('caches', {
    open: () => Promise.resolve({ match: () => Promise.resolve(undefined) }),
    delete: (key: string) => Promise.resolve(deleted.push(key) > 0)
  });
  vi.stubGlobal('fetch', () => Promise.resolve(new Response(null, { status: 401 })));

  let refreshed: Promise<unknown> = Promise.resolve();

  onFetch({
    request: new Request(new URL('/api/v1/users/me', location.origin)),
    respondWith: () => {},
    waitUntil: (pending: Promise<unknown>) => (refreshed = pending)
  });
  await vi.advanceTimersByTimeAsync(0);
  await refreshed;

  expect(deleted).toEqual(['culina-private']);
});

const timerNotice = {
  type: 'culina:timer',
  sessionId: 's1',
  stepIndex: 0,
  endsAt: 100,
  url: '/recipes/r1/cook?yield=4'
};
function clickNotification(action: string) {
  let finished: Promise<unknown> = Promise.resolve();
  const close = vi.fn();
  onNotificationClick({
    action,
    notification: { data: timerNotice, close },
    waitUntil: (promise: Promise<unknown>) => {
      finished = promise;
    }
  });
  return { close, finished };
}
it('handles extension in the worker without foregrounding a window', async () => {
  const focus = vi.fn();
  const postMessage = vi.fn();
  vi.stubGlobal('clients', {
    matchAll: vi.fn().mockResolvedValue([{ focus, postMessage }]),
    openWindow: vi.fn()
  });
  vi.mocked(applyTimerAction).mockResolvedValue(true);
  vi.mocked(editTimerState).mockResolvedValue({ timers: [], url: '' });
  const click = clickNotification('minute-1');
  await click.finished;
  expect(applyTimerAction).toHaveBeenLastCalledWith(timerNotice, 'minute-1');
  expect(postMessage).toHaveBeenCalledWith({ type: 'culina:timers-changed', sessionId: 's1' });
  expect(focus).not.toHaveBeenCalled();
  expect(click.close).toHaveBeenCalledOnce();
});
it('persists next step without opening the app when all windows are closed', async () => {
  const openWindow = vi.fn();
  vi.stubGlobal('clients', { matchAll: vi.fn().mockResolvedValue([]), openWindow });
  vi.mocked(applyTimerAction).mockResolvedValue(true);
  vi.mocked(editTimerState).mockResolvedValue({ timers: [], url: '' });
  await clickNotification('next').finished;
  expect(applyTimerAction).toHaveBeenLastCalledWith(timerNotice, 'next');
  expect(openWindow).not.toHaveBeenCalled();
});
it('opens the matching cooking route on a body click', async () => {
  const openWindow = vi.fn().mockResolvedValue(undefined);
  vi.stubGlobal('clients', { matchAll: vi.fn().mockResolvedValue([]), openWindow });
  await clickNotification('').finished;
  expect(openWindow).toHaveBeenCalledWith(new URL(timerNotice.url, location.origin).href);
});
it('navigates an existing window before focusing it on a body click', async () => {
  const navigate = vi.fn().mockResolvedValue(undefined);
  const focus = vi.fn().mockResolvedValue(undefined);
  vi.stubGlobal('clients', {
    matchAll: vi.fn().mockResolvedValue([{ url: location.origin, navigate, focus }])
  });
  await clickNotification('').finished;
  expect(navigate).toHaveBeenCalledWith(new URL(timerNotice.url, location.origin).href);
  expect(focus).toHaveBeenCalledOnce();
});

it('shows a completion push without needing an open page, with one stable tag', async () => {
  const showNotification = vi.fn().mockResolvedValue(undefined);
  vi.stubGlobal('registration', { showNotification });
  let completion: Promise<unknown> | undefined;
  onPush({
    data: {
      json: () => ({
        title: 'Your recipe is ready',
        body: 'Saved',
        url: '/recipes/imports/00000000-0000-4000-8000-000000000055',
        tag: 'recipe-intake-55'
      })
    },
    waitUntil: (promise: Promise<unknown>) => (completion = promise)
  });
  await completion;
  expect(showNotification).toHaveBeenCalledWith(
    'Your recipe is ready',
    expect.objectContaining({
      tag: 'recipe-intake-55',
      data: { type: 'culina:intake', url: '/recipes/imports/00000000-0000-4000-8000-000000000055' }
    })
  );
});
it('opens the saved import review from its notification', async () => {
  const openWindow = vi.fn().mockResolvedValue(undefined);
  vi.stubGlobal('clients', { matchAll: vi.fn().mockResolvedValue([]), openWindow });
  let completion: Promise<unknown> | undefined;
  const url = '/recipes/imports/00000000-0000-4000-8000-000000000055';
  onNotificationClick({
    notification: { close: vi.fn(), data: { type: 'culina:intake', url } },
    waitUntil: (promise: Promise<unknown>) => (completion = promise)
  });
  await completion;
  expect(openWindow).toHaveBeenCalledWith(new URL(url, location.origin).href);
});
it('ignores a push trying to send the browser somewhere else', () => {
  const showNotification = vi.fn();
  vi.stubGlobal('registration', { showNotification });
  onPush({
    data: {
      json: () => ({
        title: 'Bad',
        url: 'https://example.com/recipes/imports/00000000-0000-4000-8000-000000000055'
      })
    },
    waitUntil: vi.fn()
  });
  expect(showNotification).not.toHaveBeenCalled();
});
