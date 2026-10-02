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

type FetchListener = (event: unknown) => void;

let onFetch: FetchListener;

beforeAll(async () => {
  const addEventListener = vi.spyOn(self, 'addEventListener');

  // Not a literal: tsconfig.worker.json checks the worker as a worker, and a
  // literal import would pull its webworker lib into the app's program.
  const worker = './service-worker';

  await import(/* @vite-ignore */ worker);

  onFetch = addEventListener.mock.calls.find(([type]) => type === 'fetch')?.[1] as FetchListener;
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
